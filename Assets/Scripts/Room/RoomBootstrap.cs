using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Room
{
    public sealed class RoomBootstrap : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour providerComponent;

        public IRoomProvider Provider { get; private set; }
        public RoomGraph Room { get; private set; }

        public async Task<RoomGraph> InitializeAsync(CancellationToken cancellationToken = default)
        {
            Provider = providerComponent as IRoomProvider;
            if (Provider == null)
            {
                Provider = GetComponent<ManualRoomProvider>();
            }

            if (Provider == null)
            {
                throw new InvalidOperationException("[ROOM] No component implementing IRoomProvider is configured.");
            }

            Room = await Provider.LoadAsync(cancellationToken);
            return Room;
        }

        public void SetProvider(MonoBehaviour provider)
        {
            providerComponent = provider;
        }
    }
}
