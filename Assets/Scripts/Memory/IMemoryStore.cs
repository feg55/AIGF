using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aigf.Companion.Memory
{
    public interface IMemoryStore
    {
        Task AddAsync(MemoryItem item, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
