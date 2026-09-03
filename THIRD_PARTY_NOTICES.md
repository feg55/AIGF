# Third-party notices

## PICO Unity Integration SDK

The project uses PICO Unity Integration SDK 3.4.0. The package remains subject
to the copyright and SDK terms distributed by PICO Technology Co., Ltd.; it is
not relicensed by this project.

## Mint Swimsuit Animated- Neverness To Everness

This work is based on
[`Mint Swimsuit Animated- Neverness To Everness`](https://sketchfab.com/3d-models/mint-swimsuit-animated-neverness-to-everness-6dbe3e3dc6704a36bb3acef80b0bc5e4)
by [Jun Hungry](https://sketchfab.com/JunHungry), licensed under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

The runtime FBX is a modified derivative prepared for mobile XR: unused scene
objects and bones are removed, the scale is normalized, and LOD meshes are
generated. The original license and attribution remain applicable.

The model depicts a character associated with a third-party game property.
Before commercial distribution, independently verify that the uploader had the
right to license the underlying character design.

## Quaternius Universal Animation Library

Mint body motion uses eight clips derived from the free Standard edition of
[Universal Animation Library](https://quaternius.itch.io/universal-animation-library)
by Quaternius. The pack and the derived runtime FBX are released under
[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).

The project retains only idle, talking, walking, sitting, and greeting actions;
unused models and clips were removed for the Android XR build.

## Qwen3-0.6B-GGUF

The bundled local language model is `Qwen/Qwen3-0.6B-GGUF`, revision
`23749fefcc72300e3a2ad315e1317431b06b590a`, published by the Qwen team under
the Apache License 2.0. The application bundles the `Q8_0` GGUF file unchanged.

## llama.cpp

`libgirlfriend_ai.so` is built from `ggml-org/llama.cpp` tag `v0.1.2`
(`1511ce3bc3f087376c8526b4ad07100bfabb277f`) and linked into the Android app.
llama.cpp is distributed under the MIT License; its dependency notices remain
available in the upstream source repository.

## sherpa-onnx and offline speech models

The offline speech runtime uses `k2-fsa/sherpa-onnx` 1.13.5 Android ARM64
libraries together with the Unity-Sherpa-ONNX integration. sherpa-onnx is
distributed under the Apache License 2.0; the Unity integration and each
redistributed model remain subject to their own upstream notices.

The bundled ASR/VAD assets are Whisper Tiny and Silero VAD distributions from
the official sherpa-onnx model releases.

Speech synthesis uses the INT8 release of `Supertone/supertonic-3`. It runs
locally through ONNX Runtime and is distributed under the OpenRAIL-M model
license included by the upstream project. The application selects Russian and
the built-in F2 female voice; no voice cloning is performed.
