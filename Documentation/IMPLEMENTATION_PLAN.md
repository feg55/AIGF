# Implementation Plan

## Repository findings

- Unity Editor: `6000.3.23f1` (the repository does not use Unity 2022.3).
- Render pipeline: URP `17.3.0`.
- Navigation: AI Navigation `2.0.14` is installed.
- Input/UI: Input System `1.20.0` and uGUI `2.0.0` are installed.
- Android is configured for IL2CPP and ARM64; the current minimum API level is 25.
- No PICO SDK, OpenXR provider, XR Plug-in Management package, native inference plugin, avatar, product scripts, tests, or project documentation are present.
- `SampleScene` is the untouched URP template and has no baked NavMesh.
- There is no Git repository metadata in the supplied directory.

## Implementation sequence

1. Add a dependency-light runtime assembly and central `AppConfig`.
2. Add serializable agent contracts, strict JSON parsing, action whitelist, reply limits, and target validation.
3. Add normalized `RoomGraph`, manual room nodes/interactions, and a PICO adapter boundary that compiles without a PICO SDK.
4. Add cancellable NavMesh navigation, animation wrapper, sitting alignment, look-at, and amplitude lip sync.
5. Add deterministic sequential `ActionExecutor`, explicit state, safety validation, and observable action results.
6. Add bilingual `MockLocalLLM`, compact prompt builder, `GirlBrain`, mock TTS, and debug UI.
7. Add an Editor demo builder that creates a test scene with floor, player camera, placeholder avatar, sofa interaction points, runtime NavMesh surface, and debug UI.
8. Add local memory and mock voice pipeline interfaces without making voice a blocker.
9. Add project-local model manifest/path resolution, one-time Android installation into permanent private files, native C# bridge, and mock fallback.
10. Add the small C ABI and CMake scaffold around llama.cpp for Android ARM64.
11. Add EditMode tests for JSON safety, commands, room lookup, prompts, manifests, paths, and sequential action validation.
12. Compile/test, repair errors, audit model paths and prohibited dispatch mechanisms, and document the exact Editor/PICO setup and remaining hardware work.

## Deliberate boundaries

- PICO Scene Capture and passthrough remain adapter-driven until a real PICO SDK is installed. No guessed SDK class names will be committed.
- The native `.so` and GGUF are external build/content artifacts. The required locations and validation behavior are implemented, but generated binaries are not fabricated.
- The Editor vertical slice uses a placeholder capsule avatar and generated room geometry, so it does not depend on final art, XR hardware, microphone, or native inference.
- Dynamic on-device NavMesh generation from PICO room data remains device-dependent; the Editor demo builds from a `NavMeshSurface` at runtime.

## Verification result

- Unity import and scene generation: passed on `6000.3.23f1`.
- EditMode tests: 16 passed, 0 failed.
- PlayMode vertical-slice test: 1 passed, 0 failed.
- PICO hardware/native GGUF inference: not run; the required SDK, GGUF, and ARM64 `.so` are not included in this source snapshot.
