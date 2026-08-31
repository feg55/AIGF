# llama.cpp Android ARM64 bridge

This directory builds the small C ABI consumed by `LlamaCppLocalLLM`. Upstream llama.cpp types remain private to this bridge.

## Prerequisites

- Unity 6000.3 Android Build Support, including its Android SDK, NDK, OpenJDK, and CMake toolchain.
- The NDK selected in Unity **Preferences > External Tools**. Unity 6 installations commonly provide NDK r27; use the exact version supported by the installed Editor.
- A pinned llama.cpp checkout compatible with the API used here. Record the commit in your release notes; do not build production artifacts from an unpinned moving branch.

The bridge follows the current upstream C API (`llama_model_load_from_file`, `llama_init_from_model`, `llama_batch_get_one`, and sampler chains). If a newer pinned llama.cpp changes its ABI, update this single bridge rather than the Unity gameplay layer.

## Configure and build

From a terminal with `ANDROID_NDK` pointing at Unity's NDK:

```text
cmake -S Native/LlamaBridge -B Native/LlamaBridge/build-android \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=%ANDROID_NDK%/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_PLATFORM=android-29 \
  -DCMAKE_BUILD_TYPE=Release \
  -DLLAMA_CPP_DIR=C:/path/to/pinned/llama.cpp

cmake --build Native/LlamaBridge/build-android --config Release
```

Use shell-appropriate path syntax on macOS/Linux. Copy the resulting `libgirlfriend_ai.so` to:

`Assets/Plugins/Android/arm64-v8a/libgirlfriend_ai.so`

In Unity's plugin importer, enable Android, select ARM64, and disable Editor platforms unless you also build a matching desktop library.

## Runtime flow

1. Unity reads `Assets/StreamingAssets/Models/model-manifest.json`.
2. On Android, `BundledModelInstaller` verifies or installs the bundled GGUF once in permanent private app files.
3. `gf_init` receives that normal UTF-8 filesystem path and loads the model.
4. `gf_generate` creates a request context and performs bounded sampling on a worker task.
5. `gf_cancel` cooperatively aborts CPU decode when a newer command replaces the request.
6. `gf_shutdown` releases model and backend state.

CPU inference is the supported baseline. GPU offload is opt-in and must be profiled on the target PICO runtime. Generated build directories and upstream llama.cpp sources should not be committed here.
