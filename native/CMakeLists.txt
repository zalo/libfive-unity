# Builds the native libfive plugin binary used by libfive-unity.
#
# This is a thin wrapper around a libfive source checkout (see LIBFIVE_SOURCE_DIR)
# that:
#   * builds only the kernel (no Studio, Guile, Python, or tests),
#   * folds libfive's standard library (libfive-stdlib) into the main shared
#     library so Unity only has to load a single binary per platform, and
#   * disables -march=native so the resulting binaries are redistributable.
#
# The GitHub Actions workflow in .github/workflows/native.yml drives this file;
# see native/build.sh and native/build.ps1 for the exact invocation.

cmake_minimum_required(VERSION 3.15)
project(libfive-unity LANGUAGES C CXX)

set(LIBFIVE_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/libfive"
    CACHE PATH "Path to a libfive source checkout")
if(NOT EXISTS "${LIBFIVE_SOURCE_DIR}/CMakeLists.txt")
  message(FATAL_ERROR
    "libfive sources not found at '${LIBFIVE_SOURCE_DIR}'. "
    "Clone https://github.com/libfive/libfive there or pass -DLIBFIVE_SOURCE_DIR=<path>.")
endif()

# Kernel only.
set(BUILD_STUDIO_APP OFF CACHE BOOL "" FORCE)
set(BUILD_GUILE_BINDINGS OFF CACHE BOOL "" FORCE)
set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "" FORCE)
set(BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(ENABLE_DEBUG OFF CACHE BOOL "" FORCE)

# libfive probes for -march=native with check_cxx_compiler_flag(); pre-seeding
# the result variable skips the probe and keeps the flag out of the build.
set(MARCH_SUPPORTED OFF CACHE BOOL "Disabled: binaries are redistributed" FORCE)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

add_subdirectory("${LIBFIVE_SOURCE_DIR}" libfive)

# libfive only adds Eigen's include directory from pkg-config (non-MSVC). With vcpkg on MSVC the
# Eigen3 CMake package is found but its include path is never applied, so add it here.
find_package(Eigen3 CONFIG QUIET)
if(Eigen3_FOUND)
  if(TARGET Eigen3::Eigen)
    get_target_property(_eigen_includes Eigen3::Eigen INTERFACE_INCLUDE_DIRECTORIES)
    target_include_directories(libfive SYSTEM PRIVATE ${_eigen_includes})
  elseif(EIGEN3_INCLUDE_DIRS)
    target_include_directories(libfive SYSTEM PRIVATE ${EIGEN3_INCLUDE_DIRS})
  endif()
endif()

# Fold the standard library (shapes, CSG, transforms, text) into libfive itself.
target_sources(libfive PRIVATE
  "${LIBFIVE_SOURCE_DIR}/libfive/stdlib/stdlib.cpp"
  "${LIBFIVE_SOURCE_DIR}/libfive/stdlib/stdlib_impl.cpp")
target_include_directories(libfive PRIVATE "${LIBFIVE_SOURCE_DIR}/libfive/stdlib")

# libfive-unity helpers (batched gradient evaluation for feature-based normals).
target_sources(libfive PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/shim/libfive_unity.cpp")
target_include_directories(libfive PRIVATE "${LIBFIVE_SOURCE_DIR}/libfive/include")

# The separate libfive-stdlib target is redundant now; don't build it.
if(TARGET libfive-stdlib)
  set_target_properties(libfive-stdlib PROPERTIES EXCLUDE_FROM_ALL TRUE)
endif()

# Self-contained binaries: no runtime dependency on a particular libstdc++.
if(UNIX AND NOT APPLE)
  target_link_options(libfive PRIVATE -static-libstdc++ -static-libgcc)
endif()
