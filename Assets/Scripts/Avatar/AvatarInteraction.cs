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

        private AppConfig config;
        private RoomNode seatedNode;

        public bool IsSeated => seatedNode != null;
        public RoomNode SeatedNode => seatedNode;

        private void Awake()
        {
            if (avatarRoot == null) avatarRoot = transform;
            if (navigation == null) navigation = GetComponent<GirlNavigation>();
            if (girlAnimator == null) girlAnimator = GetComponent<GirlAnimator>();
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
            girlAnimator?.Sit();
            await WaitRealtimeAsync(config != null ? config.SitTransitionSeconds : 0.35f, cancellationToken);

            navigation.SetNavigationEnabled(false);
            avatarRoot.SetPositionAndRotation(
                node.InteractionAnchor.SitPoint.position,
                node.InteractionAnchor.SitRotation);
            seatedNode = node;
            return ActionResult.Success($"Seated on '{node.Id}'.");
        }

        public async Task<ActionResult> StandAsync(CancellationToken cancellationToken)
        {
            if (seatedNode == null)
            {
                return ActionResult.Success("Avatar is already standing.");
            }

            var previousSeat = seatedNode;
            girlAnimator?.Stand();
            await WaitRealtimeAsync(config != null ? config.StandTransitionSeconds : 0.35f, cancellationToken);

            var preferredPosition = previousSeat.ApproachPoint != null
                ? previousSeat.ApproachPoint.position
                : avatarRoot.position;
            var result = navigation != null
                ? navigation.ResumeAt(preferredPosition)
                : ActionResult.Failure("GirlNavigation is missing.");

            if (result.Succeeded)
            {
                seatedNode = null;
            }

            return result;
        }

        private static async Task WaitRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            var finishAt = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < finishAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}
