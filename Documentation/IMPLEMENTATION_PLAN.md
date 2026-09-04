# Execution plan and result

## 1. Repository and release baseline — completed

- Preserved the user's existing modified Unity settings.
- Ignored the private `ForAI/` source folder.
- Added Git LFS rules for GGUF, ONNX, and Android native libraries.
- Standardized Unity/PICO/Android settings and the production scene.

Why: reproducible source control and a single release path must exist before device features can be trusted.

## 2. PICO mixed reality — completed, device validation pending

- Installed PICO Unity Integration SDK 3.4.0 and XR dependencies.
- Added passthrough, tracked XR origin, Scene Capture semantic mapping, room colliders, interaction anchors, and runtime NavMesh rebuild.
- Kept generated room geometry strictly Editor-only.

Why: the character must understand and safely navigate the user's actual room instead of a staged demo.

## 3. Mint avatar — completed, content-quality caveat

- Converted the supplied source into a normalized humanoid runtime FBX.
- Removed unused scene data/bones, generated three LODs, configured mobile textures/materials, and built the prefab.
- Added procedural locomotion, sitting, waving, breathing, attention, blink, emotion, and lip-sync fallbacks.

Why: the original free asset was not mobile-XR ready and did not contain the facial shapes or authored clip set expected by the simulator.

## 4. Local intelligence and memory — completed, device profiling pending

- Bundled pinned Qwen3-0.6B Q8_0 with integrity manifest.
- Built a pinned llama.cpp ARM64 C ABI and added safe, cancellable inference.
- Added Qwen prompt formatting, bounded dialogue, and private local memory.

Why: PICO 4 needs a small deterministic offline baseline; the strict action boundary prevents model text from directly controlling Unity.

## 5. Offline Russian voice — completed, device validation pending

- Added microphone capture, Silero VAD, Whisper tiny ASR, Russian Supertonic 3 INT8 TTS, and Sherpa ONNX Android ARM64 bindings.
- Connected synthesized audio to lip sync and retained mocks for diagnostics.

Why: hands-free local conversation is a core AR use case and must not depend on a network service.

## 6. UX, safety, build, and acceptance — implementation completed

- Added camera-attached world-space diagnostics, safe action execution, personal-space navigation, release asset validation, and automated Android build entry points.
- Added setup, architecture, build, licensing, attribution, and physical-device checklists.

Remaining acceptance steps:

1. restore a valid Unity Editor entitlement for the installed Unity `6000.3.23f1`;
2. reimport and rerun EditMode/PlayMode after the final large-asset pass;
3. build the APK;
4. complete the PICO 4 hardware checklist and tune performance/anchors from measurements.

The first item is an external licensing blocker, not an unfinished code path.
