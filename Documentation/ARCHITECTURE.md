# Architecture

## Runtime flow

```text
debug text / future microphone
        -> GirlBrain
        -> ILocalLLM (MockLocalLLM or LlamaCppLocalLLM)
        -> AgentJson + AgentSafety
        -> ActionExecutor whitelist
        -> GirlNavigation / AvatarInteraction / GirlAnimator / LookAtUser
        -> avatar

reply speech -> ITts -> AudioSource -> AvatarLipSync
room adapter -> RoomGraph -> compact PromptBuilder context
```

`GirlBrain` owns request cancellation and high-level orchestration. A newer command cancels the previous TTS, native generation where supported, and obsolete action sequence.

## LLM-to-body safety boundary

The model emits only `AgentReply` data. `AgentJson` rejects malformed/wrapped output, unknown properties, unknown action types, non-finite distances, oversized identifiers, replies over five actions, and room IDs absent from `RoomGraph`.

`ActionExecutor` uses an explicit `switch` over:

- `walk_to_user`
- `walk_to`
- `sit`
- `stand`
- `follow_user`
- `stop`
- `look_at_user`
- `look_at`
- `wave`
- `play_animation`

No reflection, `SendMessage`, dynamic method selection, arbitrary native dispatch, generated code, filesystem command, or model-authored Animator parameter is used. Named animations must map to an Inspector-configured safe ID. Unity remains authoritative over destinations, NavMesh sampling, reachability, interaction anchors, seating, state, and cancellation.

## RoomGraph

`RoomGraph` exposes semantic `RoomNode` records instead of meshes. Each node has a stable ID, type, bounds, semantic metadata, and explicit approach/interaction geometry. Seats are valid only when both an `ApproachPoint` and `SitPoint` exist.

`ManualRoomProvider` supplies the Editor scene. `PicoRoomProvider` consumes the project-owned `IPicoSceneSource` boundary and maps labels into normalized nodes. Because this repository has no PICO SDK, no vendor class names are referenced or guessed.

## Navigation and interaction

`GirlNavigation` samples every requested destination onto the NavMesh, maintains user personal space, refreshes follow destinations only after meaningful user movement, supports timeouts/cancellation, and never teleports during normal walking.

Sitting is deterministic:

```text
validate seat -> walk to ApproachPoint -> stop agent -> trigger sit
-> disable navigation -> align root to SitPoint/SitRotation -> Sitting
```

Standing triggers its transition, samples the seat approach position, resumes the agent there, and returns to `Idle`. Missing NavMesh/Animator/anchors produce action failures or warnings, not null-reference crashes.

## Local inference and model storage

The canonical model is always:

`Assets/StreamingAssets/Models/<model>.gguf`

Editor opens that file directly. Android reads the bundled asset and installs it once into permanent private `Application.persistentDataPath/models`. The installed manifest version, filename, optional size, and optional SHA-256 decide whether replacement is necessary. No runtime network fetch is implemented.

`LlamaCppLocalLLM` runs `gf_generate` on a worker task and serializes access with `SemaphoreSlim`. `gf_cancel` drives llama.cpp's CPU abort callback. The C# gameplay layer sees only `ILocalLLM`; llama.cpp headers and types remain under `Native/LlamaBridge`.

## Voice and lip sync

`IVad`, `IStt`, and `ITts` isolate offline voice engines. Mock implementations keep the loop usable without microphone or voice models. `VoicePipeline` accepts an `AudioClip`, applies VAD, STT, and forwards text to `GirlBrain`. `AvatarLipSync` uses a cached amplitude buffer and optional mouth blend shape.

Real sherpa-onnx/whisper.cpp/TTS adapters are not included; they can be added without changing the brain or action layer.

## Memory

`LocalMemoryStore` holds at most 200 explicit local items in private persistent storage. `MemoryRetriever` returns at most a few keyword/recency/importance-ranked items. Full chat history and embeddings are not required or injected into every prompt.

## PICO integration boundary

- HMD pose enters as a normal `Transform`.
- passthrough enters through `IPicoPassthroughBackend`.
- Scene Capture enters through `IPicoSceneSource`.
- the rest of the application depends only on `RoomGraph` and Unity components.

On-device dynamic NavMesh construction from captured floor/walls is deliberately not claimed complete. The Editor vertical slice uses AI Navigation's baked `NavMeshSurface`.
