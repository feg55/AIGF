# Native plugin destination

`libgirlfriend_ai.so` is the Android ARM64 llama.cpp bridge used by `LlamaCppLocalLLM`.

It is built from llama.cpp `v0.1.2` by `Native/LlamaBridge/CMakeLists.txt`. Rebuild it after changing the bridge or the pinned upstream version, then replace the binary and verify the Android/ARM64 PluginImporter settings.

Do not place x86, x86_64, or armeabi-v7a binaries in this directory.
