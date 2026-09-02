using System;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using UnityEngine;
using UnityEngine.AI;

namespace Aigf.Companion.Avatar
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GirlNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Transform userHead;
        [SerializeField] private GirlAnimator girlAnimator;

        private AppConfig config;
        private CancellationTokenSource followCancellation;
        private Task followTask;

        public string CurrentTarget { get; private set; } = string.Empty;
        public bool IsFollowing { get; private set; }
        public bool IsNavigable => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
        public Transform UserHead => userHead;

        private void Awake()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (girlAnimator == null)
            {
                girlAnimator = GetComponent<GirlAnimator>();
            }
        }

        private void OnDestroy()
        {
            CancelFollowing();
        }

        public void Configure(AppConfig appConfig, Transform hmdTransform)
        {
            config = appConfig != null ? appConfig : AppConfig.CreateRuntimeDefaults();
            userHead = hmdTransform;
            if (navMeshAgent != null)
            {
                navMeshAgent.speed = config.MovementSpeed;
                navMeshAgent.angularSpeed = 240f;
                navMeshAgent.acceleration = Mathf.Max(2f, config.MovementSpeed * 3f);
            }
        }

        public Task<ActionResult> WalkToUser(CancellationToken cancellationToken)
        {
            if (userHead == null)
            {
                return Task.FromResult(ActionResult.Failure("User HMD transform is missing."));
            }

            var distance = config != null ? config.SocialDistance : 0.9f;
            CurrentTarget = "user";
            return MoveToAsync(CalculateUserDestination(distance), distance * 0.15f, cancellationToken);
        }

        public Task<ActionResult> WalkToUser(float requestedDistance, CancellationToken cancellationToken)
        {
            if (userHead == null)
            {
                return Task.FromResult(ActionResult.Failure("User HMD transform is missing."));
            }

            var fallback = config != null ? config.SocialDistance : 0.9f;
            var distance = requestedDistance > 0f ? Mathf.Clamp(requestedDistance, 0.6f, 2f) : fallback;
            CurrentTarget = "user";
            return MoveToAsync(CalculateUserDestination(distance), distance * 0.15f, cancellationToken);
        }

        public Task<ActionResult> WalkToRoomNode(RoomNode node, CancellationToken cancellationToken)
        {
            if (node == null)
            {
                return Task.FromResult(ActionResult.Failure("Room target is missing."));
            }

            if (node.ApproachPoint == null)
            {
                return Task.FromResult(ActionResult.Failure($"Room target '{node.Id}' has no approach point."));
            }

            CurrentTarget = node.Id;
            return MoveToAsync(node.ApproachPoint.position, 0.12f, cancellationToken);
        }

        public ActionResult FollowUser(CancellationToken cancellationToken)
        {
            if (userHead == null)
            {
                return ActionResult.Failure("User HMD transform is missing.");
            }

            if (!IsNavigable)
            {
                return ActionResult.Failure("Avatar is not placed on a NavMesh.");
            }

            CancelFollowing();
            followCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsFollowing = true;
            CurrentTarget = "user";
            followTask = FollowLoopAsync(followCancellation.Token);
            ObserveFollowTask(followTask);
            return ActionResult.Success("Following started.");
        }

        public void Stop()
        {
            CancelFollowing();
            StopAgent();
            CurrentTarget = string.Empty;
        }

        public void SetNavigationEnabled(bool enabled)
        {
            if (navMeshAgent == null)
            {
                return;
            }

            if (!enabled)
            {
                Stop();
                navMeshAgent.enabled = false;
                return;
            }

            navMeshAgent.enabled = true;
        }

        public ActionResult ResumeAt(Vector3 preferredPosition)
        {
            if (!TryResolveNavigablePosition(preferredPosition, out var resolvedPosition))
            {
                return ActionResult.Failure("No NavMesh point found near the standing position.");
            }

            return ResumeAtResolved(resolvedPosition);
        }

        public bool TryResolveNavigablePosition(Vector3 requestedPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = requestedPosition;
            if (navMeshAgent == null ||
                !NavMesh.SamplePosition(requestedPosition, out var hit, SampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            resolvedPosition = hit.position;
            return true;
        }

        public ActionResult ResumeAtResolved(Vector3 resolvedPosition)
        {
            if (navMeshAgent == null)
            {
                return ActionResult.Failure("NavMeshAgent is missing.");
            }

            navMeshAgent.enabled = true;
            if (!navMeshAgent.Warp(resolvedPosition))
            {
                return ActionResult.Failure("NavMeshAgent could not resume at the standing position.");
            }

            navMeshAgent.isStopped = false;
            return ActionResult.Success();
        }

        private async Task<ActionResult> MoveToAsync(
            Vector3 requestedDestination,
            float stoppingDistance,
            CancellationToken cancellationToken)
        {
            CancelFollowing();
            if (!IsNavigable)
            {
                return ActionResult.Failure("Avatar is not placed on a NavMesh.");
            }

            if (!NavMesh.SamplePosition(requestedDestination, out var hit, SampleRadius, NavMesh.AllAreas))
            {
                return ActionResult.Failure("No safe NavMesh point exists near the requested destination.");
            }

            navMeshAgent.stoppingDistance = Mathf.Max(0.05f, stoppingDistance);
            navMeshAgent.isStopped = false;
            if (!navMeshAgent.SetDestination(hit.position))
            {
                return ActionResult.Failure("NavMesh rejected the destination.");
            }

            girlAnimator?.SetWalking(true);
            var startedAt = Time.realtimeSinceStartup;
            try
            {
                while (navMeshAgent.pathPending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    return ActionResult.Failure("NavMesh path is invalid.");
                }

                while (navMeshAgent.hasPath &&
                       navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.05f)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartup - startedAt > MovementTimeout)
                    {
                        StopAgent();
                        return ActionResult.Failure("Navigation timed out.");
                    }

                    await Task.Yield();
                }

                StopAgent();
                return ActionResult.Success("Destination reached.");
            }
            catch (OperationCanceledException)
            {
                StopAgent();
                throw;
            }
            finally
            {
                girlAnimator?.SetWalking(false);
            }
        }

        private async Task FollowLoopAsync(CancellationToken cancellationToken)
        {
            var lastUserPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!IsNavigable || userHead == null)
                    {
                        break;
                    }

                    var flatUser = new Vector3(userHead.position.x, transform.position.y, userHead.position.z);
                    var threshold = config != null ? config.FollowRefreshThreshold : 0.25f;
                    if ((flatUser - lastUserPosition).sqrMagnitude >= threshold * threshold)
                    {
                        var destination = CalculateUserDestination(config != null ? config.FollowDistance : 1.2f);
                        if (NavMesh.SamplePosition(destination, out var hit, SampleRadius, NavMesh.AllAreas))
                        {
                            navMeshAgent.stoppingDistance = 0.12f;
                            navMeshAgent.isStopped = false;
                            navMeshAgent.SetDestination(hit.position);
                            lastUserPosition = flatUser;
                        }
                    }

                    var shouldWalk = navMeshAgent.hasPath &&
                                     navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.05f;
                    girlAnimator?.SetWalking(shouldWalk);

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IsFollowing = false;
                StopAgent();
                girlAnimator?.SetWalking(false);
            }
        }

        private Vector3 CalculateUserDestination(float personalSpace)
        {
            var userGround = new Vector3(userHead.position.x, transform.position.y, userHead.position.z);
            var awayFromUser = transform.position - userGround;
            awayFromUser.y = 0f;
            if (awayFromUser.sqrMagnitude < 0.0001f)
            {
                awayFromUser = -userHead.forward;
                awayFromUser.y = 0f;
            }

            return userGround + awayFromUser.normalized * personalSpace;
        }

        private void CancelFollowing()
        {
            if (followCancellation != null)
            {
                followCancellation.Cancel();
                followCancellation.Dispose();
                followCancellation = null;
            }

            IsFollowing = false;
        }

        private void StopAgent()
        {
            if (!IsNavigable)
            {
                return;
            }

            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }

        private async void ObserveFollowTask(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NAV] Follow loop failed: {exception.Message}", this);
            }
        }

        private float SampleRadius => config != null ? config.NavMeshSampleRadius : 0.75f;
        private float MovementTimeout => config != null ? config.MovementTimeoutSeconds : 45f;
    }
}
