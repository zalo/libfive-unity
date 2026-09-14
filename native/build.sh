#!/usr/bin/env bash
# Builds the libfive Unity plugin binary for macOS or Linux.
#
#   native/build.sh <vcpkg-triplet> [extra cmake args...]
#     triplets: x64-linux | arm64-osx | x64-osx
#
# Environment:
#   LIBFIVE_SOURCE_DIR  libfive checkout (default: native/libfive)
#   VCPKG_ROOT          vcpkg checkout (default: $VCPKG_INSTALLATION_ROOT, then ~/vcpkg)
#   BUILD_DIR           build tree (default: native/build/<triplet>)
#
# Output: $BUILD_DIR/libfive/libfive/src/libfive.{so,dylib}
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TRIPLET="${1:?usage: build.sh <triplet> [cmake args]}"; shift || true
SRC="${LIBFIVE_SOURCE_DIR:-$HERE/libfive}"
VCPKG="${VCPKG_ROOT:-${VCPKG_INSTALLATION_ROOT:-$HOME/vcpkg}}"
BUILD="${BUILD_DIR:-$HERE/build/$TRIPLET}"

if [ ! -f "$SRC/CMakeLists.txt" ]; then
  echo "libfive sources not found at $SRC (clone https://github.com/libfive/libfive there)" >&2
  exit 1
fi

# libfive finds Eigen through pkg-config on non-MSVC platforms; point it at vcpkg's .pc files.
INSTALLED="$BUILD/vcpkg_installed/$TRIPLET"
export PKG_CONFIG_PATH="$INSTALLED/lib/pkgconfig:$INSTALLED/share/pkgconfig:${PKG_CONFIG_PATH:-}"

cmake -S "$HERE" -B "$BUILD" \
  -DCMAKE_BUILD_TYPE=Release \
  -DLIBFIVE_SOURCE_DIR="$SRC" \
  -DCMAKE_TOOLCHAIN_FILE="$VCPKG/scripts/buildsystems/vcpkg.cmake" \
  -DVCPKG_TARGET_TRIPLET="$TRIPLET" \
  -DVCPKG_OVERLAY_TRIPLETS="$HERE/triplets" \
  "$@"
cmake --build "$BUILD" --target libfive --parallel
ls -la "$BUILD"/libfive/libfive/src/libfive.*
