#!/usr/bin/env python3
"""Run an approved offline dispatch diagnostic against the retained exact graph."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ASSETS_SHA = "432f6c7796b7302a655eb65d0e3baa1bb1cb7c656c9fc05ec8ff16b7a8874a1f"
LOCK_SHA = "2d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f"
SDK = "10.0.401"
RUNTIME = "10.0.12"
GUARDS_SHA = "65d8256ed7f7d4d127838c89565efeeb6c2c6c864860108e64517e40db4b5af9"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_regular(path):
    if path.is_symlink() or not path.is_file():
        raise ValueError("Expected regular file: " + path.name)
    return path.read_bytes()


def run(args):
    guard_path = HERE.parent / "HttpsKeyProviders/verify-signatures.py"
    if sha(read_regular(guard_path)) != GUARDS_SHA:
        raise ValueError("Shared guard source binding mismatch")
    spec = importlib.util.spec_from_file_location(
        "signature_guards", guard_path)
    guards = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(guards)
    guards.environment_check(os.environ)
    manifest = guards.load_manifest()
    assets_raw, lock_raw = read_regular(args.assets), read_regular(args.lock)
    if sha(assets_raw) != ASSETS_SHA or sha(lock_raw) != LOCK_SHA:
        raise ValueError("Retained actual graph binding mismatch")
    assets = json.loads(assets_raw, object_pairs_hook=guards.closed_pairs)
    lock = json.loads(lock_raw, object_pairs_hook=guards.closed_pairs)
    packages = {p["id"] + "/" + p["version"]: p for p in manifest["packages"]}
    target = assets["targets"]["net10.0"]
    if {k for k, v in target.items() if v["type"] == "package"} != set(packages):
        raise ValueError("Actual asset graph differs from manifest37")
    if {k + "/" + v["resolved"] for k, v in lock["dependencies"]["net10.0"].items()
            if v["type"] != "Project"} != set(packages):
        raise ValueError("Actual lock differs from manifest37")
    if args.packages.is_symlink() or not args.packages.is_dir():
        raise ValueError("Expected existing non-symlink package root")
    receipts, dlls = [], []
    # No process is launched until every archive and selected compile/runtime DLL matches.
    for key, p in packages.items():
        relative = assets["libraries"][key]["path"]
        if relative != p["id"].lower() + "/" + p["version"]:
            raise ValueError("Unexpected actual package path")
        directory = args.packages / relative
        archive = read_regular(directory / p["fileName"])
        if sha(archive) != p["archiveSHA256"]:
            raise ValueError("Archive hash mismatch: " + p["fileName"])
        item = {**p, "selectedDLLs": []}
        with zipfile.ZipFile(directory / p["fileName"]) as z:
            for kind in ("compile", "runtime"):
                for entry in target[key].get(kind, {}):
                    if not entry.endswith(".dll"):
                        continue
                    if entry.startswith("/") or ".." in Path(entry).parts:
                        raise ValueError("Unsafe selected DLL entry")
                    if z.namelist().count(entry) != 1:
                        raise ValueError("Missing/duplicate raw DLL entry")
                    data = read_regular(directory / entry)
                    if data != z.read(entry):
                        raise ValueError("Expanded DLL differs from raw archive entry")
                    item["selectedDLLs"].append({"kind": kind, "entry": entry,
                                                 "sha256": sha(data)})
                    dlls.append((kind, Path(entry).name, data))
        receipts.append(item)
    if (not args.dotnet.is_absolute() or args.dotnet.is_symlink()
            or args.dotnet.name != "dotnet" or not os.access(args.dotnet, os.X_OK)):
        raise ValueError("Expected existing native absolute dotnet executable")
    if (args.output.exists() or args.output.is_symlink()
            or not args.output.is_absolute() or not args.output.parent.is_dir()
            or not str(args.output.resolve()).startswith("/private/tmp/")):
        raise ValueError("Scratch output must be new under /tmp")
    source = read_regular(HERE / "Program.cs")
    args.output.mkdir()
    output = args.output.resolve()
    receipt = {"status": "FAILED", "meaning": "Synthetic dispatch only, not production acceptance",
               "assetsSHA256": ASSETS_SHA, "lockSHA256": LOCK_SHA,
               "manifestSHA256": guards.MANIFEST_SHA256, "sourceSHA256": sha(source),
               "guardSourceSHA256": GUARDS_SHA,
               "runnerSHA256": sha(read_regular(Path(__file__))),
               "sdkExecutableSHA256": sha(read_regular(args.dotnet)),
               "packages": receipts, "commands": []}
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.parent),
               DOTNET_GENERATE_ASPNET_CERTIFICATE="false", MSBUILDDISABLENODEREUSE="1")

    def invoke(label, argv):
        entry, stdout = guards.capture(argv, output, env, label, subprocess.run)
        receipt["commands"].append(entry)
        if entry["exitCode"] != 0:
            raise ValueError("Diagnostic command failed: " + label)
        return stdout.decode("utf-8", errors="strict")

    try:
        (output / "global.json").write_text(json.dumps({"sdk": {"version": SDK,
                                                               "rollForward": "disable"}}))
        if invoke("sdk", [str(args.dotnet), "--version"]).strip() != SDK:
            raise ValueError("Pinned SDK mismatch")
        runtimes = invoke("runtimes", [str(args.dotnet), "--list-runtimes"])
        for framework in ("Microsoft.NETCore.App", "Microsoft.AspNetCore.App"):
            if not any(line.startswith(framework + " " + RUNTIME + " [")
                       for line in runtimes.splitlines()):
                raise ValueError("Pinned runtime missing")
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        properties = ET.SubElement(project, "PropertyGroup")
        for name, value in {"OutputType": "Exe", "TargetFramework": "net10.0",
                            "Nullable": "enable", "ImplicitUsings": "enable",
                            "TreatWarningsAsErrors": "true", "RuntimeFrameworkVersion": RUNTIME,
                            "RollForward": "Disable"}.items():
            ET.SubElement(properties, name).text = value
        group = ET.SubElement(project, "ItemGroup")
        ET.SubElement(group, "FrameworkReference", Include="Microsoft.AspNetCore.App")
        names = {"compile": set(), "runtime": set()}
        for kind, name, data in dlls:
            if name in names[kind]:
                raise ValueError("Duplicate selected DLL name")
            names[kind].add(name)
            folder = output / kind
            folder.mkdir(exist_ok=True)
            (folder / name).write_bytes(data)
            if kind == "compile":
                ref = ET.SubElement(group, "Reference", Include=Path(name).stem)
                ET.SubElement(ref, "HintPath").text = str(folder / name)
                ET.SubElement(ref, "Private").text = "false"
        ET.indent(project)
        ET.ElementTree(project).write(output / "Dispatch.csproj", encoding="unicode")
        (output / "Program.cs").write_bytes(source)
        (output / "NuGet.Config").write_text(
            '<configuration><packageSources><clear /></packageSources>'
            '<auditSources><clear /></auditSources></configuration>\n')
        invoke("restore", [str(args.dotnet), "restore", "Dispatch.csproj",
                           "--configfile", "NuGet.Config"])
        invoke("build", [str(args.dotnet), "build", "Dispatch.csproj", "-c", "Release",
                         "--no-restore", "-m:1", "-nr:false", "/p:UseSharedCompilation=false"])
        invoke("format", [str(args.dotnet), "format", "Dispatch.csproj", "--no-restore",
                          "--verify-no-changes"])
        binary = output / "bin/Release/net10.0"
        for path in (output / "runtime").iterdir():
            shutil.copyfile(path, binary / path.name)
        for kind, name, data in dlls:
            if read_regular(output / kind / name) != data:
                raise ValueError("Selected DLL changed before loading")
            if kind == "runtime" and read_regular(binary / name) != data:
                raise ValueError("Runtime DLL copy mismatch")
        if read_regular(output / "Program.cs") != source:
            raise ValueError("Harness source changed")
        invoke("cases", [str(args.dotnet), str(binary / "Dispatch.dll")])
        if read_regular(HERE / "Program.cs") != source:
            raise ValueError("Repository harness source changed during run")
        # Recheck retained archive bytes after execution, not just before compilation.
        for key, p in packages.items():
            archive = args.packages / assets["libraries"][key]["path"] / p["fileName"]
            if sha(read_regular(archive)) != p["archiveSHA256"]:
                raise ValueError("Archive changed during run")
        receipt["status"] = "PASS"
        print("DISP01 PASS archives=37 selectedDLLentries=" + str(len(dlls)))
        print("DISP08 PASS scratch-only build/format/cases")
    finally:
        (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--lock", type=Path, required=True)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    try:
        run(parser.parse_args())
    except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
        print("Diagnostic denied: " + type(error).__name__, file=sys.stderr)
        sys.exit(1)
