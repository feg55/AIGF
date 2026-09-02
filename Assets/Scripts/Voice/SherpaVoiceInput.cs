using System;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using PonyuDev.SherpaOnnx.Asr.Offline;
using PonyuDev.SherpaOnnx.Asr.Offline.Engine;
using PonyuDev.SherpaOnnx.Common.Audio;
using PonyuDev.SherpaOnnx.Common.Audio.Config;
using PonyuDev.SherpaOnnx.Vad;
using PonyuDev.SherpaOnnx.Vad.Engine;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    public sealed class SherpaVoiceInput : MonoBehaviour
    {
        [SerializeField] private GirlBrain brain;
        [SerializeField] private bool startListeningOnReady = true;

        private VadService vad;
        private AsrService asr;
        private MicrophoneSource microphone;
        private float[] vadWindow;
        private int vadWindowPosition;
        private bool processing;

        public bool IsReady => vad != null && vad.IsReady && asr != null && asr.IsReady;
        public bool IsListening => microphone != null && microphone.IsRecording;
        public string Status { get; private set; } = "Initializing local voice";

        private async void Start()
        {
            if (brain == null) brain = FindAnyObjectByType<GirlBrain>();
            vad = new VadService();
            asr = new AsrService();
            try
            {
                await vad.InitializeAsync(ct: destroyCancellationToken);
                await asr.InitializeAsync(ct: destroyCancellationToken);
                if (!vad.IsReady || !asr.IsReady)
                {
                    Status = "Voice models are not installed; text input is available";
                    Debug.LogWarning($"[VOICE] {Status}", this);
                    return;
                }

                vad.OnSegment += HandleSegment;
                vad.OnSpeechStart += HandleSpeechStart;
                vad.OnSpeechEnd += HandleSpeechEnd;
                var settings = await MicrophoneSettingsLoader.LoadAsync(destroyCancellationToken);
                microphone = new MicrophoneSource(settings);
                microphone.SamplesAvailable += HandleSamples;
                Status = "Voice ready";
                if (startListeningOnReady) await StartListeningAsync();
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Status = $"Voice unavailable: {exception.Message}";
                Debug.LogWarning($"[VOICE] {Status}", this);
            }
        }

        private void OnDestroy()
        {
            if (microphone != null) microphone.SamplesAvailable -= HandleSamples;
            if (vad != null)
            {
                vad.OnSegment -= HandleSegment;
                vad.OnSpeechStart -= HandleSpeechStart;
                vad.OnSpeechEnd -= HandleSpeechEnd;
            }
            microphone?.Dispose();
            asr?.Dispose();
            vad?.Dispose();
        }

        public async Task<bool> StartListeningAsync()
        {
            if (!IsReady || processing) return false;
            if (microphone.IsRecording) return true;
            var started = await microphone.StartRecordingAsync(destroyCancellationToken);
            Status = started ? "Listening" : "Microphone unavailable";
            return started;
        }

        public void StopListening()
        {
            microphone?.StopRecording();
            vad?.Flush();
            Status = IsReady ? "Voice ready" : Status;
        }

        private void HandleSamples(float[] samples)
        {
            if (!processing && brain != null && brain.CurrentState != AgentState.Speaking)
            {
                FeedVad(samples);
            }
        }

        private void FeedVad(float[] samples)
        {
            if (samples == null || samples.Length == 0 || vad == null || !vad.IsReady) return;
            var windowSize = vad.WindowSize;
            if (windowSize <= 0) return;
            if (vadWindow == null || vadWindow.Length != windowSize)
            {
                vadWindow = new float[windowSize];
                vadWindowPosition = 0;
            }

            for (var i = 0; i < samples.Length; i++)
            {
                vadWindow[vadWindowPosition++] = samples[i];
                if (vadWindowPosition < windowSize) continue;
                vad.AcceptWaveform(vadWindow);
                vadWindowPosition = 0;
            }
        }

        private async void HandleSegment(VadSegment segment)
        {
            if (processing || segment?.Samples == null || segment.Samples.Length == 0 || brain == null) return;
            processing = true;
            microphone?.StopRecording();
            Status = "Recognizing";
            try
            {
                var sampleRate = vad.ActiveProfile?.sampleRate ?? 16000;
                var result = await asr.RecognizeAsync(segment.Samples, sampleRate);
                if (result != null && result.IsValid)
                {
                    Status = $"Heard: {result.Text}";
                    await brain.ProcessUserMessageAsync(result.Text, destroyCancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Status = $"Voice error: {exception.Message}";
                Debug.LogWarning($"[VOICE] {Status}", this);
            }
            finally
            {
                processing = false;
                if (startListeningOnReady && this != null && !destroyCancellationToken.IsCancellationRequested)
                    await StartListeningAsync();
            }
        }

        private void HandleSpeechStart()
        {
            Status = "Hearing speech";
        }

        private void HandleSpeechEnd()
        {
            Status = "Recognizing";
        }
    }
}
