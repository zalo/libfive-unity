# Builds the libfive Unity plugin binary (libfive.dll) on Windows with MSVC + vcpkg.
#
#   native\build.ps1 [-Triplet x64-windows-static] [-Config Release]
#
# Run from a "Developer PowerShell for VS" (or after ilammy/msvc-dev-cmd in CI) so that cl.exe and
# ninja are on PATH; the Ninja generator is used when available (this is what lets a compiler
# launcher such as sccache work), otherwise the default Visual Studio generator.
#
# Environment:
#   LIBFIVE_SOURCE_DIR  libfive checkout (default: native\libfive)
#   VCPKG_ROOT          vcpkg checkout (default: $env:VCPKG_INSTALLATION_ROOT, then C:\vcpkg)
#   BUILD_DIR           build tree (default: native\build\<triplet>)
#   CMAKE_CXX_COMPILER_LAUNCHER / CMAKE_C_COMPILER_LAUNCHER  optional, e.g. sccache
#
# Output: $BUILD_DIR\libfive.dll (copied from the generator-specific location)
param(
  [string]$Triplet = "x64-windows-static",
  [string]$Config = "Release"
)
$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Src = if ($env:LIBFIVE_SOURCE_DIR) { $env:LIBFIVE_SOURCE_DIR } else { Join-Path $Here "libfive" }
$Vcpkg = if ($env:VCPKG_ROOT) { $env:VCPKG_ROOT } elseif ($env:VCPKG_INSTALLATION_ROOT) { $env:VCPKG_INSTALLATION_ROOT } else { "C:\vcpkg" }
$Build = if ($env:BUILD_DIR) { $env:BUILD_DIR } else { Join-Path $Here "build\$Triplet" }

if (-not (Test-Path (Join-Path $Src "CMakeLists.txt"))) {
  throw "libfive sources not found at $Src (clone https://github.com/libfive/libfive there)"
}

# libfive hard-codes MSVC flags that are wrong for a redistributable plugin. Patch them:
#   /MD  -> /MT        static CRT, so users don't need a matching VC++ redistributable
#   /arch:AVX2 removed so the DLL runs on any x64 CPU
#   /WX removed        newer compilers' warnings must not fail the build
$cm = Join-Path $Src "CMakeLists.txt"
$text = Get-Content $cm -Raw
$text = $text -replace '/MD/', '/MT' -replace '/MDd', '/MTd' -replace ' /arch:AVX2', '' -replace '/WX ', ''
Set-Content $cm $text -NoNewline

$useNinja = (Get-Command ninja -ErrorAction SilentlyContinue) -and (Get-Command cl -ErrorAction SilentlyContinue)
$generatorArgs = if ($useNinja) { @("-G", "Ninja", "-DCMAKE_BUILD_TYPE=$Config") } else { @("-A", "x64") }
Write-Host "Generator: $(if ($useNinja) { 'Ninja' } else { 'Visual Studio (default)' })"

# Every -D argument is quoted as a whole: PowerShell does not expand variables inside an
# unquoted token that starts with "-".
cmake -S "$Here" -B "$Build" @generatorArgs `
  "-DLIBFIVE_SOURCE_DIR=$Src" `
  "-DCMAKE_TOOLCHAIN_FILE=$Vcpkg\scripts\buildsystems\vcpkg.cmake" `
  "-DVCPKG_TARGET_TRIPLET=$Triplet" `
  "-DVCPKG_OVERLAY_TRIPLETS=$Here\triplets" `
  "-DCMAKE_POLICY_DEFAULT_CMP0091=NEW" `
  '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
cmake --build "$Build" --config $Config --target libfive --parallel
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Get-ChildItem -Path (Join-Path $Build "libfive\libfive\src") -Filter libfive.dll -Recurse | Select-Object -First 1
if (-not $dll) { throw "libfive.dll was not produced" }
Copy-Item $dll.FullName (Join-Path $Build "libfive.dll") -Force
Get-ChildItem (Join-Path $Build "libfive.dll")
