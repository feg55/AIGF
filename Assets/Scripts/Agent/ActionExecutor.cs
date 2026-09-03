using System;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.AI;
using Aigf.Companion.Avatar;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.Agent
{
    public sealed class ActionExecutor : MonoBehaviour
    {
        [SerializeField] private GirlNavigation navigation;
        [SerializeField] private GirlAnimator girlAnimator;
        [SerializeField] private AvatarInteraction avatarInteraction;
        [SerializeField] private LookAtUser lookAt;

        private RoomGraph room;
        private AppConfig config;

        public AgentState State { get; private set; } = AgentState.Idle;
        public string CurrentAction { get; private set; } = string.Empty;
        public string CurrentTarget { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        public event Action StateChanged;

        private void Awake()
        {
            if (navigation == null) navigation = GetComponent<GirlNavigation>();
            if (girlAnimator == null) girlAnimator = GetComponent<GirlAnimator>();
            if (avatarInteraction == null) avatarInteraction = GetComponent<AvatarInteraction>();
            if (lookAt == null) lookAt = GetComponent<LookAtUser>();
        }

        public void Configure(RoomGraph roomGraph, AppConfig appConfig)
        {
            room = roomGraph;
            config = appConfig != null ? appConfig : AppConfig.CreateRuntimeDefaults();
        }

        public Task<ActionResult> ExecuteAsync(AgentReply reply, CancellationToken cancellationToken)
        {
            if (!AgentJson.ValidateReply(reply, room, out var error, config != null ? config.MaxActionsPerReply : 5))
            {
                SetError(error);
                return Task.FromResult(ActionResult.Failure(error));
            }

            return ActionSequenceRunner.RunAsync(reply.Actions, ExecuteOneAsync, cancellationToken);
        }

        public bool TryPrepare(AgentReply reply, out string error)
        {
            if (!AgentJson.ValidateReply(reply, room, out error, config != null ? config.MaxActionsPerReply : 5))
            {
                return false;
            }

            for (var i = 0; i < reply.Actions.Count; i++)
            {
                var action = reply.Actions[i];
                if (!AgentSafety.ValidateAction(action, room, out error))
                {
                    return false;
                }

                switch (action.Type)
                {
                    case AgentActionTypes.WalkToUser:
                    case AgentActionTypes.FollowUser:
                    {
                        if (!EnsureNavigationReady(out error)) return false;
                        var distance = action.Type == AgentActionTypes.FollowUser
                            ? config != null ? config.FollowDistance : 1.2f
                            : action.Distance;
                        if (!navigation.TryResolveUserDestination(distance, out _))
                        {
                            error = "No reachable floor point exists near the user.";
                            return false;
                        }
                        break;
                    }

                    case AgentActionTypes.WalkTo:
                    {
                        if (!EnsureNavigationReady(out error)) return false;
                        if (!room.TryGetNode(action.Target, out var node) ||
                            !navigation.TryResolveRoomDestination(node, out _))
                        {
                            error = $"No reachable floor point exists beside '{action.Target}'.";
                            return false;
                        }
                        break;
                    }

                    case AgentActionTypes.Sit:
                    {
                        if (!room.TryGetNode(action.Target, out var seat) || avatarInteraction == null ||
                            !avatarInteraction.CanSit(seat, out error))
                        {
                            if (string.IsNullOrEmpty(error)) error = $"Cannot sit on '{action.Target}'.";
                            return false;
                        }
                        if (!EnsureNavigationReady(out error)) return false;
                        if (!navigation.TryResolveRoomDestination(seat, out _))
                        {
                            error = $"No reachable floor point exists beside seat '{action.Target}'.";
                            return false;
                        }
                        break;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private bool EnsureNavigationReady(out string error)
        {
            if (navigation == null)
            {
                error = "GirlNavigation is missing.";
                return false;
            }

            var placement = navigation.EnsurePlacedOnNavMesh();
            error = placement.Succeeded ? string.Empty : placement.Message;
            return placement.Succeeded;
        }

        private async Task<ActionResult> ExecuteOneAsync(AgentAction action, CancellationToken cancellationToken)
        {
            if (!AgentSafety.ValidateAction(action, room, out var error))
            {
                SetError(error);
                return ActionResult.Failure(error);
            }

            CurrentAction = action.Type;
            CurrentTarget = action.Target;
            SetState(AgentState.ExecutingAction);
            ActionResult result;

            switch (action.Type)
            {
                case AgentActionTypes.WalkToUser:
                    SetState(AgentState.Walking);
                    result = navigation != null
                        ? await navigation.WalkToUser(action.Distance, cancellationToken)
                        : ActionResult.Failure("GirlNavigation is missing.");
                    break;

                case AgentActionTypes.WalkTo:
                    SetState(AgentState.Walking);
                    result = room.TryGetNode(action.Target, out var walkNode) && navigation != null
                        ? await navigation.WalkToRoomNode(walkNode, cancellationToken)
                        : ActionResult.Failure($"Cannot navigate to '{action.Target}'.");
                    break;

                case AgentActionTypes.Sit:
                    result = room.TryGetNode(action.Target, out var seatNode) && avatarInteraction != null
                        ? await avatarInteraction.SitAsync(seatNode, cancellationToken)
                        : ActionResult.Failure($"Cannot sit on '{action.Target}'.");
                    if (result.Succeeded) SetState(AgentState.Sitting);
                    break;

                case AgentActionTypes.Stand:
                    result = avatarInteraction != null
                        ? await avatarInteraction.StandAsync(cancellationToken)
                        : ActionResult.Failure("AvatarInteraction is missing.");
                    break;

                case AgentActionTypes.FollowUser:
                    result = navigation != null
                        ? navigation.FollowUser(cancellationToken)
                        : ActionResult.Failure("GirlNavigation is missing.");
                    if (result.Succeeded) SetState(AgentState.Following);
                    break;

                case AgentActionTypes.Stop:
                    navigation?.Stop();
                    result = ActionResult.Success("Stopped.");
                    break;

                case AgentActionTypes.LookAtUser:
                    if (lookAt == null)
                    {
                        result = ActionResult.Failure("LookAtUser is missing.");
                    }
                    else
                    {
                        lookAt.LookAtUserNow();
                        result = ActionResult.Success();
                    }
                    break;

                case AgentActionTypes.LookAt:
                    if (lookAt != null && room.TryGetNode(action.Target, out var lookNode))
                    {
                        lookAt.LookAt(lookNode.InteractionPoint);
                        result = ActionResult.Success();
                    }
                    else
                    {
                        result = ActionResult.Failure($"Cannot look at '{action.Target}'.");
                    }
                    break;

                case AgentActionTypes.Wave:
                    girlAnimator?.Wave();
                    result = ActionResult.Success();
                    break;

                case AgentActionTypes.PlayAnimation:
                    result = girlAnimator != null && girlAnimator.PlayNamedAnimation(action.Animation)
                        ? ActionResult.Success()
                        : ActionResult.Failure($"Animation '{action.Animation}' is not allowed or unavailable.");
                    break;

                default:
                    result = ActionResult.Failure($"Unsupported action '{action.Type}'.");
                    break;
            }

            if (!result.Succeeded)
            {
                SetError(result.Message);
                SetState(avatarInteraction != null && avatarInteraction.IsSeated ? AgentState.Sitting : AgentState.Idle);
                Debug.LogWarning($"[ACTION] {action.Type} failed: {result.Message}", this);
                return result;
            }

            LastError = string.Empty;
            if (State != AgentState.Following && State != AgentState.Sitting)
            {
                SetState(avatarInteraction != null && avatarInteraction.IsSeated ? AgentState.Sitting : AgentState.Idle);
            }

            CurrentAction = string.Empty;
            if (State != AgentState.Following)
            {
                CurrentTarget = string.Empty;
            }

            return result;
        }

        private void SetState(AgentState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke();
        }

        private void SetError(string error)
        {
            LastError = error ?? string.Empty;
            StateChanged?.Invoke();
        }
    }
}
