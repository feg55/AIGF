# Architecture

## Runtime flow

```text
PICO microphone -> Sherpa VAD/Whisper ASR --+
debug UI -----------------------------------+-> GirlBrain
                                               -> Qwen/llama.cpp
                                               -> strict AgentReply JSON
                                               -> ActionExecutor whitelist
                                               -> navigation / interaction / animation

reply text -> Sherpa VITS TTS -> AudioSource -> amplitude lip sync
PICO Scene Capture -> semantic RoomGraph -> runtime NavMesh -> safe targets
local memory -----------------------------------------------> prompt context
```

`GirlBrain` owns cancellation and high-level orchestration. A new request cancels obsolete speech, native generation, and the previous action sequence.

## LLM-to-body safety boundary

The model emits data only. `AgentJson` rejects wrapped/malformed JSON, unknown fields or action types, non-finite distances, oversized identifiers, more than five actions, and room IDs absent from `RoomGraph`.

`ActionExecutor` uses an explicit whitelist: `walk_to_user`, `walk_to`, `sit`, `stand`, `follow_user`, `stop`, `look_at_user`, `look_at`, `wave`, and safe-ID `play_animation`. There is no reflection, `SendMessage`, arbitrary native dispatch, generated code, filesystem command, or model-authored Animator parameter.

Unity stays authoritative over NavMesh sampling/reachability, personal distance, room anchors, seated state, and cancellation.

## PICO room and navigation

`PicoSdkSceneSource` queries PICO spatial anchors, semantic labels, poses, bounds, and polygons after Scene Capture. On a regular PICO 4 with OS 5.13.x it uses the SDK's legacy MR path and also performs a filtered object-anchor query because sofas, tables, and chairs share the legacy `Object` scene flag. `PicoRoomProvider` converts results into stable `RoomNode` records and refreshes the same `RoomGraph` instance after either the legacy `SpatialSceneCaptured` event or the newer `SceneAnchorDataUpdated` event. Seats receive explicit approach and sit transforms. Each refresh cancels stale movement and rebuilds navigation from captured floor and obstacle colliders.

Speech output is fully offline. `SherpaTtsAdapter` loads the INT8 Supertonic 3 model through sherpa-onnx, selects Russian plus the bright F2 female voice (`sid=1` in sherpa's alphabetically packed `voice.bin`), and uses a small flow-step count suitable for the PICO 4 CPU. The legacy Piper/Irina assets are not included in the project or APK.

If device room data is unavailable, the Android build fails safely with an empty room graph. Generated room geometry is an Editor-only fallback and is disabled on Android.

Passthrough is isolated behind `IPicoPassthroughBackend`; the concrete backend delegates to `PXR_Manager`. Gameplay depends on project-owned interfaces rather than PICO types.

## Avatar

The Mint root owns navigation, interaction, brain, speech, and animation components. The imported humanoid FBX keeps only deforming bones and supplies LOD0/LOD1/LOD2. `GirlAnimator` exposes centralized, safe animation operations; `ProceduralAvatarMotion` supplies a fallback when an authored controller/clip is missing. `MintFacialDriver`, `LookAtUser`, and `AvatarLipSync` handle blink, head/eye attention, and speech amplitude.

## Local inference

The canonical GGUF is in `Assets/StreamingAssets/Models`. Android copies it once to private persistent storage, verifies the manifest size/SHA-256, and replaces it only when the pinned manifest changes.

`LlamaCppLocalLLM` serializes native access, runs generation away from the Unity main thread, forwards cancellation to the llama abort callback, and decodes UTF-8 bytes explicitly. The native boundary contains only `gf_init`, `gf_generate`, `gf_cancel`, and `gf_shutdown`.

The prompt uses Qwen chat control tokens, `/no_think`, compact room facts, a bounded recent dialogue, and retrieved local memories. Output remains subject to the strict parser and executor boundary.

## Offline voice

`SherpaVoiceInput` uses the microphone, Silero VAD, and Whisper tiny Russian ASR. `SherpaTtsAdapter` synthesizes Russian speech using the Supertonic 3 INT8 F2 voice (`sid=1`). Profiles and model assets are bundled under `Assets/StreamingAssets/SherpaOnnx`; no cloud service is called. The underlying `IVad`, `IStt`, and `ITts` boundaries still allow deterministic mocks for Editor diagnostics.

## Memory and privacy

`LocalMemoryStore` keeps at most 200 deduplicated items in the app's private persistent directory. `MemoryRetriever` selects a small keyword/recency/importance-ranked subset. Audio, room data, prompts, and model inference remain local; the application implements no telemetry or runtime model download.
