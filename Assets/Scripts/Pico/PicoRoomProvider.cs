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
        Task<IReadOnlyList<PicoSemanticObject>> QuerySemanticObjectsAsync(
            CancellationToken cancellationToken);
    }

    public sealed class PicoRoomProvider : MonoBehaviour, IRoomProvider
    {
        [Tooltip("PICO Scene Capture adapter used by the production scene.")]
        [SerializeField] private MonoBehaviour picoSceneSourceComponent;
        [SerializeField] private ManualRoomProvider editorFallback;

        public bool IsReady { get; private set; }
        public RoomGraph Current { get; private set; }

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
                Current = new RoomGraph();
                IsReady = true;
                return Current;
            }
            Current = new RoomGraph();
            if (semanticObjects != null)
            {
                for (var i = 0; i < semanticObjects.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddSemanticObject(semanticObjects[i]);
                }
            }

            IsReady = true;
            Debug.Log($"[PICO] Normalized {Current.Count} Scene Capture objects.", this);
            return Current;
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
            switch ((label ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "floor": return RoomNodeType.Floor;
                case "wall": return RoomNodeType.Wall;
                case "sofa":
                case "couch": return RoomNodeType.Sofa;
                case "chair": return RoomNodeType.Chair;
                case "table": return RoomNodeType.Table;
                case "bed": return RoomNodeType.Bed;
                case "cabinet": return RoomNodeType.Cabinet;
                case "door": return RoomNodeType.Door;
                case "window": return RoomNodeType.Window;
                default: return RoomNodeType.Unknown;
            }
        }
    }
}
