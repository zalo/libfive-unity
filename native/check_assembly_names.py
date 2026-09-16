#!/usr/bin/env python3
"""Fails if a managed assembly definition would compile to the same filename as a native plugin.

Unity refuses to load a project where an .asmdef's assembly (name.dll) shares its filename with a
native plugin ("Plugin '...libfive.dll' has the same filename as Assembly Definition File ..."),
so the managed assemblies must never be called 'libfive'. Feature branches carry no binaries, which
is why this is checked against the plugin name the workflow installs, not just files on disk.

    python3 native/check_assembly_names.py [repo root]
"""
import json
import pathlib
import sys

PLUGIN_NAME = "libfive"  # the native library, see install_plugin.py
NATIVE_SUFFIXES = {".dll", ".so", ".dylib", ".bundle"}


def main() -> int:
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else pathlib.Path(__file__).resolve().parents[1])
    assets = root / "Assets"
    reserved = {PLUGIN_NAME}
    for f in (assets / "libfive" / "Plugins").rglob("*"):
        if f.suffix.lower() in NATIVE_SUFFIXES:
            reserved.add(f.stem)

    failures = []
    asmdefs = sorted(assets.rglob("*.asmdef"))
    for asmdef in asmdefs:
        name = json.loads(asmdef.read_text(encoding="utf-8"))["name"]
        if name.lower() in {r.lower() for r in reserved}:
            failures.append(f"{asmdef.relative_to(root)}: assembly '{name}' collides with native plugin '{name}.dll'")

    if failures:
        print("Assembly definition / native plugin filename collision:", file=sys.stderr)
        for line in failures:
            print("  " + line, file=sys.stderr)
        return 1
    print(f"OK: {len(asmdefs)} assembly definitions, none named {sorted(reserved)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
