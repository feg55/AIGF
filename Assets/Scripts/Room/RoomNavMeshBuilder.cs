using Unity.AI.Navigation;
using UnityEngine;

namespace Aigf.Companion.Room
{
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class RoomNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface surface;

        private void Awake()
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
        }

        public void Rebuild(RoomGraph room)
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
            if (surface == null || room == null || room.Count == 0)
            {
                Debug.LogWarning("[ROOM] NavMesh was not built because no room geometry is available.", this);
                return;
            }

            surface.RemoveData();
            surface.BuildNavMesh();
            Debug.Log($"[ROOM] Runtime NavMesh rebuilt from {room.Count} semantic nodes.", this);
        }
    }
}
