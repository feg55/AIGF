using System.Threading;
using System.Threading.Tasks;

namespace Aigf.Companion.Room
{
    public interface IRoomProvider
    {
        bool IsReady { get; }
        RoomGraph Current { get; }
        Task<RoomGraph> LoadAsync(CancellationToken cancellationToken = default);
    }
}
