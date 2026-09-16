#!/usr/bin/env python3
"""Copies a built libfive binary into the Unity project with the matching .meta files.

    native/install_plugin.py --platform windows-x64 --binary path/to/libfive.dll [--repo .]
    native/install_plugin.py --platform macos-universal --binary path/to/libfive.dylib
    native/install_plugin.py --platform linux-x64 --binary path/to/libfive.so

The .meta files carry fixed GUIDs and per-platform PluginImporter settings so Unity picks the
right binary for the editor and for each standalone player without any manual configuration.
"""
import argparse
import os
import shutil
import sys

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

PLUGIN_META = """fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: {exclude_editor}
        Exclude Linux64: {exclude_linux}
        Exclude OSXUniversal: {exclude_osx}
        Exclude Win: 1
        Exclude Win64: {exclude_win}
  - first:
      Any: 
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: {editor_cpu}
        DefaultValueInitialized: true
        OS: {editor_os}
  - first:
      Standalone: Linux64
    second:
      enabled: {linux_enabled}
      settings:
        CPU: {linux_cpu}
  - first:
      Standalone: OSXUniversal
    second:
      enabled: {osx_enabled}
      settings:
        CPU: {osx_cpu}
  - first:
      Standalone: Win
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: Win64
    second:
      enabled: {win_enabled}
      settings:
        CPU: {win_cpu}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

PLATFORMS = {
    "windows-x64": dict(
        path="Assets/libfive/Plugins/Windows/x86_64/libfive.dll",
        folders={"Assets/libfive/Plugins/Windows": "5a1c0f7d9c8e4b2a8f3d6e1b7c9a2d41",
                 "Assets/libfive/Plugins/Windows/x86_64": "6b2d1e8ead9f4c3b9a4e7f2c8d0b3e52"},
        guid="7c3e2f9fbea05d4cab5f803d9e1c4f63",
        meta=dict(exclude_editor=0, exclude_linux=1, exclude_osx=1, exclude_win=0,
                  editor_cpu="x86_64", editor_os="Windows",
                  linux_enabled=0, linux_cpu="None", osx_enabled=0, osx_cpu="None",
                  win_enabled=1, win_cpu="x86_64")),
    "macos-universal": dict(
        path="Assets/libfive/Plugins/macOS/libfive.dylib",
        folders={"Assets/libfive/Plugins/macOS": "8d4f3a0acfb16e5dbc6a914eaf2d5a74"},
        guid="9e5a4b1bd0c27f6ecd7ba25fb03e6b85",
        meta=dict(exclude_editor=0, exclude_linux=1, exclude_osx=0, exclude_win=1,
                  editor_cpu="AnyCPU", editor_os="OSX",
                  linux_enabled=0, linux_cpu="None", osx_enabled=1, osx_cpu="AnyCPU",
                  win_enabled=0, win_cpu="None")),
    "linux-x64": dict(
        path="Assets/libfive/Plugins/Linux/x86_64/libfive.so",
        folders={"Assets/libfive/Plugins/Linux": "af6b5c2ce1d380fadec8b36ac14f7c96",
                 "Assets/libfive/Plugins/Linux/x86_64": "b07c6d3df2e491abefd9c47bd25a8da7"},
        guid="c18d7e4e03f5a2bcf0ead58ce36b9eb8",
        meta=dict(exclude_editor=0, exclude_linux=0, exclude_osx=1, exclude_win=1,
                  editor_cpu="x86_64", editor_os="Linux",
                  linux_enabled=1, linux_cpu="x86_64", osx_enabled=0, osx_cpu="None",
                  win_enabled=0, win_cpu="None")),
}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--platform", required=True, choices=sorted(PLATFORMS))
    ap.add_argument("--binary", required=True, help="built libfive shared library")
    ap.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."),
                    help="libfive-unity checkout (default: parent of this script)")
    args = ap.parse_args()

    if not os.path.isfile(args.binary):
        sys.exit("binary not found: " + args.binary)
    spec = PLATFORMS[args.platform]
    repo = os.path.abspath(args.repo)

    for folder, guid in spec["folders"].items():
        full = os.path.join(repo, folder)
        os.makedirs(full, exist_ok=True)
        with open(full + ".meta", "w", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid))

    dest = os.path.join(repo, spec["path"])
    shutil.copyfile(args.binary, dest)
    with open(dest + ".meta", "w", newline="\n") as f:
        f.write(PLUGIN_META.format(guid=spec["guid"], **spec["meta"]))
    print("installed", args.binary, "->", os.path.relpath(dest, repo), "(%d bytes)" % os.path.getsize(dest))


if __name__ == "__main__":
    main()
