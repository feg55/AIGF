using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Aigf.Companion.Room
{
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class RoomNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface surface;
        [SerializeField, Min(0.03f)] private float navigationFloorThickness = 0.06f;
        [SerializeField, Min(1f)] private float minimumFallbackFloorSize = 4f;

        private GameObject navigationFloorRoot;

        private void Awake()
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
        }

        private void OnDestroy()
        {
            if (navigationFloorRoot != null) Destroy(navigationFloorRoot);
        }

        public bool Rebuild(RoomGraph room)
        {
            if (surface == null) surface = GetComponent<NavMeshSurface>();
            if (surface == null || room == null)
            {
                Debug.LogWarning("[ROOM] NavMesh was not built because no room geometry is available.", this);
                return false;
            }

            ConfigureSurfaceForRoomScale();
            var capturedFloorCount = PrepareNavigationGeometry(room);
            Physics.SyncTransforms();
            surface.RemoveData();
            surface.BuildNavMesh();
            var triangulation = NavMesh.CalculateTriangulation();
            var built = triangulation.vertices != null && triangulation.vertices.Length >= 3;

            // A malformed PICO floor anchor can survive the semantic query but still
            // produce no walkable polygons. Retry from room/player bounds instead of
            // leaving every movement command permanently disabled.
            if (!built && capturedFloorCount > 0)
            {
                ResetNavigationFloorRoot();
                CreateHorizontalFloorProxy(
                    CalculateConservativeFallbackBounds(room),
                    null,
                    "RecoveredFallbackFloor");
                Physics.SyncTransforms();
                surface.RemoveData();
                surface.BuildNavMesh();
                triangulation = NavMesh.CalculateTriangulation();
                built = triangulation.vertices != null && triangulation.vertices.Length >= 3;
                if (built)
                {
                    Debug.LogWarning(
                        "[ROOM] Captured Floor geometry was invalid. Navigation recovered from room bounds.",
                        this);
                }
            }

            if (!built)
            {
                Debug.LogError(
                    "[ROOM] Runtime NavMesh is empty even after the fallback floor was generated.",
                    this);
                return false;
            }

            Debug.Log(
                $"[ROOM] Runtime NavMesh rebuilt from {room.Count} semantic nodes " +
                $"and {capturedFloorCount} captured floor anchors " +
                $"({triangulation.vertices.Length} vertices).",
                this);
            return true;
        }

        private void ConfigureSurfaceForRoomScale()
        {
            // The Unity Humanoid defaults (0.5 m radius, 2 m height and 2 square metre
            // regions) erase most walkable space in a furnished room.
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.minRegionArea = 0.25f;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f;
        }

        private int PrepareNavigationGeometry(RoomGraph room)
        {
            ResetNavigationFloorRoot();
            var floorCount = 0;
            var notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");

            for (var i = 0; i < room.Nodes.Count; i++)
            {
                var node = room.Nodes[i];
                if (node == null) continue;

                if (node.Type == RoomNodeType.Floor)
                {
                    if (CreateHorizontalFloorProxy(node.Bounds, node.Anchor, $"CapturedFloor_{floorCount}"))
                    {
                        floorCount++;
                    }
                    continue;
                }

                // Furniture and walls carve the generated floor instead of becoming
                // walkable surfaces on their top faces.
                var boundsSource = node.BoundsSource;
                if (boundsSource == null || boundsSource.isTrigger || notWalkableArea < 0) continue;
                var modifier = boundsSource.GetComponent<NavMeshModifier>();
                if (modifier == null) modifier = boundsSource.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = notWalkableArea;
            }

            if (floorCount == 0)
            {
                var fallback = CalculateConservativeFallbackBounds(room);
                CreateHorizontalFloorProxy(fallback, null, "FallbackFloor");
                Debug.LogWarning(
                    "[ROOM] PICO returned no Floor anchor. Built a conservative navigation floor " +
                    "from the captured room bounds so voice commands remain usable.",
                    this);
            }

            return floorCount;
        }

        private void ResetNavigationFloorRoot()
        {
            if (navigationFloorRoot != null)
            {
                navigationFloorRoot.SetActive(false);
                Destroy(navigationFloorRoot);
            }

            navigationFloorRoot = new GameObject("Runtime Navigation Floors");
            navigationFloorRoot.transform.SetParent(transform, false);
        }

        private bool CreateHorizontalFloorProxy(Bounds source, Transform anchor, string objectName)
        {
            if (!IsFinite(source.center) || !IsFinite(source.size)) return false;

            var width = Mathf.Abs(source.size.x);
            var depth = Mathf.Abs(source.size.z);
            var sourceLooksVertical = depth < 0.25f || width < 0.25f;
            if (sourceLooksVertical)
            {
                // Some PICO 4 legacy captures expose the second plane extent on Y.
                // Recover the two plane dimensions and still emit a world-horizontal floor.
                var dimensions = new[]
                {
                    Mathf.Abs(source.size.x),
                    Mathf.Abs(source.size.y),
                    Mathf.Abs(source.size.z)
                };
                Array.Sort(dimensions);
                width = dimensions[2];
                depth = dimensions[1];
            }

            width = Mathf.Max(0.5f, width);
            depth = Mathf.Max(0.5f, depth);
            var floorY = sourceLooksVertical && anchor != null
                ? anchor.position.y
                : source.max.y;

            var floor = new GameObject(objectName);
            floor.transform.SetParent(navigationFloorRoot.transform, false);
            floor.transform.SetPositionAndRotation(
                new Vector3(source.center.x, floorY - navigationFloorThickness * 0.5f, source.center.z),
                Quaternion.identity);
            var collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, navigationFloorThickness, depth);
            return true;
        }

        private Bounds CalculateConservativeFallbackBounds(RoomGraph room)
        {
            var initialized = false;
            var minX = 0f;
            var maxX = 0f;
            var minZ = 0f;
            var maxZ = 0f;
            var floorY = float.PositiveInfinity;

            for (var i = 0; i < room.Nodes.Count; i++)
            {
                var node = room.Nodes[i];
                if (node == null) continue;
                var bounds = node.Bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.size)) continue;

                if (!initialized)
                {
                    minX = bounds.min.x;
                    maxX = bounds.max.x;
                    minZ = bounds.min.z;
                    maxZ = bounds.max.z;
                    initialized = true;
                }
                else
                {
                    minX = Mathf.Min(minX, bounds.min.x);
                    maxX = Mathf.Max(maxX, bounds.max.x);
                    minZ = Mathf.Min(minZ, bounds.min.z);
                    maxZ = Mathf.Max(maxZ, bounds.max.z);
                }
                floorY = Mathf.Min(floorY, bounds.min.y);
            }

            var camera = Camera.main;
            if (camera != null)
            {
                var position = camera.transform.position;
                if (!initialized)
                {
                    minX = maxX = position.x;
                    minZ = maxZ = position.z;
                    initialized = true;
                }
                else
                {
                    minX = Mathf.Min(minX, position.x);
                    maxX = Mathf.Max(maxX, position.x);
                    minZ = Mathf.Min(minZ, position.z);
                    maxZ = Mathf.Max(maxZ, position.z);
                }

                if (float.IsPositiveInfinity(floorY)) floorY = position.y - 1.65f;
            }

            if (!initialized)
            {
                minX = minZ = -minimumFallbackFloorSize * 0.5f;
                maxX = maxZ = minimumFallbackFloorSize * 0.5f;
            }
            if (float.IsPositiveInfinity(floorY)) floorY = transform.position.y;

            // Leave enough space for a 0.24 m agent to stand outside a sofa even when
            // the only geometry returned by the PICO 4 legacy backend is that sofa.
            const float margin = 1.25f;
            var width = Mathf.Max(minimumFallbackFloorSize, maxX - minX + margin * 2f);
            var depth = Mathf.Max(minimumFallbackFloorSize, maxZ - minZ + margin * 2f);
            return new Bounds(
                new Vector3((minX + maxX) * 0.5f, floorY - navigationFloorThickness * 0.5f,
                    (minZ + maxZ) * 0.5f),
                new Vector3(width, navigationFloorThickness, depth));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
