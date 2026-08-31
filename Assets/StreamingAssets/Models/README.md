# Local GGUF model

Place the application model at:

`Assets/StreamingAssets/Models/qwen3-1.7b-q4.gguf`

The expected family is Qwen 3 around 1.7B parameters, preferably `Q4_K_M` (roughly 1.1-1.3 GB depending on the conversion). A small 1B-3B instruct model is the intended PICO 4 range; 7B/8B models are outside the MVP memory and latency budget.

After placing the file, update `model-manifest.json` with its exact byte length and lowercase SHA-256. Empty `sha256` and zero `sizeBytes` are accepted for development but should not be used for a release build.

The model is never fetched at application startup. In Editor it is opened directly from this directory. In Android builds it is bundled in StreamingAssets and installed once into the app's permanent private `files/models` storage because llama.cpp requires a normal filesystem path.

Use Git LFS for the GGUF when the repository is version controlled. If policy requires an ignored binary, ignore only `*.gguf`; keep this directory, README, and manifest in source control. The canonical developer copy must still physically remain here.

No GGUF binary is included in this source snapshot. Until one is added, `AppConfig.UseMockLLM` must remain enabled or the configured mock fallback will be used with a clear error.
