# Status

## IMPLEMENTED

- strict `AgentReply` JSON contract, ten-action whitelist, five-action limit, safe identifiers, and `RoomGraph` target validation
- sequential cancellable `ActionExecutor`
- semantic `RoomGraph`, explicit seat geometry, `ManualRoomProvider`, and SDK-neutral PICO room/passthrough boundaries
- NavMesh walk-to-user, walk-to-node, follow refresh threshold, stop, timeout, sampling, and personal space
- deterministic sit/stand alignment and state
- centralized animation wrapper, safe named-animation map, head look, and amplitude lip sync
- bilingual Russian/English `MockLocalLLM`, compact prompt, `GirlBrain`, debug text UI, and convenience buttons
- mock VAD/STT/TTS, voice pipeline, local memory store, and bounded retrieval
- project-local model manifest/path resolver, one-time permanent Android installer, optional size/SHA-256 verification
- async `LlamaCppLocalLLM` P/Invoke layer with serialized inference and cancellation
- Android ARM64 llama.cpp C ABI/CMake scaffold
- Editor demo scene generator and EditMode tests
- Android IL2CPP/ARM64 project settings

## PARTIALLY IMPLEMENTED

- local LLM: managed/native integration exists; a real GGUF and compiled native library are not included
- voice: interfaces, orchestration, and mocks exist; production local VAD/STT/TTS engines and their model assets are not included
- PICO MR: clean adapters exist; no vendor SDK implementation is possible until a supported PICO package is installed
- dynamic MR navigation: semantic boundary exists; runtime floor/obstacle NavMesh generation is not implemented
- animation: wrapper and calls exist; the generated scene uses safe logged placeholders without an Animator Controller

## EDITOR TESTED

- Unity `6000.3.23f1` imported and compiled the runtime, Editor tooling, and tests with no C# errors.
- `CompanionDemo` was generated in the repository with a baked NavMesh, manual sofa anchors, debug UI, runtime config, and new-Input-System EventSystem.
- EditMode: 16/16 tests passed (`Logs/editmode-results.xml`).
- PlayMode: 1/1 scene acceptance test passed in 4.7 seconds (`Logs/playmode-results-2.xml`). It executed bootstrap, come-here navigation/personal space, wave, sofa walk/sit, stand, follow/stop, and look-at-user.

## DEVICE DEPENDENT

- PICO passthrough and Scene Capture permissions/APIs
- real HMD transform/XR rig assignment
- runtime NavMesh construction from captured geometry
- llama.cpp ARM64 performance, thermals, RAM, and optional GPU offload
- first-launch installation duration for the selected GGUF
- Android microphone and final offline voice engines

## TODO

- add the licensed `qwen3-1.7b-q4.gguf`, byte size, and SHA-256
- pin/build llama.cpp and copy `libgirlfriend_ai.so`
- install the selected PICO SDK and implement the two adapter components
- integrate a production humanoid avatar, Animator Controller, safe clips, and calibrated seat anchors
- integrate and profile offline VAD/STT/TTS models
- implement captured-floor/obstacle NavMesh updates
- run and record PICO 4 hardware acceptance tests

## MANUAL UNITY SETUP REQUIRED

- wait for first import, open `Assets/Scenes/CompanionDemo.unity`, and press Play
- run EditMode tests in Test Runner
- add the real GGUF/native plugin before disabling mock mode
- install/configure PICO XR packages before building a passthrough APK
- tune avatar root, agent dimensions, head bone, animation parameters, and interaction anchors for final art
