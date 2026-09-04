# PICO 4 release build

## Release configuration

- Unity `6000.3.23f1`
- PICO Unity Integration SDK `3.4.0`
- Android API 29, ARM64, IL2CPP, Release
- package ID `com.aigf.mintcompanion`
- OpenGLES3, Linear color space, URP, Multiview
- passthrough, hand tracking, and Scene Capture enabled
- production scene: `Assets/Scenes/CompanionMR.unity`

## Build

1. In Unity Hub, confirm Android Build Support, SDK/NDK, and OpenJDK for Unity `6000.3.23f1`, then sign in and confirm that the Editor has a valid entitlement.
2. Run `git lfs pull` and open the project once so Unity finishes importing large assets.
3. Run **AIGF > Validate PICO Release**. Fix every reported missing asset before building.
4. Run the EditMode and PlayMode tests.
5. Run **AIGF > Build PICO 4 Release APK**.

The output is `Builds/MintARCompanion.apk`. The build tool regenerates the Sherpa StreamingAssets manifest, uses a clean Release build, and validates the exact Qwen size/SHA-256, llama.cpp ARM64 bridge, Sherpa managed/native bindings, active Supertonic profile, speech models, Mint prefab, and production scene.

## First device launch

1. Allow microphone, spatial sensing/room access, and passthrough permissions when PICO requests them.
2. If no captured room exists, complete **Settings > Boundary > Room Capture** and restart the app.
3. Wait while the bundled GGUF is copied to private app storage and verified. Keep roughly 2 GB of free storage for installation and first-launch extraction.
4. Open the in-app diagnostics menu and confirm `sofas: 1` (or more) and `seats: 1` after marking a sofa. On a regular PICO 4, OS 5.13.x uses the SDK's legacy Scene Capture path; both its capture-completed event and the newer scene-data event trigger a room reload. If counts stay at zero, recapture the room and verify the spatial-data permission.
5. Confirm that walls, floor, doors, windows, tables, sofas, and beds align with the physical room before enabling free movement.

The application performs no model download at runtime. If local inference cannot initialize, the diagnostic panel reports the error and the safe mock responder remains available.

## Native versions

- Qwen model: `Qwen/Qwen3-0.6B-GGUF`, Q8_0, pinned revision from `model-manifest.json`.
- llama.cpp: tag `v0.1.2`, commit `1511ce3bc3f087376c8526b4ad07100bfabb277f`.
- sherpa-onnx native libraries: `1.13.5` for Android ARM64.
- offline TTS: Supertonic 3 INT8, Russian, bright F2 female voice (`sid=1`), 5 generation steps.

Run the full checklist in `Documentation/PICO_DEVICE_TEST.md` on a physical PICO 4 before distribution.
