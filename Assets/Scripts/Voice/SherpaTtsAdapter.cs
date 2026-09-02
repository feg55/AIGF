using System;
using System.Threading;
using System.Threading.Tasks;
using PonyuDev.SherpaOnnx.Tts;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SherpaTtsAdapter : MonoBehaviour, ITts
    {
        [SerializeField] private AudioSource voiceSource;

        private TtsService service;
        private AudioClip activeClip;
        private CancellationTokenSource playbackCancellation;

        public bool IsReady => service != null && service.IsReady;
        public string LastError { get; private set; } = string.Empty;

        private async void Start()
        {
            if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
            service = new TtsService();
            try
            {
                await service.InitializeAsync(ct: destroyCancellationToken);
                if (!service.IsReady) LastError = "No active Sherpa-ONNX TTS model is installed.";
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
        }

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(text)) return;
            Stop();
            playbackCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                destroyCancellationToken);
            var token = playbackCancellation.Token;

            using (var result = await service.GenerateAsync(text.Trim(), token))
            {
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
    }
}
