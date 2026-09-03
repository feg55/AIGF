using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using PonyuDev.SherpaOnnx.Tts;
using PonyuDev.SherpaOnnx.Tts.Config;
using PonyuDev.SherpaOnnx.Tts.Data;
using SherpaOnnx;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SherpaTtsAdapter : MonoBehaviour, ITts
    {
        [SerializeField] private AudioSource voiceSource;
        [Header("Offline neural voice")]
        [SerializeField, Range(0, 9)] private int speakerId = 6;
        [SerializeField, Range(2, 12)] private int generationSteps = 5;
        [SerializeField, Range(0.75f, 1.35f)] private float speechSpeed = 1.08f;
        [SerializeField] private string language = "ru";

        private TtsService service;
        private OfflineTts directTts;
        private readonly SemaphoreSlim generationGate = new SemaphoreSlim(1, 1);
        private AudioClip activeClip;
        private CancellationTokenSource playbackCancellation;

        public bool IsReady => directTts != null || service != null && service.IsReady;
        public string LastError { get; private set; } = string.Empty;

        private async void Start()
        {
            if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
            service = new TtsService();
            try
            {
                await service.InitializeAsync(ct: destroyCancellationToken);
                if (!service.IsReady) LastError = "No active Sherpa-ONNX TTS model is installed.";
                else if (service.ActiveProfile != null &&
                         service.ActiveProfile.modelType == TtsModelType.Supertonic)
                {
                    await InitializeDirectSupertonicAsync(service.ActiveProfile, destroyCancellationToken);
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                LastError = exception.Message;
                Debug.LogWarning($"[VOICE] Local TTS unavailable: {LastError}", this);
            }
        }

        private void OnDestroy()
        {
            Stop();
            service?.Dispose();
            service = null;
            if (generationGate.Wait(0))
            {
                directTts?.Dispose();
                directTts = null;
                generationGate.Release();
            }
            generationGate.Dispose();
        }

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(text)) return;
            Stop();
            playbackCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                destroyCancellationToken);
            var token = playbackCancellation.Token;

            if (directTts != null)
            {
                var audio = await GenerateSupertonicAsync(text.Trim(), token);
                token.ThrowIfCancellationRequested();
                if (audio.Samples == null || audio.Samples.Length == 0 || audio.SampleRate <= 0) return;
                activeClip = AudioClip.Create(
                    "MintVoice",
                    audio.Samples.Length,
                    1,
                    audio.SampleRate,
                    false);
                activeClip.SetData(audio.Samples, 0);
            }
            else
            {
                using var result = await service.GenerateAsync(text.Trim(), token);
                token.ThrowIfCancellationRequested();
                if (result == null || !result.IsValid) return;
                activeClip = result.ToAudioClip("MintVoice");
            }

            voiceSource.clip = activeClip;
            voiceSource.Play();
            try
            {
                while (voiceSource != null && voiceSource.isPlaying)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            finally
            {
                ReleaseClip();
            }
        }

        public void Stop()
        {
            playbackCancellation?.Cancel();
            playbackCancellation?.Dispose();
            playbackCancellation = null;
            if (voiceSource != null) voiceSource.Stop();
            ReleaseClip();
        }

        private void ReleaseClip()
        {
            if (voiceSource != null) voiceSource.clip = null;
            if (activeClip != null) Destroy(activeClip);
            activeClip = null;
        }

        private async Task InitializeDirectSupertonicAsync(
            TtsProfile profile,
            CancellationToken cancellationToken)
        {
            var modelDirectory = TtsModelPathResolver.GetModelDirectory(
                profile.profileName,
                profile.modelSource);
            var nativeConfig = TtsConfigBuilder.Build(profile, modelDirectory);

            // TtsService has already staged StreamingAssets on Android. Release its
            // engine before constructing the direct one so the headset never holds
            // two copies of the neural voice in memory.
            service.Dispose();
            service = null;
            directTts = await Task.Run(() => new OfflineTts(nativeConfig), cancellationToken);
            if (directTts.SampleRate <= 0)
            {
                directTts.Dispose();
                directTts = null;
                throw new InvalidOperationException("Supertonic initialized without a valid sample rate.");
            }

            Debug.Log(
                $"[VOICE] Supertonic ready: language={language}, speaker={speakerId}, " +
                $"steps={generationSteps}, sampleRate={directTts.SampleRate}.",
                this);
        }

        private async Task<GeneratedAudio> GenerateSupertonicAsync(
            string text,
            CancellationToken cancellationToken)
        {
            await generationGate.WaitAsync(cancellationToken);
            try
            {
                var engine = directTts;
                if (engine == null) return default;
                var configuredLanguage = string.IsNullOrWhiteSpace(language) ? "ru" : language.Trim();
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var config = new OfflineTtsGenerationConfig
                    {
                        SilenceScale = 0.2f,
                        Speed = speechSpeed,
                        Sid = speakerId,
                        NumSteps = generationSteps,
                        Extra = new Hashtable { ["lang"] = configuredLanguage }
                    };
                    var generated = engine.GenerateWithConfig(text, config, ContinueGeneration);
                    try
                    {
                        var samples = generated?.Samples;
                        var sampleRate = generated?.SampleRate ?? 0;
                        cancellationToken.ThrowIfCancellationRequested();
                        return new GeneratedAudio(samples, sampleRate);
                    }
                    finally
                    {
                        generated?.Dispose();
                    }
                });
            }
            finally
            {
                generationGate.Release();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(OfflineTtsCallbackProgressWithArg))]
        private static int ContinueGeneration(IntPtr samples, int count, float progress, IntPtr argument)
        {
            return 1;
        }

        private readonly struct GeneratedAudio
        {
            public readonly float[] Samples;
            public readonly int SampleRate;

            public GeneratedAudio(float[] samples, int sampleRate)
            {
                Samples = samples;
                SampleRate = sampleRate;
            }
        }
    }
}
