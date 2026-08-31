using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    public sealed class MockStt : MonoBehaviour, IStt
    {
        [SerializeField] private string simulatedTranscript = "come here";

        public bool IsReady => true;

        public Task<string> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Log($"[VOICE] Mock STT: {simulatedTranscript}", this);
            return Task.FromResult(simulatedTranscript);
        }

        public void SetSimulatedTranscript(string value)
        {
            simulatedTranscript = value ?? string.Empty;
        }
    }
}
