# PICO 4 device acceptance checklist

Target device: regular PICO 4 on OS 5.13.x. The project supports the legacy PICO 4 Scene Capture API as well as the newer scene-data update event.

Record headset firmware, APK commit, room, battery state, and ambient temperature with each run.

## Install and startup

- [ ] `MintARCompanion.apk` installs and launches without a network connection.
- [ ] Microphone, spatial sensing/room, and passthrough permission prompts are understandable and recoverable after denial.
- [ ] First-launch model extraction completes; later launches reuse the verified model.
- [ ] Passthrough remains stable through headset sleep/resume and app focus changes.
- [ ] The diagnostics panel shows local Qwen, Sherpa ASR/TTS, and captured-room readiness.

## Room safety

- [ ] Captured floor height matches the real floor within 5 cm.
- [ ] Walls, doors, windows, tables, sofas, and beds have the correct semantic type and usable bounds.
- [ ] After adding or changing a captured sofa, diagnostics updates to `sofas: 1` (or more) and `seats: 1` without restarting the app.
- [ ] The avatar never routes through a wall or solid furniture.
- [ ] `come here` stops at the configured personal distance and never enters the user's guardian boundary.
- [ ] Follow mode replans smoothly, stops on command, and does not oscillate near the user.
- [ ] Missing/invalid room capture disables unsafe movement instead of using fake Android geometry.

## Interaction and avatar

- [ ] Mint scale and feet alignment look correct from normal standing and seated viewpoints.
- [ ] Walking direction, acceleration, turn rate, and foot motion are acceptable without visible sliding.
- [ ] Each detected seat has a reachable approach point and a correctly aligned sit pose.
- [ ] Stand returns to a reachable point without intersecting furniture.
- [ ] Wave, look-at, blink, breathing, emotion, and lip sync remain stable for a 20-minute session.
- [ ] LOD transitions are unobtrusive and do not detach clothing or deform the skeleton.

## Conversation

- [ ] Russian near-field speech is recognized in quiet and typical household noise.
- [ ] VAD does not continuously trigger on synthesized speech; echo behavior is acceptable.
- [ ] A new utterance cancels obsolete generation, speech, and actions.
- [ ] Qwen returns valid strict JSON for at least 50 varied commands; invalid output produces no unsafe action.
- [ ] TTS is intelligible, playback finishes reliably, and the mouth returns to neutral.
- [ ] Airplane mode does not break conversation after startup.

## Performance and stability

- [ ] Record median and p95 ASR, first-token, full-response, and TTS latency.
- [ ] Record peak RAM, app storage, average frame time, and thermal state after 10, 30, and 60 minutes.
- [ ] No sustained frame drops, Android ANR, native crash, or low-memory termination occurs in a 60-minute mixed workload.
- [ ] Repeated cancel/regenerate cycles do not leak memory.
- [ ] Battery drain and headset temperature are acceptable for the intended session length.

## Release gate

Do not distribute until all safety items pass in at least two differently sized captured rooms. Any collision, incorrect floor, missing-room fallback, native crash, or invalid-action execution is release-blocking.
