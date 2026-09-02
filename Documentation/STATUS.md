# Status

## Implemented

- production PICO MR scene with XR origin, tracked camera, passthrough, Scene Capture, semantic room mapping, and runtime NavMesh;
- Mint mobile avatar derived from the supplied source, normalized to 1.63 m, reduced from 488 to 227 used bones, with three LODs and Android texture settings;
- safe animation facade for walking, sitting, standing, waving, breathing, emotion, look-at, blink, and amplitude lip sync;
- local `Qwen3-0.6B` Q8_0 model with exact size/SHA-256 manifest and Qwen chat template;
- pinned llama.cpp Android ARM64 bridge with cancellation, bounded generation, and UTF-8 output;
- offline Sherpa ONNX microphone/VAD/Whisper ASR/Russian VITS TTS path with managed and Android ARM64 native bindings;
- strict JSON action contract, semantic target validation, cancellable execution, personal-space controls, and local bounded memory;
- release validator/builder, Git LFS rules, attribution, setup/build/device-test documentation.

## Verified before the final asset pass

- Unity imported and compiled the core runtime, Editor tooling, PICO integration, and tests without C# errors.
- EditMode: 16/16 passed.
- PlayMode: 1/1 acceptance scenario passed.
- `libgirlfriend_ai.so` built successfully for AArch64 and has the expected Android dependencies.
- Qwen and speech model files were downloaded from their official upstream releases and validated locally.
- After the final pass, Roslyn compilation passed for runtime, Editor build tooling, test assemblies, the `SHERPA_ONNX` package path, and the Android-only PICO Scene Capture branch. All bundled native libraries report AArch64 ELF headers.

## Pending verification

- Reimport and rerun the test suites after the final Sherpa/native/model additions.
- Produce `Builds/MintARCompanion.apk`.
- Run the physical PICO 4 checklist for permissions, room alignment, microphone, Russian speech, latency, thermals, RAM, and long-session stability.

These steps are currently blocked because command-line Unity exits with `No valid Unity Editor license found`. Android Build Support, SDK/NDK, and OpenJDK are already installed for the pinned Unity `6000.3.23f1`. Refresh/activate Unity Personal in Unity Hub and reopen the project; no source-code change can replace that entitlement.

## Known content limitation

The supplied free character contains no usable facial blend shapes or authored production animation clips. The application provides procedural motion and safe Animator hooks, but final commercial-quality facial expression and locomotion require compatible authored assets. The source model also depicts third-party character IP; review distribution rights before release.

The old `Assets/Scenes/CompanionDemo.unity` remains in the repository because it had pre-existing uncommitted changes. It is disabled in Build Settings; `CompanionMR` is the only release scene.
