"""Mock HTTP safety checks only: no network, dotnet or package signature execution."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
import urllib.error

SPEC = importlib.util.spec_from_file_location("key_archive_downloader", Path(__file__).with_name("download-archives.py"))
d = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(d)


class Response(io.BytesIO):
    def __init__(self, raw, url, status=200, length="auto", encoding="identity"):
        super().__init__(raw)
        self.url = url
        self.status = status
        self.headers = {"Content-Encoding": encoding}
        if length is not None:
            self.headers["Content-Length"] = str(len(raw)) if length == "auto" else str(length)

    def geturl(self):
        return self.url


class DownloadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="iga_ci_download_mock_")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.archives = self.root / "new archives"
        self.receipts = self.root / "new receipts"
        self.manifest = json.loads(d.v.MANIFEST_PATH.read_bytes())
        self.payloads = {}
        for p in self.manifest["packages"]:
            raw = ("synthetic payload; NOT a real package: " + p["fileName"]).encode()
            self.payloads[p["fileName"]] = raw
            p["archiveSHA256"] = hashlib.sha256(raw).hexdigest()
        self.manifest_path = self.root / "manifest.json"
        self.bind(self.manifest)
        self.patch_path = patch.object(d.v, "MANIFEST_PATH", self.manifest_path)
        self.patch_path.start()
        self.addCleanup(self.patch_path.stop)
        self.patch_hash = patch.object(d.v, "MANIFEST_SHA256", hashlib.sha256(self.manifest_path.read_bytes()).hexdigest())
        self.patch_hash.start()
        self.addCleanup(self.patch_hash.stop)
        self.index = {"version": "3.0.0", "resources": [{"@type": "PackageBaseAddress/3.0.0", "@id": d.PACKAGE_BASE}]}
        self.open = Mock(side_effect=self.good_response)

    def bind(self, m):
        self.manifest_path.write_text(json.dumps(m) + "\n")

    def good_response(self, request, timeout):
        self.assertEqual(timeout, 30)
        self.assertEqual(request.get_header("Accept-encoding"), "identity")
        self.assertNotIn("Authorization", request.headers)
        url = request.full_url
        raw = json.dumps(self.index).encode() if url == d.INDEX_URL else self.payloads[url.rsplit('/', 1)[1]]
        return Response(raw, url)

    def invoke(self):
        return d.acquire(self.archives, self.receipts, open_url=self.open)

    def receipt(self):
        return json.loads((self.receipts / "download-receipt.json").read_text())

    def denied_pre_network(self):
        with self.assertRaises((ValueError, OSError)):
            self.invoke()
        self.open.assert_not_called()
        self.assertFalse(self.receipts.exists())

    def test_exact37_positive_url_bytes_hashes_and_closed_receipt(self):
        self.assertEqual(self.invoke(), 0)  # Synthetic bytes/HTTP mocks, not integrity proof.
        self.assertEqual(self.open.call_count, 38)
        self.assertEqual({p.name for p in self.archives.iterdir()}, set(self.payloads))
        r = self.receipt()
        self.assertEqual(r["status"], "PASS")
        self.assertEqual(len(r["packages"]), 37)
        self.assertNotIn("body", r["index"])
        for call, p, entry in zip(self.open.call_args_list[1:], self.manifest["packages"], r["packages"]):
            expected = d.PACKAGE_BASE + p["id"].lower() + '/' + p["version"] + '/' + p["fileName"]
            self.assertEqual(call.args[0].full_url, expected)
            raw = (self.archives / p["fileName"]).read_bytes()
            self.assertEqual(raw, self.payloads[p["fileName"]])
            self.assertEqual(entry["sha256"], hashlib.sha256(raw).hexdigest())
            self.assertEqual(entry["partialSHA256"], entry["sha256"])
            self.assertEqual(entry["receivedBytes"], len(raw))
            self.assertEqual(entry["status"], "HASH_VERIFIED")

    def test_pinned_manifest_before_network(self):
        self.manifest_path.write_bytes(self.manifest_path.read_bytes() + b" ")
        self.denied_pre_network()

    def test_trust_overrides_before_network_without_value_disclosure(self):
        for name in ("SSL_CERT_FILE", "NUGET_CERT_REVOCATION_MODE", "DOTNET_NUGET_SIGNATURE_VERIFICATION"):
            with self.subTest(name=name), patch.dict(d.os.environ, {name: "synthetic-private-value"}):
                with self.assertRaises(ValueError) as caught:
                    self.invoke()
                self.assertNotIn("synthetic-private-value", str(caught.exception))
                self.open.assert_not_called()
                self.assertFalse(self.receipts.exists())

    def test_hostile_manifest_name_is_denied_before_network(self):
        self.manifest["packages"][0]["fileName"] = "../../outside"
        self.bind(self.manifest)
        with patch.object(d.v, "MANIFEST_SHA256", hashlib.sha256(self.manifest_path.read_bytes()).hexdigest()):
            self.denied_pre_network()

    def test_outputs_existing_same_missing_parent_and_file_collisions(self):
        self.archives.mkdir()
        self.denied_pre_network()
        self.archives.rmdir()
        self.receipts = self.archives
        self.denied_pre_network()
        self.receipts = self.root / "missing parent" / "receipts"
        self.denied_pre_network()
        self.receipts = self.root / "new receipts"
        self.archives.write_bytes(b"preserve this existing file")
        self.denied_pre_network()
        self.assertEqual(self.archives.read_bytes(), b"preserve this existing file")

    def test_symlink_output_ancestry_and_dangling_destination(self):
        actual = self.root / "actual"
        actual.mkdir()
        alias = self.root / "alias"
        alias.symlink_to(actual, target_is_directory=True)
        self.archives = alias / "out"
        self.denied_pre_network()
        self.archives = self.root / "new archives"
        self.archives.symlink_to(self.root / "missing", target_is_directory=True)
        self.denied_pre_network()

    def test_service_index_bad_duplicate_or_absent_resources(self):
        variants = [{}, {"version": "2.0.0", "resources": []},
                    {"version": "3.0.0", "resources": [{"@type": "PackageBaseAddress/3.0.0", "@id": "http://api.nuget.org/v3-flatcontainer/"}]},
                    {"version": "3.0.0", "resources": [{"@type": "PackageBaseAddress/3.0.0", "@id": "https://evil.invalid/"}]},
                    {"version": "3.0.0", "resources": self.index["resources"] * 2}]
        for i, index in enumerate(variants):
            with self.subTest(i=i):
                self.archives = self.root / ("archives-" + str(i))
                self.receipts = self.root / ("receipts-" + str(i))
                self.index = index
                self.open.reset_mock()
                self.assertEqual(self.invoke(), 1)
                self.assertEqual(self.open.call_count, 1)
                self.assertEqual(self.receipt()["status"], "FAILED")

    def test_duplicate_index_keys_and_non_json_no_archive_request(self):
        for i, raw in enumerate((b'{"version":"3.0.0","version":"3.0.0","resources":[]}', b'<html>error</html>')):
            self.archives = self.root / ("archives-" + str(i))
            self.receipts = self.root / ("receipts-" + str(i))
            self.open = Mock(return_value=Response(raw, d.INDEX_URL))
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.open.call_count, 1)

    def test_redirect_handler_never_follows_any_status_or_scheme(self):
        handler = d.NoRedirect()
        for status in (301, 302, 303, 307, 308):
            for target in ('https://api.nuget.org/elsewhere', 'https://evil.invalid/', 'http://api.nuget.org/'):
                with self.subTest(status=status, target=target), self.assertRaises(ValueError):
                    handler.redirect_request(None, None, status, 'redirect', {}, target)

    def test_status_and_final_url_denied(self):
        for i, (status, url) in enumerate(((404, d.INDEX_URL), (302, d.INDEX_URL), (200, 'https://evil.invalid/'))):
            self.archives = self.root / ("archives-" + str(i))
            self.receipts = self.root / ("receipts-" + str(i))
            self.open = Mock(return_value=Response(b'{}', url, status=status))
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.open.call_count, 1)
            self.assertEqual(self.receipt()["index"]["httpStatus"], status)

    def test_index_lengths_oversize_encoding_truncation_and_empty(self):
        raw = json.dumps(self.index).encode()
        variants = [(raw, '-1', 'identity'), (raw, 'abc', 'identity'), (raw, d.INDEX_LIMIT + 1, 'identity'),
                    (raw, len(raw) + 1, 'identity'), (raw, len(raw) - 1, 'identity'),
                    (raw, len(raw), 'gzip'), (b'', None, 'identity'), (b'x' * (d.INDEX_LIMIT + 1), None, 'identity')]
        for i, (body, length, encoding) in enumerate(variants):
            self.archives = self.root / ("archives-" + str(i))
            self.receipts = self.root / ("receipts-" + str(i))
            self.open = Mock(return_value=Response(body, d.INDEX_URL, length=length, encoding=encoding))
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.open.call_count, 1)
            self.assertEqual(self.receipt()["status"], "FAILED")

    def test_archive_hash_failure_stops_first_and_preserves_partial_receipts(self):
        original = self.good_response
        calls = []

        def fake(req, timeout):
            calls.append(req.full_url)
            if len(calls) == 3:
                return Response(b'wrong package bytes', req.full_url)
            return original(req, timeout)

        self.open = Mock(side_effect=fake)
        self.assertEqual(self.invoke(), 1)
        r = self.receipt()
        self.assertEqual(self.open.call_count, 3)
        self.assertEqual(len(r["packages"]), 2)
        self.assertEqual(r["packages"][0]["status"], "HASH_VERIFIED")
        self.assertEqual(r["packages"][1]["status"], "DOWNLOADED")
        self.assertEqual(r["packages"][1]["sha256"], hashlib.sha256(b'wrong package bytes').hexdigest())
        self.assertEqual(r["status"], "FAILED")

    def test_archive_read_timeout_preserves_retained_byte_digest(self):
        class Broken(Response):
            def read(self, size):
                if self.tell() != 0:
                    raise TimeoutError('synthetic-private-exception-content')
                return super().read(size)

        def fake(req, timeout):
            if req.full_url == d.INDEX_URL:
                return self.good_response(req, timeout)
            return Broken(b'partial bytes', req.full_url, length=100)

        self.open = Mock(side_effect=fake)
        self.assertEqual(self.invoke(), 1)
        r = self.receipt()
        self.assertEqual(self.open.call_count, 2)
        entry = r["packages"][0]
        self.assertEqual(entry["partialSHA256"], hashlib.sha256(b'partial bytes').hexdigest())
        self.assertEqual(entry["retainedBytes"], len(b'partial bytes'))
        self.assertNotIn('synthetic-private-exception-content', json.dumps(r))

    def test_archive_limit_and_truncation_fail_before_publication(self):
        for i, length in enumerate((d.ARCHIVE_LIMIT + 1, 100)):
            self.archives = self.root / ("archives-" + str(i))
            self.receipts = self.root / ("receipts-" + str(i))

            def fake(req, timeout):
                if req.full_url == d.INDEX_URL:
                    return self.good_response(req, timeout)
                return Response(b'short', req.full_url, length=length)

            self.open = Mock(side_effect=fake)
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(len(list(self.archives.glob('*.nupkg'))), 0)
            self.assertEqual(self.open.call_count, 2)

    def test_final_file_collision_or_symlink_is_not_overwritten(self):
        for i, symlink in enumerate((False, True)):
            self.archives = self.root / ("archives-" + str(i))
            self.receipts = self.root / ("receipts-" + str(i))
            first = self.manifest["packages"][0]["fileName"]
            target = self.root / ("outside-" + str(i))
            target.write_bytes(b'preserve target')

            def fake(req, timeout):
                if req.full_url != d.INDEX_URL:
                    dest = self.archives / first
                    if symlink:
                        dest.symlink_to(target)
                    else:
                        dest.write_bytes(b'preserve collision')
                return self.good_response(req, timeout)

            self.open = Mock(side_effect=fake)
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.open.call_count, 2)
            self.assertEqual(target.read_bytes(), b'preserve target')
            self.assertEqual((self.archives / first).read_bytes(), b'preserve target' if symlink else b'preserve collision')

    def test_default_opener_preserves_tls_and_proxy_handlers(self):
        # Construction only; never open a socket. build_opener supplies its normal handlers.
        opener = d.urllib.request.build_opener(d.NoRedirect())
        self.assertTrue(any(isinstance(h, d.urllib.request.HTTPSHandler) for h in opener.handlers))
        self.assertTrue(any(isinstance(h, d.NoRedirect) for h in opener.handlers))
        # Proxy handler may be absent if the normal environment has no proxies.
        https = next(h for h in opener.handlers if isinstance(h, d.urllib.request.HTTPSHandler))
        self.assertIsNone(https._context)  # urllib will use its normal verified default context.


if __name__ == '__main__':
    unittest.main(verbosity=2)
