using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    public interface IStt
    {
        bool IsReady { get; }
        Task<string> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken = default);
    }
}
