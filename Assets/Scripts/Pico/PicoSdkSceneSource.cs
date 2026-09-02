using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Room;
using UnityEngine;
using Unity.XR.PXR;

namespace Aigf.Companion.Pico
{
    public sealed class PicoSdkSceneSource : MonoBehaviour, IPicoSceneSource
    {
        [SerializeField] private Transform generatedRoot;
        [SerializeField] private bool launchCaptureWhenRoomMissing = true;
        [SerializeField, Min(0.01f)] private float planeThickness = 0.04f;

        private bool providerStarted;

        public bool IsSceneCaptureAvailable => Application.platform == RuntimePlatform.Android;

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (providerStarted)
            {
                PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.SceneCapture);
            }
#endif
        }

        public async Task<IReadOnlyList<PicoSemanticObject>> QuerySemanticObjectsAsync(
            CancellationToken cancellationToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureGeneratedRoot();
            ClearGeneratedObjects();
            PXR_Manager.EnableVideoSeeThrough = true;

            var start = await PXR_MixedReality.StartSenseDataProvider(
                PxrSenseDataProviderType.SceneCapture,
                cancellationToken);
            if (start != PxrResult.SUCCESS)
            {
                throw new InvalidOperationException($"PICO Scene Capture provider failed to start: {start}");
            }

            providerStarted = true;
            var query = await QueryAnchorsAsync(cancellationToken);
            if (query.result == PxrResult.SUCCESS && query.handles.Count == 0 && launchCaptureWhenRoomMissing)
            {
                var capture = await PXR_MixedReality.StartSceneCaptureAsync(cancellationToken);
                if (capture == PxrResult.SUCCESS)
                {
                    query = await QueryAnchorsAsync(cancellationToken);
                }
            }

            if (query.result != PxrResult.SUCCESS)
            {
                throw new InvalidOperationException($"PICO Scene Capture query failed: {query.result}");
            }

            var result = new List<PicoSemanticObject>(query.handles.Count);
            for (var i = 0; i < query.handles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var semantic = CreateSemanticObject(query.handles[i]);
                if (semantic != null) result.Add(semantic);
            }

            return result;
#else
            _ = launchCaptureWhenRoomMissing;
            _ = planeThickness;
            await Task.Yield();
            return Array.Empty<PicoSemanticObject>();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<(PxrResult result, List<ulong> handles)> QueryAnchorsAsync(
            CancellationToken cancellationToken)
        {
            var query = await PXR_MixedReality.QuerySceneAnchorAsync(
                (PxrSemanticLabel[])null,
                cancellationToken);
            return (query.result, query.anchorHandleList ?? new List<ulong>());
        }

        private PicoSemanticObject CreateSemanticObject(ulong handle)
        {
            if (PXR_MixedReality.GetSceneSemanticLabel(handle, out var label) != PxrResult.SUCCESS ||
                PXR_MixedReality.LocateAnchor(handle, out var position, out var rotation) != PxrResult.SUCCESS)
            {
                return null;
            }

            var anchorObject = new GameObject($"PICO_{label}_{handle}");
            anchorObject.transform.SetParent(generatedRoot, false);
            anchorObject.transform.SetPositionAndRotation(position, rotation);

            Collider bounds = null;
            Transform geometry = null;
            if (PXR_MixedReality.GetSceneBox3DData(handle, out var boxPosition, out var boxRotation, out var boxExtent) == PxrResult.SUCCESS)
            {
                geometry = CreateBoxGeometry(anchorObject.transform, boxPosition, boxRotation, boxExtent);
                bounds = geometry.GetComponent<Collider>();
            }
            else if (PXR_MixedReality.GetSceneBox2DData(handle, out var offset, out var extent2D) == PxrResult.SUCCESS)
            {
                geometry = CreateBoxGeometry(
                    anchorObject.transform,
                    new Vector3(offset.x, offset.y, 0f),
                    Quaternion.identity,
                    new Vector3(extent2D.x, extent2D.y, planeThickness));
                bounds = geometry.GetComponent<Collider>();
            }
            else if (PXR_MixedReality.GetScenePolygonData(handle, out var polygon) == PxrResult.SUCCESS && polygon.Length >= 3)
            {
                geometry = CreatePolygonBounds(anchorObject.transform, polygon);
                bounds = geometry.GetComponent<Collider>();
            }

            var approach = CreateApproachPoint(anchorObject.transform, geometry, label);
            var interaction = CreateInteractionAnchor(anchorObject.transform, geometry, approach, label);
            return new PicoSemanticObject
            {
                Id = $"pico_{handle}",
                SemanticLabel = label.ToString(),
                Anchor = anchorObject.transform,
                BoundsSource = bounds,
                ApproachPoint = approach,
                Interaction = interaction
            };
        }

        private Transform CreateBoxGeometry(Transform parent, Vector3 position, Quaternion rotation, Vector3 extent)
        {
            var geometry = new GameObject("CollisionGeometry").transform;
            geometry.SetParent(parent, false);
            geometry.localPosition = position;
            geometry.localRotation = rotation;
            var collider = geometry.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(planeThickness, Mathf.Abs(extent.x)),
                Mathf.Max(planeThickness, Mathf.Abs(extent.y)),
                Mathf.Max(planeThickness, Mathf.Abs(extent.z)));
            return geometry;
        }

        private Transform CreatePolygonBounds(Transform parent, IReadOnlyList<Vector2> polygon)
        {
            var min = polygon[0];
            var max = polygon[0];
            for (var i = 1; i < polygon.Count; i++)
            {
                min = Vector2.Min(min, polygon[i]);
                max = Vector2.Max(max, polygon[i]);
            }

            var center = (min + max) * 0.5f;
            var size = max - min;
            return CreateBoxGeometry(
                parent,
                new Vector3(center.x, center.y, -planeThickness * 0.5f),
                Quaternion.identity,
                new Vector3(size.x, size.y, planeThickness));
        }

        private static Transform CreateApproachPoint(Transform anchor, Transform geometry, PxrSemanticLabel label)
        {
            var point = new GameObject("ApproachPoint").transform;
            point.SetParent(anchor, false);
            if (geometry != null && geometry.TryGetComponent<BoxCollider>(out var collider))
            {
                var localOffset = collider.center + new Vector3(
                    0f,
                    -Mathf.Abs(collider.size.y) * 0.5f,
                    -Mathf.Abs(collider.size.z) * 0.5f - 0.45f);
                point.localPosition = geometry.localPosition + geometry.localRotation * localOffset;
                point.localRotation = geometry.localRotation;
            }
            return point;
        }

        private static InteractionAnchor CreateInteractionAnchor(
            Transform anchor,
            Transform geometry,
            Transform approach,
            PxrSemanticLabel label)
        {
            if (label != PxrSemanticLabel.Sofa && label != PxrSemanticLabel.Chair) return null;
            if (geometry == null || !geometry.TryGetComponent<BoxCollider>(out var collider)) return null;

            var objectHeight = Mathf.Abs(collider.size.y);
            var objectDepth = Mathf.Abs(collider.size.z);
            var seatHeightFromBottom = label == PxrSemanticLabel.Sofa
                ? Mathf.Clamp(objectHeight * 0.45f, 0.38f, 0.52f)
                : Mathf.Clamp(objectHeight * 0.48f, 0.4f, 0.58f);
            var seatOffset = collider.center + new Vector3(
                0f,
                -objectHeight * 0.5f + seatHeightFromBottom,
                -Mathf.Min(0.18f, objectDepth * 0.15f));

            var sit = new GameObject("SitPoint").transform;
            sit.SetParent(anchor, false);
            sit.localPosition = geometry.localPosition + geometry.localRotation * seatOffset;
            sit.localRotation = geometry.localRotation * Quaternion.Euler(0f, 180f, 0f);
            var interaction = anchor.gameObject.AddComponent<InteractionAnchor>();
            interaction.Configure(approach, sit, sit);
            return interaction;
        }
#endif

        private void EnsureGeneratedRoot()
        {
            if (generatedRoot != null) return;
            generatedRoot = new GameObject("PICO Scene Capture Geometry").transform;
            generatedRoot.SetParent(transform, false);
        }

        private void ClearGeneratedObjects()
        {
            for (var i = generatedRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(generatedRoot.GetChild(i).gameObject);
            }
        }
    }
}
