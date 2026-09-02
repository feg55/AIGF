# Offline voice assets

The active pipeline is fully local:

- ASR: `sherpa-onnx-whisper-tiny` (multilingual Whisper Tiny, Russian selected)
- VAD: `silero_vad.onnx`
- TTS: `vits-piper-ru_RU-irina-medium` (female Russian Piper voice)

The model files are redistributed from the official `k2-fsa/sherpa-onnx`
release assets. Their upstream model cards and licenses remain applicable.
The active profiles use full-precision ONNX files for the safest ARM64
baseline; do not enable INT8 until it has been verified on the target headset.
