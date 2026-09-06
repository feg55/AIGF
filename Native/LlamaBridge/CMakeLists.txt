cmake_minimum_required(VERSION 3.22.1)
project(girlfriend_ai LANGUAGES C CXX)

if(NOT ANDROID)
    message(FATAL_ERROR "This bridge target is configured for Android builds.")
endif()

if(NOT ANDROID_ABI STREQUAL "arm64-v8a")
    message(FATAL_ERROR "girlfriend_ai supports only Android arm64-v8a.")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(LLAMA_CPP_DIR "" CACHE PATH "Path to a pinned llama.cpp source checkout")
if(NOT EXISTS "${LLAMA_CPP_DIR}/CMakeLists.txt")
    message(FATAL_ERROR "Set LLAMA_CPP_DIR to a pinned llama.cpp checkout.")
endif()

set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_COMMON OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(LLAMA_BUILD_SERVER OFF CACHE BOOL "" FORCE)
set(LLAMA_CURL OFF CACHE BOOL "" FORCE)
set(GGML_OPENMP OFF CACHE BOOL "" FORCE)
set(GGML_NATIVE OFF CACHE BOOL "" FORCE)

add_subdirectory("${LLAMA_CPP_DIR}" "${CMAKE_BINARY_DIR}/llama.cpp")

add_library(girlfriend_ai SHARED girlfriend_ai.cpp)
target_include_directories(girlfriend_ai PRIVATE
    "${LLAMA_CPP_DIR}/include"
    "${LLAMA_CPP_DIR}/ggml/include")
# The matching NDK libc++_shared.so must also be packaged in Assets/Plugins/
# Android/arm64-v8a. Unity IL2CPP does not automatically bundle this dependency.
target_link_libraries(girlfriend_ai PRIVATE llama log android c++_shared)
target_compile_options(girlfriend_ai PRIVATE -fvisibility=hidden -ffunction-sections -fdata-sections)
target_link_options(girlfriend_ai PRIVATE -Wl,--gc-sections -Wl,-z,max-page-size=16384)
