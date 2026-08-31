using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using UnityEngine;

namespace Aigf.Companion.AI
{
    public sealed class LlamaCppLocalLLM : ILocalLLM, IDisposable
    {
        private const int OutputBufferBytes = 32768;
        private readonly AppConfig config;
        private readonly PromptBuilder promptBuilder = new PromptBuilder();
        private readonly SemaphoreSlim inferenceGate = new SemaphoreSlim(1, 1);
        private bool nativeInitialized;
        private bool disposed;

        public bool IsReady => nativeInitialized && !disposed;
        public string LastError { get; private set; } = string.Empty;
        public string LastRawOutput { get; private set; } = string.Empty;

        public LlamaCppLocalLLM(AppConfig appConfig)
        {
            config = appConfig != null ? appConfig : AppConfig.CreateRuntimeDefaults();
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var manifest = await LocalModelPathResolver.LoadBundledManifestAsync(cancellationToken);
                if (!string.Equals(manifest.Filename, config.ModelFilename, StringComparison.Ordinal))
                {
                    LastError = $"Config expects '{config.ModelFilename}' but manifest declares '{manifest.Filename}'.";
                    return false;
                }

                var install = await LocalModelPathResolver.EnsureRuntimeModelAvailableAsync(manifest, cancellationToken);
                if (!install.Succeeded)
                {
                    LastError = install.Error;
                    Debug.LogError($"[MODEL] {LastError}");
                    return false;
                }

                var result = await Task.Run(() => Native.gf_init(
                    install.ModelPath,
                    config.ContextSize,
                    config.Threads,
                    config.UseGpuOffload ? 1 : 0), cancellationToken);
                if (result != 0)
                {
                    LastError = $"Native llama.cpp initialization failed with code {result}.";
                    return false;
                }

                nativeInitialized = true;
                return true;
            }
            catch (DllNotFoundException exception)
            {
                LastError = $"libgirlfriend_ai is missing: {exception.Message}";
                return false;
            }
            catch (EntryPointNotFoundException exception)
            {
                LastError = $"Native bridge ABI mismatch: {exception.Message}";
                return false;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                LastError = exception.Message;
                return false;
            }
        }

        public async Task<AgentReply> GenerateAsync(
            string userMessage,
            AgentContext context,
            CancellationToken cancellationToken = default)
        {
            if (!IsReady) throw new InvalidOperationException(LastError.Length > 0 ? LastError : "Local LLM is not ready.");
            await inferenceGate.WaitAsync(cancellationToken);
            try
            {
                var prompt = promptBuilder.Build(userMessage, context);
                using (cancellationToken.Register(RequestNativeCancellation))
                {
                    var nativeResult = await Task.Run(() => GenerateNative(prompt), CancellationToken.None);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (nativeResult.Code != 0)
                    {
                        throw new InvalidOperationException($"Native generation failed with code {nativeResult.Code}.");
                    }

                    LastRawOutput = nativeResult.Json;
                    if (!AgentJson.TryParse(nativeResult.Json, context != null ? context.Room : null, out var reply, out var error, config.MaxActionsPerReply))
                    {
                        throw new InvalidOperationException($"Model returned invalid action JSON: {error}");
                    }

                    return reply;
                }
            }
            finally
            {
                inferenceGate.Release();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (nativeInitialized)
            {
                try { Native.gf_shutdown(); }
                catch (Exception exception) { Debug.LogWarning($"[LLM] Native shutdown failed: {exception.Message}"); }
                nativeInitialized = false;
            }

            inferenceGate.Dispose();
        }

        private NativeResult GenerateNative(string prompt)
        {
            var buffer = new StringBuilder(OutputBufferBytes);
            var code = Native.gf_generate(
                prompt,
                config.MaxTokens,
                config.Temperature,
                config.TopP,
                buffer,
                buffer.Capacity);
            return new NativeResult(code, buffer.ToString());
        }

        private static void RequestNativeCancellation()
        {
            try { Native.gf_cancel(); }
            catch (Exception) { }
        }

        private readonly struct NativeResult
        {
            public int Code { get; }
            public string Json { get; }

            public NativeResult(int code, string json)
            {
                Code = code;
                Json = json ?? string.Empty;
            }
        }

        private static class Native
        {
            private const string LibraryName = "girlfriend_ai";

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern int gf_init(
                [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
                int contextSize,
                int threads,
                int useGpuOffload);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern int gf_generate(
                [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
                int maxTokens,
                float temperature,
                float topP,
                StringBuilder output,
                int outputCapacity);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void gf_cancel();

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void gf_shutdown();
        }
    }
}
