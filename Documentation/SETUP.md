# Editor setup

## Prerequisites

- Unity `6000.3.23f1` with Android Build Support, SDK/NDK, OpenJDK, and a valid Unity license.
- Git LFS installed before cloning or pulling the repository.
- PICO 4 room data captured with **Settings > Boundary > Room Capture**.

`ForAI/` is private source material and is ignored. Runtime-ready derivatives live in `Assets/Avatars/Mint`.

## Open the application

1. Run `git lfs pull`.
2. Open the repository in Unity Hub and wait for package import.
3. Open `Assets/Scenes/CompanionMR.unity`.
4. Run **AIGF > Validate PICO Release**.
5. Press Play for the Editor fallback room, or use **AIGF > Build PICO 4 Release APK**.

The production scene contains the PICO XR origin, passthrough, Scene Capture room provider, runtime NavMesh, Mint avatar, local Qwen model, offline speech input/output, and camera-attached diagnostics UI.

## Configuration

`Assets/Settings/CompanionAppConfig.asset` defaults to:

- local model: `qwen3-0.6b-q8_0.gguf`;
- context: 2048 tokens;
- output: 128 tokens;
- CPU inference with mock fallback on initialization failure;
- social distance: 0.9 m;
- follow distance: 1.2 m;
- movement speed: 1.4 m/s.

The mock fallback is intentional: it keeps movement and UI diagnosable if an Android native library or model fails. Release validation still requires all production artifacts.

## Editor limitations

The Android llama.cpp library cannot load in the Windows Editor. PICO passthrough and Scene Capture are also device-only. Editor Play Mode therefore uses the deterministic room fallback and may fall back to the mock LLM. Production inference and speech are tested in the Android build.

## Tests

In **Window > General > Test Runner**, run both EditMode and PlayMode suites. The existing acceptance test loads `CompanionMR` and exercises movement, seating, following, stopping, waving, and look-at behavior.

If Unity reports `No valid Unity Editor license found`, refresh or activate Unity Personal in Unity Hub, keep Hub signed in, reopen the project, and repeat the validation/tests.
