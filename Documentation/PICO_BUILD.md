# PICO 4 build path

## Current project settings

- Unity: `6000.3.23f1`
- Android scripting backend: IL2CPP
- target architecture: ARM64 only
- minimum Android API: 29
- package ID: `com.aigf.localcompanion`
- rendering: URP

## Required vendor setup

This repository does not currently contain PICO XR/OpenXR packages. Install a PICO-supported Unity 6 SDK/provider version from PICO's official distribution, then configure XR Plug-in Management and the PICO Android provider using that version's documentation.

Do not add guessed calls to `PicoRoomProvider`. Implement two small SDK-specific components instead:

1. `IPicoPassthroughBackend` for enabling/disabling passthrough.
2. `IPicoSceneSource` for querying captured semantic objects and returning `PicoSemanticObject` descriptors.

Assign those components to `PicoPassthroughAdapter` and `PicoRoomProvider`. The manual provider remains the fallback.

Scene Capture labels must be normalized to stable IDs. Seats require project-generated or SDK-derived `ApproachPoint`, `SitPoint`, and `SitRotation`. Floor and obstacle geometry must feed a runtime NavMesh adapter before free movement in a captured room is enabled.

## Native llama.cpp

1. Use the Android SDK/NDK/JDK installed with this Unity Editor.
2. Pin a llama.cpp commit.
3. Follow `Native/LlamaBridge/README.md` to configure CMake for `arm64-v8a` and Android API 29.
4. Copy `libgirlfriend_ai.so` to `Assets/Plugins/Android/arm64-v8a/`.
5. In the Unity plugin importer, enable Android/ARM64 and disable incompatible platforms.

The C ABI is limited to `gf_init`, `gf_generate`, `gf_cancel`, and `gf_shutdown`.

## Model packaging

Place the real GGUF at `Assets/StreamingAssets/Models/qwen3-1.7b-q4.gguf` before building. It is bundled into the application; there is no startup network fetch.

At first launch, `BundledModelInstaller` writes the asset to permanent private app files, verifies optional size/SHA-256, and records the installed manifest. Later launches reuse it. A manifest version/hash change causes a verified replacement.

Large GGUF assets significantly increase APK size and first-launch installation time. Validate available internal storage and packaging limits on the target deployment channel.

## Android checklist

1. Switch Build Profile to Android.
2. Confirm IL2CPP and ARM64.
3. Confirm the PICO XR loader/provider and required Android permissions.
4. Confirm passthrough and Scene Capture capabilities in the vendor manifest/settings.
5. Include `CompanionDemo` or the production XR scene in Build Profiles.
6. Confirm the real GGUF and ARM64 `.so` exist.
7. Build and install the APK.
8. Test model installation, inference latency, thermals, memory use, cancellation, passthrough, captured-room mapping, NavMesh boundaries, seating anchors, and personal-space behavior on PICO 4.

No PICO hardware validation has been performed in this repository.
