using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.Pico
{
    public sealed class PicoSemanticObject
    {
        public string Id;
        public string SemanticLabel;
        public Transform Anchor;
        public Collider BoundsSource;
        public Transform ApproachPoint;
        public InteractionAnchor Interaction;
    }

    public interface IPicoSceneSource
    {
        bool IsSceneCaptureAvailable { get; }
        event Action SceneDataChanged;
        Task<IReadOnlyList<PicoSemanticObject>> QuerySemanticObjectsAsync(
            CancellationToken cancellationToken);
    }

    public sealed class PicoRoomProvider : MonoBehaviour, IRoomProvider, IRoomUpdateSource
    {
        [Tooltip("PICO Scene Capture adapter used by the production scene.")]
        [SerializeField] private MonoBehaviour picoSceneSourceComponent;
        [SerializeField] private ManualRoomProvider editorFallback;

        public bool IsReady { get; private set; }
        public RoomGraph Current { get; private set; }
        public event Action<RoomGraph> RoomUpdated;

        private IPicoSceneSource subscribedSource;
        private bool refreshRunning;
        private bool refreshQueued;

        private void OnDestroy()
        {
            if (subscribedSource != null)
            {
                subscribedSource.SceneDataChanged -= HandleSceneDataChanged;
                subscribedSource = null;
            }
        }

        public async Task<RoomGraph> LoadAsync(CancellationToken cancellationToken = default)
        {
            var source = picoSceneSourceComponent as IPicoSceneSource;
            if (source == null || !source.IsSceneCaptureAvailable)
            {
                Debug.LogWarning("[PICO] Scene Capture adapter is unavailable.", this);
#if UNITY_EDITOR
                if (editorFallback != null)
                {
                    Debug.LogWarning("[PICO] Using the Editor-only manual room fallback.", this);
                    Current = await editorFallback.LoadAsync(cancellationToken);
                    IsReady = true;
                    return Current;
                }
#endif

                Current = new RoomGraph();
                IsReady = true;
                return Current;
            }

            IReadOnlyList<PicoSemanticObject> semanticObjects;
            try
            {
                semanticObjects = await source.QuerySemanticObjectsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PICO] Scene Capture failed; room actions are disabled: {exception.Message}", this);
                if (Current == null) Current = new RoomGraph();
                IsReady = true;
                SubscribeToUpdates(source);
                return Current;
            }

            // Keep the RoomGraph instance stable. GirlBrain and ActionExecutor retain this
            // reference, so in-place replacement makes newly discovered furniture visible
            // to planning and safety without reinitializing the whole application.
            if (Current == null) Current = new RoomGraph();
            else Current.Clear();
            if (semanticObjects != null)
            {
                for (var i = 0; i < semanticObjects.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddSemanticObject(semanticObjects[i]);
                }
            }

            IsReady = true;
            SubscribeToUpdates(source);
            var seatCount = 0;
            var floorCount = 0;
            for (var i = 0; i < Current.Nodes.Count; i++)
            {
                var node = Current.Nodes[i];
                if (node == null) continue;
                if (node.CanSit) seatCount++;
                if (node.Type == RoomNodeType.Floor) floorCount++;
            }
            Debug.Log(
                $"[PICO] Normalized {Current.Count} Scene Capture objects; " +
                $"{floorCount} floors, {seatCount} valid seats.",
                this);
            return Current;
        }

        private void SubscribeToUpdates(IPicoSceneSource source)
        {
            if (ReferenceEquals(subscribedSource, source)) return;
            if (subscribedSource != null)
            {
                subscribedSource.SceneDataChanged -= HandleSceneDataChanged;
            }

            subscribedSource = source;
            subscribedSource.SceneDataChanged += HandleSceneDataChanged;
        }

        private async void HandleSceneDataChanged()
        {
            refreshQueued = true;
            if (refreshRunning) return;

            refreshRunning = true;
            try
            {
                do
                {
                    refreshQueued = false;
                    var room = await LoadAsync(destroyCancellationToken);
                    RoomUpdated?.Invoke(room);
                }
                while (refreshQueued && !destroyCancellationToken.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PICO] Scene Capture refresh failed: {exception.Message}", this);
            }
            finally
            {
                refreshRunning = false;
            }
        }

        private void AddSemanticObject(PicoSemanticObject source)
        {
            if (source == null || source.Anchor == null)
            {
                return;
            }

            var type = MapType(source.SemanticLabel);
            var node = source.Anchor.GetComponent<RoomNode>();
            if (node == null)
            {
                node = source.Anchor.gameObject.AddComponent<RoomNode>();
            }

            var canSit = type == RoomNodeType.Sofa || type == RoomNodeType.Chair;
            node.Configure(
                source.Id,
                type,
                canSit,
                source.ApproachPoint,
                source.Interaction != null ? source.Interaction.SitPoint : source.Anchor,
                source.Interaction,
                source.BoundsSource,
                source.SemanticLabel);

            if (!Current.AddOrReplace(node, out var error))
            {
                Debug.LogWarning($"[PICO] Ignored semantic object: {error}", this);
            }
        }

        public static RoomNodeType MapType(string label)
        {
            var normalized = (label ?? string.Empty)
                .Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
            switch (normalized)
            {
                case "floor": return RoomNodeType.Floor;
                case "wall": return RoomNodeType.Wall;
                case "virtualwall": return RoomNodeType.Wall;
                case "sofa":
                case "couch": return RoomNodeType.Sofa;
                case "chair": return RoomNodeType.Chair;
                case "table": return RoomNodeType.Table;
                case "bed": return RoomNodeType.Bed;
                case "cabinet": return RoomNodeType.Cabinet;
                case "door": return RoomNodeType.Door;
                case "window": return RoomNodeType.Window;
                case "curtain":
                case "plant":
                case "screen":
                case "refrigerator":
                case "washingmachine":
                case "airconditioner":
                case "lamp":
                case "wallart": return RoomNodeType.OtherFurniture;
                default: return RoomNodeType.Unknown;
            }
        }
    }
}
