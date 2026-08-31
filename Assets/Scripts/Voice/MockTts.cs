using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MockTts : MonoBehaviour, ITts
    {
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private bool simulateSpeechDuration;
        [SerializeField, Range(5f, 30f)] private float charactersPerSecond = 16f;

        public bool IsReady => true;
        public AudioSource VoiceSource => voiceSource;

        private void Awake()
        {
            if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
        }

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(text)) return;
            Debug.Log($"[VOICE] Mock TTS: {text}", this);
            if (!simulateSpeechDuration) return;

            var seconds = Math.Min(8f, text.Length / Math.Max(1f, charactersPerSecond));
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        public void Stop()
        {
            if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
        }
    }
}
