#include "girlfriend_ai.h"

#include "llama.h"

#include <algorithm>
#include <atomic>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

namespace {

std::mutex g_mutex;
std::atomic_bool g_cancelled{false};
llama_model * g_model = nullptr;
int32_t g_context_size = 2048;
int32_t g_threads = 4;
bool g_backend_initialized = false;

bool abort_callback(void *) {
    return g_cancelled.load(std::memory_order_relaxed);
}

void cleanup_locked() {
    if (g_model != nullptr) {
        llama_model_free(g_model);
        g_model = nullptr;
    }

    if (g_backend_initialized) {
        llama_backend_free();
        g_backend_initialized = false;
    }
}

bool append_token_piece(
    const llama_vocab * vocab,
    llama_token token,
    std::string & output,
    int32_t output_capacity) {
    std::vector<char> piece(256);
    int32_t length = llama_token_to_piece(
        vocab,
        token,
        piece.data(),
        static_cast<int32_t>(piece.size()),
        0,
        true);

    if (length < 0) {
        piece.resize(static_cast<size_t>(-length));
        length = llama_token_to_piece(
            vocab,
            token,
            piece.data(),
            static_cast<int32_t>(piece.size()),
            0,
            true);
    }

    if (length < 0 || output.size() + static_cast<size_t>(length) >= static_cast<size_t>(output_capacity)) {
        return false;
    }

    output.append(piece.data(), static_cast<size_t>(length));
    return true;
}

} // namespace

int32_t gf_init(
    const char * model_path,
    int32_t context_size,
    int32_t threads,
    int32_t use_gpu_offload) {
    std::lock_guard<std::mutex> lock(g_mutex);
    cleanup_locked();

    if (model_path == nullptr || model_path[0] == '\0' || context_size < 512 || threads < 1) {
        return -1;
    }

    llama_backend_init();
    g_backend_initialized = true;

    llama_model_params model_params = llama_model_default_params();
    model_params.n_gpu_layers = use_gpu_offload != 0 ? 99 : 0;
    g_model = llama_model_load_from_file(model_path, model_params);
    if (g_model == nullptr) {
        cleanup_locked();
        return -2;
    }

    g_context_size = std::max<int32_t>(512, context_size);
    g_threads = std::max<int32_t>(1, threads);
    return 0;
}

int32_t gf_generate(
    const char * prompt,
    int32_t max_tokens,
    float temperature,
    float top_p,
    char * output,
    int32_t output_capacity) {
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_model == nullptr || prompt == nullptr || output == nullptr || output_capacity < 2) {
        return -1;
    }

    output[0] = '\0';
    g_cancelled.store(false, std::memory_order_relaxed);
    max_tokens = std::clamp<int32_t>(max_tokens, 1, 512);

    const llama_vocab * vocab = llama_model_get_vocab(g_model);
    const int32_t prompt_length = static_cast<int32_t>(std::strlen(prompt));
    const int32_t token_count = -llama_tokenize(
        vocab,
        prompt,
        prompt_length,
        nullptr,
        0,
        true,
        true);
    if (token_count <= 0 || token_count + max_tokens > g_context_size) {
        return -3;
    }

    std::vector<llama_token> tokens(static_cast<size_t>(token_count));
    if (llama_tokenize(
            vocab,
            prompt,
            prompt_length,
            tokens.data(),
            token_count,
            true,
            true) < 0) {
        return -4;
    }

    llama_context_params context_params = llama_context_default_params();
    context_params.n_ctx = static_cast<uint32_t>(g_context_size);
    context_params.n_batch = static_cast<uint32_t>(std::min(token_count, g_context_size));
    context_params.n_threads = g_threads;
    context_params.n_threads_batch = g_threads;
    context_params.abort_callback = abort_callback;
    context_params.abort_callback_data = nullptr;

    llama_context * context = llama_init_from_model(g_model, context_params);
    if (context == nullptr) {
        return -5;
    }

    llama_sampler_chain_params sampler_params = llama_sampler_chain_default_params();
    llama_sampler * sampler = llama_sampler_chain_init(sampler_params);
    llama_sampler_chain_add(sampler, llama_sampler_init_top_p(std::clamp(top_p, 0.1f, 1.0f), 1));
    llama_sampler_chain_add(sampler, llama_sampler_init_temp(std::clamp(temperature, 0.0f, 2.0f)));
    llama_sampler_chain_add(sampler, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));

    int32_t result = 0;
    llama_batch batch = llama_batch_get_one(tokens.data(), token_count);
    std::string generated;
    generated.reserve(static_cast<size_t>(std::min(output_capacity - 1, 4096)));

    for (int32_t generated_count = 0; generated_count < max_tokens; ++generated_count) {
        if (g_cancelled.load(std::memory_order_relaxed)) {
            result = -8;
            break;
        }

        if (llama_decode(context, batch) != 0) {
            result = g_cancelled.load(std::memory_order_relaxed) ? -8 : -6;
            break;
        }

        const llama_token token = llama_sampler_sample(sampler, context, -1);
        if (llama_vocab_is_eog(vocab, token)) {
            break;
        }

        if (!append_token_piece(vocab, token, generated, output_capacity)) {
            result = -7;
            break;
        }

        llama_token next = token;
        batch = llama_batch_get_one(&next, 1);
    }

    llama_sampler_free(sampler);
    llama_free(context);

    if (result == 0) {
        std::memcpy(output, generated.data(), generated.size());
        output[generated.size()] = '\0';
    }

    return result;
}

void gf_cancel(void) {
    g_cancelled.store(true, std::memory_order_relaxed);
}

void gf_shutdown(void) {
    g_cancelled.store(true, std::memory_order_relaxed);
    std::lock_guard<std::mutex> lock(g_mutex);
    cleanup_locked();
}
