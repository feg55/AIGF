# Editor setup

## Quick start

1. Open the project in Unity `6000.3.23f1`.
2. Wait for package import and script compilation.
3. Open `Assets/Scenes/CompanionDemo.unity`. If it was not generated automatically, run **AIGF > Create or Rebuild Companion Demo Scene**.
4. Press Play.
5. Use the left debug panel or its `Come here`, `Sit`, `Stand`, `Follow`, `Stop`, and `Wave` buttons.

Useful commands include `иди ко мне`, `сядь на диван`, `встань`, `следуй за мной`, `остановись`, `помаши`, `посмотри на меня`, and their English equivalents.

The demo scene contains a camera/HMD stand-in, placeholder capsule avatar, floor, baked NavMesh, manual `sofa_1`, approach/sit anchors, mock LLM/TTS, and diagnostics UI. Missing final animations are logged placeholders by design.

## Tests

Open **Window > General > Test Runner**, select **EditMode**, and run `Aigf.Companion.Tests`. Tests cover strict JSON parsing, malformed/unsupported actions, action limits, room targets/lookup, prompt content, bilingual mock commands, action order, model manifest validation, and Editor model paths.

## Configuration

The demo generator creates `Assets/Settings/CompanionAppConfig.asset`. Important defaults:

- `Use Mock LLM`: enabled
- social distance: `0.9 m`
- follow distance: `1.2 m`
- movement speed: `1.4 m/s`
- context: `2048`
- output: `128` tokens
- GPU offload: disabled

Keep CPU inference supported. Enable GPU offload only after profiling the exact PICO runtime.

## Local GGUF in Editor

1. Place `qwen3-1.7b-q4.gguf` under `Assets/StreamingAssets/Models/`.
2. Update `model-manifest.json` with exact byte size and SHA-256 for release builds.
3. Disable `Use Mock LLM`.
4. Build a compatible Windows native library if testing llama.cpp inside Windows Editor, or test the native path on Android. The Android `.so` does not load in Windows Editor.

If the GGUF or native library is absent, initialization reports the exact problem and uses `MockLocalLLM` when fallback is enabled.

## Replacing the placeholder avatar

Place the imported humanoid under the avatar root, assign its `Animator` to `GirlAnimator`, and configure centralized parameters (`Walking`, `Sit`, `Stand`, `Wave`, optional `Emotion`). Assign a humanoid head bone or let `LookAtUser` find it. Add only approved animation IDs to `safeNamedAnimations`.

Keep the `NavMeshAgent` and interaction scripts on the root. Adjust sit anchors for the final avatar's pelvis/root convention.
