# Mint AR Companion for PICO 4

Offline mixed-reality companion built with Unity `6000.3.23f1`, PICO Integration SDK `3.4.0`, URP and Android ARM64/IL2CPP.

Open `Assets/Scenes/CompanionMR.unity`. In the Editor the app falls back to the deterministic planner because the llama.cpp plugin is Android-only. On PICO 4 it uses the bundled Qwen3 GGUF, Russian voice recognition/synthesis, passthrough and Scene Capture.

Build from **AIGF > Build PICO 4 Release APK**. The output is `Builds/MintARCompanion.apk`. Git LFS is required for GGUF, ONNX and native `.so` files.

Release note: the bundled Russian Irina TTS voice has an upstream `Unknown` dataset license and must be replaced or legally cleared before commercial distribution. See `THIRD_PARTY_NOTICES.md`.

See:

- `Documentation/SETUP.md`
- `Documentation/ARCHITECTURE.md`
- `Documentation/PICO_BUILD.md`
- `Documentation/STATUS.md`
- `Documentation/PICO_DEVICE_TEST.md`
- `Native/LlamaBridge/README.md`
- `THIRD_PARTY_NOTICES.md`
