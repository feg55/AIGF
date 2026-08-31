using System.Threading;
using System.Threading.Tasks;

namespace Aigf.Companion.Voice
{
    public interface ITts
    {
        bool IsReady { get; }
        Task SpeakAsync(string text, CancellationToken cancellationToken = default);
        void Stop();
    }
}
