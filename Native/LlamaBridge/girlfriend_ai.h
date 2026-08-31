#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define GF_API __declspec(dllexport)
#else
#define GF_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Returns 0 on success. The model path must be a normal UTF-8 filesystem path.
GF_API int32_t gf_init(
    const char * model_path,
    int32_t context_size,
    int32_t threads,
    int32_t use_gpu_offload);

// Writes one UTF-8 JSON object and a trailing NUL into output.
// Returns 0 on success, a negative error code on failure.
GF_API int32_t gf_generate(
    const char * prompt,
    int32_t max_tokens,
    float temperature,
    float top_p,
    char * output,
    int32_t output_capacity);

// Thread-safe cooperative cancellation. CPU llama_decode observes this flag.
GF_API void gf_cancel(void);

GF_API void gf_shutdown(void);

#ifdef __cplusplus
}
#endif
