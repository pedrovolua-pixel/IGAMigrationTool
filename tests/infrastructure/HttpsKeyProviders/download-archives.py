#!/usr/bin/env python3
"""Acquire the exact diagnostic archive inventory, without extraction or execution."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.request

SPEC = importlib.util.spec_from_file_location("key_signature_verifier", Path(__file__).with_name("verify-signatures.py"))
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)
INDEX_URL = "https://api.nuget.org/v3/index.json"
PACKAGE_BASE = "https://api.nuget.org/v3-flatcontainer/"
INDEX_LIMIT = 1024 * 1024
ARCHIVE_LIMIT = 64 * 1024 * 1024
REQUEST_TIMEOUT = 30  # Diagnostic socket timeout, not a production service deadline.


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise v.Denied("HTTP redirects are denied")


def new_directory(path):
    absolute = path.absolute()
    if ".." in absolute.parts or absolute.exists() or absolute.is_symlink():
        raise v.Denied("Output must be a new nonexistent directory")
    if not absolute.parent.is_dir():
        raise v.Denied("Output parent must exist")
    if any(p.is_symlink() for p in [absolute.parent, *absolute.parent.parents]):
        raise v.Denied("Symlink output ancestry is denied; use a canonical path")
    return absolute


def fetch(url, limit, open_url, entry, sink=None):
    entry.update(url=url, startedAtUtc=v.utc_now(), timeoutSeconds=REQUEST_TIMEOUT,
                 maximumBytes=limit, status="FAILED")
    h = hashlib.sha256()
    size = 0
    body = bytearray() if sink is None else None
    request = urllib.request.Request(url, headers={"Accept-Encoding": "identity"})
    try:
        with open_url(request, timeout=REQUEST_TIMEOUT) as response:
            entry["httpStatus"] = response.status
            if response.status != 200 or response.geturl() != url:
                raise v.Denied("Require HTTP200 at the exact approved URL")
            length = response.headers.get("Content-Length")
            if length is not None:
                if not re.fullmatch(r"[0-9]+", length) or not 0 < int(length) <= limit:
                    raise v.Denied("Invalid or oversized declared response length")
                length = int(length)
            entry["declaredBytes"] = length
            encoding = response.headers.get("Content-Encoding", "identity")
            if encoding.lower() != "identity":
                raise v.Denied("Encoded responses are denied")
            while True:
                block = response.read(64 * 1024)
                if not isinstance(block, bytes):
                    raise v.Denied("Response must preserve raw bytes")
                if not block:
                    break
                next_size = size + len(block)
                if next_size > limit or length is not None and next_size > length:
                    raise v.Denied("Response exceeds its size bound or declared length")
                size = next_size
                h.update(block)
                if sink is None:
                    body.extend(block)
                else:
                    sink.write(block)
            if size == 0 or length is not None and size != length:
                raise v.Denied("Empty or truncated response")
            entry.update(status="DOWNLOADED", receivedBytes=size, sha256=h.hexdigest())
            return bytes(body) if sink is None else None
    finally:
        entry["retainedBytes"] = size
        entry["partialSHA256"] = h.hexdigest()
        entry["finishedAtUtc"] = v.utc_now()


def acquire(archives, receipts, *, open_url=None):
    # Injection is for HTTP mocks only; the CLI exposes no endpoint or simulation options.
    manifest = v.load_manifest()  # Closed, hash-pinned authority before any network request.
    v.environment_check(os.environ)  # Reject overrides without clearing normal proxy settings.
    archives = new_directory(archives)
    receipts = new_directory(receipts)
    if archives == receipts:
        raise v.Denied("Archive and receipt destinations must differ")
    receipts.mkdir()
    receipt = {"schemaVersion": 1, "status": "FAILED", "manifestSHA256": v.MANIFEST_SHA256,
               "sourceCommit": manifest["sourceCommit"], "experimentalLockSHA256": manifest["experimentalLockSHA256"],
               "graphKind": manifest["graphKind"], "startedAtUtc": v.utc_now(), "index": {}, "packages": []}
    try:
        archives.mkdir()  # No exist_ok; do not replace a raced output.
        staging = archives / ".staging"
        staging.mkdir()
        if open_url is None:
            # Default proxy handlers, verified HTTPS context and normal host trust are preserved.
            open_url = urllib.request.build_opener(NoRedirect()).open
        raw = fetch(INDEX_URL, INDEX_LIMIT, open_url, receipt["index"])
        index = json.loads(raw, object_pairs_hook=v.closed_pairs)
        if not isinstance(index, dict) or index.get("version") != "3.0.0" or not isinstance(index.get("resources"), list):
            raise v.Denied("Unexpected NuGet V3 service index")
        bases = [r.get("@id") for r in index["resources"] if isinstance(r, dict) and r.get("@type") == "PackageBaseAddress/3.0.0"]
        if bases != [PACKAGE_BASE]:
            raise v.Denied("Require one exact official package base resource")
        receipt["packageBase"] = PACKAGE_BASE
        v.write_json(receipts / "download-receipt.json", receipt)
        for p in manifest["packages"]:
            entry = {"id": p["id"], "version": p["version"], "fileName": p["fileName"],
                     "expectedArchiveSHA256": p["archiveSHA256"]}
            receipt["packages"].append(entry)
            url = PACKAGE_BASE + p["id"].lower() + "/" + p["version"] + "/" + p["fileName"]
            temporary = staging / (p["fileName"] + ".part")
            with temporary.open("xb") as stream:
                fetch(url, ARCHIVE_LIMIT, open_url, entry, stream)
            if entry["sha256"] != p["archiveSHA256"] or v.digest(temporary) != p["archiveSHA256"]:
                raise v.Denied("Downloaded archive digest mismatch")
            # Same-filesystem hard link publishes atomically and refuses to overwrite a raced file.
            os.link(temporary, archives / p["fileName"])
            temporary.unlink()
            entry["status"] = "HASH_VERIFIED"
            v.write_json(receipts / "download-receipt.json", receipt)
        staging.rmdir()
        v.inventory(archives, manifest["packages"])
        if len(receipt["packages"]) != 37 or any(p["status"] != "HASH_VERIFIED" for p in receipt["packages"]):
            raise v.Denied("Incomplete acquisition")
        receipt["status"] = "PASS"
    except (OSError, ValueError, urllib.error.URLError) as error:
        # Metadata only; never persist a response body or arbitrary exception text.
        receipt["failureType"] = type(error).__name__
        if isinstance(error, urllib.error.HTTPError):
            receipt["failureHttpStatus"] = error.code
    finally:
        receipt["finishedAtUtc"] = v.utc_now()
        v.write_json(receipts / "download-receipt.json", receipt)
    return 0 if receipt["status"] == "PASS" else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archives", type=Path, required=True)
    parser.add_argument("--receipts", type=Path, required=True)
    args = parser.parse_args()
    try:
        return acquire(args.archives, args.receipts)
    except (OSError, ValueError) as error:
        print("DENIED: " + type(error).__name__, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
