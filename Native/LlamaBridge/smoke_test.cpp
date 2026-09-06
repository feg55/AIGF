#include "girlfriend_ai.h"
#include <cstdio>

// Run on a connected ARM64 headset to exercise the real GGUF and sampler.
int main(int argc, char ** argv) {
    if (argc != 2) return 2;
    const int init = gf_init(argv[1], 4096, 4, 0);
    std::printf("INIT=%d\n", init);
    std::fflush(stdout);
    if (init != 0) return 3;
    const char * prompt =
        "<|im_start|>system\nYou are Mint, a friendly companion. Answer in Russian. "
        "Return one JSON object only: {\"speech\":\"your answer\",\"emotion\":\"warm\",\"actions\":[]}. "
        "For ordinary conversation answer the question naturally with no actions, in 1-2 short sentences.\n<|im_end|>\n"
        "<|im_start|>user\nКак прошёл твой день? /no_think\n<|im_end|>\n"
        "<|im_start|>assistant\n<think>\n\n</think>\n\n";
    char output[32768]{};
    const int result = gf_generate(prompt, 384, 0.35f, 0.9f, output, sizeof(output));
    std::printf("GENERATE=%d\n%s\n", result, output);
    gf_shutdown();
    return result == 0 ? 0 : 4;
}
