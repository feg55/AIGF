using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

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

        public bool Rebuild(RoomGraph room)
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
            if (surface == null || room == null || room.Count == 0)
            {
                Debug.LogWarning("[ROOM] NavMesh was not built because no room geometry is available.", this);
                return false;
            }

            surface.RemoveData();
            surface.BuildNavMesh();
            var triangulation = NavMesh.CalculateTriangulation();
            var built = triangulation.vertices != null && triangulation.vertices.Length >= 3;
            if (!built)
            {
                Debug.LogError("[ROOM] Runtime NavMesh is empty. Check that Scene Capture contains a floor.", this);
                return false;
            }

            Debug.Log(
                $"[ROOM] Runtime NavMesh rebuilt from {room.Count} semantic nodes " +
                $"({triangulation.vertices.Length} vertices).",
                this);
            return true;
        }
    }
}
