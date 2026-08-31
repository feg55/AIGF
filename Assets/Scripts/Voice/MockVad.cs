using UnityEngine;

namespace Aigf.Companion.Voice
{
    public sealed class MockVad : MonoBehaviour, IVad
    {
        [SerializeField, Range(0.0001f, 0.1f)] private float rmsThreshold = 0.008f;

        public bool ContainsSpeech(float[] samples, int sampleCount, int sampleRate)
        {
            if (samples == null || sampleCount <= 0) return false;
            var count = Mathf.Min(sampleCount, samples.Length);
            var sum = 0f;
            for (var i = 0; i < count; i++) sum += samples[i] * samples[i];
            return Mathf.Sqrt(sum / count) >= rmsThreshold;
        }
    }
}
