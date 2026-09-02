using System;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class AvatarInteraction : MonoBehaviour
    {
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private GirlNavigation navigation;
        [SerializeField] private GirlAnimator girlAnimator;
        [Header("Seat alignment")]
        [SerializeField, Range(0f, 0.2f)] private float hipsAboveSeatSurface = 0.08f;
        [SerializeField, Range(0.1f, 3f)] private float seatAlignmentSpeed = 1.2f;
        [SerializeField, Range(0f, 0.5f)] private float maxVerticalRootCorrection = 0.4f;
        [SerializeField, Range(0f, 0.6f)] private float maxHorizontalRootCorrection = 0.45f;

        private AppConfig config;
        private RoomNode seatedNode;
        private InteractionAnchor activeSeatAnchor;
        private Vector3 seatedRootReference;
        private bool maintainSeatAlignment;

        public bool IsSeated => seatedNode != null;
        public RoomNode SeatedNode => seatedNode;

        private void Awake()
        {
            if (avatarRoot == null) avatarRoot = transform;
            if (navigation == null) navigation = GetComponent<GirlNavigation>();
            if (girlAnimator == null) girlAnimator = GetComponent<GirlAnimator>();
        }

        private void LateUpdate()
        {
            if (maintainSeatAlignment)
            {
                AlignHipsToSeat(activeSeatAnchor, seatedRootReference, true);
            }
        }

        public void Configure(AppConfig appConfig)
        {
            config = appConfig != null ? appConfig : AppConfig.CreateRuntimeDefaults();
        }

        public bool CanSit(RoomNode node, out string reason)
        {
            if (node == null)
            {
                reason = "Seat target is missing.";
                return false;
            }

            if (!node.CanSit || node.InteractionAnchor == null || !node.InteractionAnchor.HasSittingGeometry)
            {
                reason = $"Room node '{node.Id}' has no valid sitting geometry.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public async Task<ActionResult> SitAsync(RoomNode node, CancellationToken cancellationToken)
        {
            if (!CanSit(node, out var reason))
            {
                return ActionResult.Failure(reason);
            }

            if (navigation == null)
            {
                return ActionResult.Failure("GirlNavigation is missing.");
            }

            var moveResult = await navigation.WalkToRoomNode(node, cancellationToken);
            if (!moveResult.Succeeded)
            {
                return moveResult;
            }

            navigation.Stop();
            navigation.SetNavigationEnabled(false);
            var anchor = node.InteractionAnchor;
            var approachRootPosition = avatarRoot.position;
            var seatedReferencePosition = anchor.SitPoint.position;
            seatedReferencePosition.y = approachRootPosition.y;
            activeSeatAnchor = anchor;
            seatedRootReference = seatedReferencePosition;
            maintainSeatAlignment = false;
            avatarRoot.rotation = anchor.SitRotation;

            girlAnimator?.Sit();
            var configuredDuration = config != null ? config.SitTransitionSeconds : 0.35f;
            var authoredDuration = girlAnimator != null ? girlAnimator.SitTransitionDuration : 0f;
            try
            {
                await WaitForSitAndAlignAsync(
                    Mathf.Max(configuredDuration, authoredDuration),
                    anchor,
                    approachRootPosition,
                    seatedReferencePosition,
                    cancellationToken);
            }
            catch
            {
                maintainSeatAlignment = false;
                activeSeatAnchor = null;
                girlAnimator?.Stand();
                navigation.ResumeAt(anchor.ApproachPoint != null
                    ? anchor.ApproachPoint.position
                    : approachRootPosition);
                throw;
            }

            seatedNode = node;
            maintainSeatAlignment = true;
            AlignHipsToSeat(activeSeatAnchor, seatedRootReference, true);
            return ActionResult.Success($"Seated on '{node.Id}'.");
        }

        public async Task<ActionResult> StandAsync(CancellationToken cancellationToken)
        {
            if (seatedNode == null)
            {
                return ActionResult.Success("Avatar is already standing.");
            }

            var previousSeat = seatedNode;
            if (navigation == null)
            {
                return ActionResult.Failure("GirlNavigation is missing.");
            }

            var requestedStandingPosition = CalculateStandingPosition(previousSeat);
            if (!navigation.TryResolveNavigablePosition(
                    requestedStandingPosition,
                    out var standingPosition))
            {
                return ActionResult.Failure("No safe NavMesh point exists near the seat exit.");
            }

            var standStartPosition = avatarRoot.position;
            maintainSeatAlignment = false;
            girlAnimator?.Stand();
            var configuredDuration = config != null ? config.StandTransitionSeconds : 0.35f;
            var authoredDuration = girlAnimator != null ? girlAnimator.StandTransitionDuration : 0f;
            try
            {
                await WaitForStandAndMoveAsync(
                    Mathf.Max(configuredDuration, authoredDuration),
                    standStartPosition,
                    standingPosition,
                    cancellationToken);
            }
            catch
            {
                girlAnimator?.Sit();
                maintainSeatAlignment = true;
                throw;
            }

            var result = navigation.ResumeAtResolved(standingPosition);

            if (result.Succeeded)
            {
                seatedNode = null;
                activeSeatAnchor = null;
            }

            return result;
        }

        private async Task WaitForSitAndAlignAsync(
            float seconds,
            InteractionAnchor anchor,
            Vector3 approachRootPosition,
            Vector3 seatedReferencePosition,
            CancellationToken cancellationToken)
        {
            var duration = Mathf.Max(0.01f, seconds);
            var startedAt = Time.realtimeSinceStartup;
            var previousReferencePosition = approachRootPosition;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = (Time.realtimeSinceStartup - startedAt) / duration;
                var travelProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.05f, 0.82f, progress));
                var referencePosition = Vector3.Lerp(
                    approachRootPosition,
                    seatedReferencePosition,
                    travelProgress);
                avatarRoot.position += referencePosition - previousReferencePosition;
                previousReferencePosition = referencePosition;

                if (progress >= 0.45f)
                {
                    AlignHipsToSeat(anchor, seatedReferencePosition, false);
                }
                await Task.Yield();
            }

            avatarRoot.position += seatedReferencePosition - previousReferencePosition;
            AlignHipsToSeat(anchor, seatedReferencePosition, true);
        }

        private async Task WaitForStandAndMoveAsync(
            float seconds,
            Vector3 seatedPosition,
            Vector3 standingPosition,
            CancellationToken cancellationToken)
        {
            var duration = Mathf.Max(0.01f, seconds);
            var startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var progress = (Time.realtimeSinceStartup - startedAt) / duration;
                var horizontalProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.02f, 0.58f, progress));
                var verticalProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.18f, 0.82f, progress));
                avatarRoot.position = new Vector3(
                    Mathf.Lerp(seatedPosition.x, standingPosition.x, horizontalProgress),
                    Mathf.Lerp(seatedPosition.y, standingPosition.y, verticalProgress),
                    Mathf.Lerp(seatedPosition.z, standingPosition.z, horizontalProgress));
                await Task.Yield();
            }

            avatarRoot.position = standingPosition;
        }

        private Vector3 CalculateStandingPosition(RoomNode seat)
        {
            if (seat == null || seat.ApproachPoint == null)
            {
                return avatarRoot.position;
            }

            var position = Vector3.Lerp(
                seatedRootReference,
                seat.ApproachPoint.position,
                0.55f);
            position.y = seat.ApproachPoint.position.y;
            return position;
        }

        private void AlignHipsToSeat(
            InteractionAnchor anchor,
            Vector3 initialRootPosition,
            bool finishImmediately)
        {
            if (anchor == null || anchor.SitPoint == null || girlAnimator == null ||
                !girlAnimator.TryGetHips(out var hips))
            {
                return;
            }

            var desiredHips = anchor.SitPoint.position + Vector3.up * hipsAboveSeatSurface;
            var correction = desiredHips - hips.position;
            if (!finishImmediately)
            {
                var maxStep = seatAlignmentSpeed * Mathf.Max(0.001f, Time.unscaledDeltaTime);
                correction = Vector3.ClampMagnitude(correction, maxStep);
            }

            var correctedRoot = avatarRoot.position + correction;
            correctedRoot.y = Mathf.Clamp(
                correctedRoot.y,
                initialRootPosition.y - 0.05f,
                initialRootPosition.y + maxVerticalRootCorrection);

            var horizontal = new Vector2(
                correctedRoot.x - initialRootPosition.x,
                correctedRoot.z - initialRootPosition.z);
            horizontal = Vector2.ClampMagnitude(horizontal, maxHorizontalRootCorrection);
            correctedRoot.x = initialRootPosition.x + horizontal.x;
            correctedRoot.z = initialRootPosition.z + horizontal.y;
            avatarRoot.position = correctedRoot;
        }
    }
}
