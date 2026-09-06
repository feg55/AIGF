using System;
using System.Threading;
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
        private bool starting;
        private bool initializing;
        private bool disposed;
        private bool paused;
        private bool suppressingPlayback;
        private int captureVersion;
        private CancellationToken lifetime;

        public bool IsReady => vad != null && vad.IsReady && asr != null && asr.IsReady;
        public bool IsListening => microphone != null && microphone.IsRecording;
        public bool IsMuted { get; private set; }
        public bool IsRecognizing => processing;
        public float InputLevel { get; private set; }
        public string LastTranscript { get; private set; } = string.Empty;
        public string Status { get; private set; } = "Initializing local voice";
        public event Action StateChanged;

        private bool CanCapture => !disposed && !paused && isActiveAndEnabled && !IsMuted;

        private void Awake()
        {
            lifetime = destroyCancellationToken;
            IsMuted = !startListeningOnReady;
            // Also attaches to existing scenes, without requiring a scene rebuild.
            if (GetComponent<UI.PicoMicrophoneHud>() == null)
                gameObject.AddComponent<UI.PicoMicrophoneHud>();
        }

        private async void Start()
        {
            initializing = true;
            if (brain == null) brain = FindAnyObjectByType<GirlBrain>();
            vad = new VadService();
            asr = new AsrService();
            try
            {
                await vad.InitializeAsync(ct: lifetime);
                lifetime.ThrowIfCancellationRequested();
                await asr.InitializeAsync(ct: lifetime);
                lifetime.ThrowIfCancellationRequested();
                if (!vad.IsReady || !asr.IsReady)
                {
                    Status = "Voice models are not installed; text input is available";
                    Debug.LogWarning($"[VOICE] {Status}", this);
                    return;
                }

                vad.OnSegment += HandleSegment;
                vad.OnSpeechStart += HandleSpeechStart;
                vad.OnSpeechEnd += HandleSpeechEnd;
                var settings = await MicrophoneSettingsLoader.LoadAsync(lifetime);
                settings.sampleRate = vad.ActiveProfile?.sampleRate ?? 16000;
                microphone = new MicrophoneSource(settings);
                microphone.SamplesAvailable += HandleSamples;
                Status = "Voice ready";
                if (CanCapture) await StartListeningAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Status = $"Voice unavailable: {exception.Message}";
                Debug.LogWarning($"[VOICE] {Status}", this);
            }
            finally
            {
                initializing = false;
                if (disposed) DisposeServices();
                else StateChanged?.Invoke();
            }
        }

        private void OnDestroy()
        {
            disposed = true;
            captureVersion++;
            microphone?.StopRecording();
            if (!initializing && !processing && !starting) DisposeServices();
        }

        private void DisposeServices()
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
            microphone = null;
            asr = null;
            vad = null;
        }

        private void OnDisable() => SuspendCapture();

        private void OnEnable()
        {
            if (IsReady && CanCapture) ResumeCapture();
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) SuspendCapture();
            else if (CanCapture) ResumeCapture();
        }

        public void ToggleMute() => SetMuted(!IsMuted);

        public void SetMuted(bool muted)
        {
            if (IsMuted == muted) return;
            IsMuted = muted;
            Debug.Log($"[VOICE] Microphone {(muted ? "muted" : "unmuted")}.", this);
            if (muted)
            {
                SuspendCapture();
                Status = "Muted";
            }
            else if (CanCapture) ResumeCapture();
            StateChanged?.Invoke();
        }

        private void SuspendCapture()
        {
            captureVersion++;
            microphone?.StopRecording();
            // Flush emits the unfinished utterance. Mute must discard it instead.
            ResetVad();
            StateChanged?.Invoke();
        }

        private void ResetVad()
        {
            vadWindowPosition = 0;
            InputLevel = 0f;
            if (!initializing) vad?.Reset();
        }

        private async void ResumeCapture()
        {
            try { await StartListeningAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (disposed) return;
                Status = $"Microphone error: {exception.Message}";
                Debug.LogWarning($"[VOICE] {Status}", this);
                StateChanged?.Invoke();
            }
        }

        public async Task<bool> StartListeningAsync()
        {
            if (!CanCapture || !IsReady || microphone == null || processing || starting) return false;
            if (microphone.IsRecording) return true;
            starting = true;
            var version = captureVersion;
            try
            {
                ResetVad();
                var started = await microphone.StartRecordingAsync(lifetime);
                if (!CanCapture || version != captureVersion)
                {
                    microphone.StopRecording();
                    return false;
                }
                Status = started ? "Listening" : "Microphone unavailable: check audio permission, then toggle X to retry";
                StateChanged?.Invoke();
                return started;
            }
            finally
            {
                starting = false;
                if (disposed && !processing && !initializing) DisposeServices();
                else if (CanCapture && version != captureVersion) ResumeCapture();
            }
        }

        public void StopListening()
        {
            SetMuted(true);
        }

        private void HandleSamples(float[] samples)
        {
            if (!CanCapture || processing || brain == null || !brain.CanProcessInput) return;
            var playback = brain.CurrentState == AgentState.Speaking;
            if (playback != suppressingPlayback) ResetVad();
            suppressingPlayback = playback;
            if (playback) return;
            var sum = 0f;
            if (samples == null || samples.Length == 0) return;
            for (var i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            InputLevel = Mathf.Sqrt(sum / samples.Length);
            FeedVad(samples);
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
                vadWindowPosition = 0;
                vad.AcceptWaveform(vadWindow);
                // Sherpa 0.2 only auto-drains while speech is active. The final
                // segment becomes available AFTER silence, so drain every window.
                vad.DrainSegments();
                if (processing || !CanCapture) break;
            }
        }

        private async void HandleSegment(VadSegment segment)
        {
            if (!CanCapture || processing || segment?.Samples == null || segment.Samples.Length == 0 || brain == null || !brain.CanProcessInput) return;
            processing = true;
            var version = captureVersion;
            microphone?.StopRecording();
            Status = "Recognizing";
            try
            {
                var sampleRate = vad.ActiveProfile?.sampleRate ?? 16000;
                var result = await asr.RecognizeAsync(segment.Samples, sampleRate);
                if (CanCapture && version == captureVersion && result != null && result.IsValid)
                {
                    LastTranscript = result.Text;
                    Debug.Log($"[VOICE] Heard: {result.Text}", this);
                    Status = $"Heard: {result.Text}";
                    // ASR owns the capture gate only until transcription completes.
                    // A later utterance can now interrupt inference or movement.
                    _ = brain.ProcessUserMessageAsync(result.Text, lifetime);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!disposed)
                {
                    Status = $"Voice error: {exception.Message}";
                    Debug.LogWarning($"[VOICE] {Status}", this);
                }
            }
            finally
            {
                processing = false;
                if (disposed) DisposeServices();
                else
                {
                    ResetVad();
                    if (CanCapture) ResumeCapture();
                    StateChanged?.Invoke();
                }
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
