# Local GGUF model

The application model is bundled at:

`Assets/StreamingAssets/Models/qwen3-0.6b-q8_0.gguf`

It is the official `Qwen/Qwen3-0.6B-GGUF` Q8_0 file, revision `23749fefcc72300e3a2ad315e1317431b06b590a`. Exact size and SHA-256 are pinned in `model-manifest.json` and checked before release builds and again during first-launch installation.

The model is never fetched at application startup. In Editor it is opened directly from this directory. In Android builds it is bundled in StreamingAssets and installed once into the app's permanent private `files/models` storage because llama.cpp requires a normal filesystem path.

Git LFS is mandatory for this file. `AppConfig.UseMockLLM` is disabled in the production asset; missing/incompatible native inference falls back to `MockLocalLLM` without crashing.
