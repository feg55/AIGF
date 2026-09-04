using System;
using System.Collections.Generic;
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
        private Vector3 lastResolvedDestination;
        private string lastResolvedRoomNodeId = string.Empty;
        private bool hasLastResolvedDestination;

        public string CurrentTarget { get; private set; } = string.Empty;
        public bool IsFollowing { get; private set; }
        public bool IsNavigable => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;
        public Transform UserHead => userHead;
        public bool HasLastResolvedDestination => hasLastResolvedDestination;
        public Vector3 LastResolvedDestination => lastResolvedDestination;

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
            var distance = config != null ? config.SocialDistance : 0.9f;
            return WalkToUser(distance, cancellationToken);
        }

        public async Task<ActionResult> WalkToUser(float requestedDistance, CancellationToken cancellationToken)
        {
            if (userHead == null)
            {
                return ActionResult.Failure("User HMD transform is missing.");
            }

            var fallback = config != null ? config.SocialDistance : 0.9f;
            var distance = requestedDistance > 0f ? Mathf.Clamp(requestedDistance, 0.6f, 2f) : fallback;
            var placement = EnsurePlacedOnNavMesh();
            if (!placement.Succeeded)
            {
                return placement;
            }

            if (!TryResolveUserDestination(distance, out var destination))
            {
                return ActionResult.Failure("No reachable NavMesh point exists near the user.");
            }

            CurrentTarget = "user";
            lastResolvedRoomNodeId = string.Empty;
            return await MoveToResolvedAsync(destination, distance * 0.15f, cancellationToken);
        }

        public async Task<ActionResult> WalkToRoomNode(RoomNode node, CancellationToken cancellationToken)
        {
            if (node == null)
            {
                return ActionResult.Failure("Room target is missing.");
            }

            var placement = EnsurePlacedOnNavMesh();
            if (!placement.Succeeded)
            {
                return placement;
            }

            if (!TryResolveRoomDestination(node, out var destination))
            {
                return ActionResult.Failure($"No reachable floor point exists beside '{node.Id}'.");
            }

            CurrentTarget = node.Id;
            lastResolvedRoomNodeId = node.Id;
            return await MoveToResolvedAsync(destination, 0.12f, cancellationToken);
        }

        public ActionResult FollowUser(CancellationToken cancellationToken)
        {
            if (userHead == null)
            {
                return ActionResult.Failure("User HMD transform is missing.");
            }

            var placement = EnsurePlacedOnNavMesh();
            if (!placement.Succeeded)
            {
                return placement;
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

        public ActionResult EnsurePlacedOnNavMesh()
        {
            if (IsNavigable)
            {
                return ActionResult.Success();
            }

            if (navMeshAgent == null)
            {
                return ActionResult.Failure("NavMeshAgent is missing.");
            }

            var searchRadius = Mathf.Max(3f, SampleRadius * 4f);
            if (TrySamplePosition(transform.position, searchRadius, float.PositiveInfinity, out var position) ||
                TrySampleNearUser(searchRadius, out position))
            {
                navMeshAgent.enabled = false;
                transform.position = position;
                navMeshAgent.enabled = true;
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.Warp(position);
                    return ActionResult.Success("Avatar placed on the runtime NavMesh.");
                }
            }

            navMeshAgent.enabled = false;
            return ActionResult.Failure("No NavMesh floor was found near the avatar or the user.");
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
            if (navMeshAgent == null || !TrySamplePosition(
                    requestedPosition,
                    Mathf.Max(SampleRadius, 1.25f),
                    0.65f,
                    out resolvedPosition))
            {
                return false;
            }

            return true;
        }

        public bool TryResolveUserDestination(float requestedDistance, out Vector3 resolvedPosition)
        {
            resolvedPosition = transform.position;
            if (userHead == null || !IsNavigable)
            {
                return false;
            }

            var distance = requestedDistance > 0f
                ? Mathf.Clamp(requestedDistance, 0.6f, 2f)
                : config != null ? config.SocialDistance : 0.9f;
            var userGround = new Vector3(userHead.position.x, transform.position.y, userHead.position.z);
            var preferredDirection = transform.position - userGround;
            preferredDirection.y = 0f;
            if (preferredDirection.sqrMagnitude < 0.0001f)
            {
                preferredDirection = -userHead.forward;
                preferredDirection.y = 0f;
            }

            preferredDirection.Normalize();
            var candidates = new List<Vector3>(49);
            AddRadialCandidates(candidates, userGround, preferredDirection, distance, 16);
            AddRadialCandidates(candidates, userGround, preferredDirection, Mathf.Max(0.45f, distance * 0.7f), 16);
            AddRadialCandidates(candidates, userGround, preferredDirection, distance + 0.35f, 16);
            candidates.Add(userGround);

            return TryResolveBestReachable(candidates, Mathf.Max(SampleRadius, 0.55f), out resolvedPosition);
        }

        public bool TryResolveRoomDestination(RoomNode node, out Vector3 resolvedPosition)
        {
            resolvedPosition = transform.position;
            if (node == null || !IsNavigable)
            {
                return false;
            }

            var bounds = node.Bounds;
            var floorY = transform.position.y;
            var center = new Vector3(bounds.center.x, floorY, bounds.center.z);
            var candidates = new List<Vector3>(49);
            var preferredDirection = transform.position - center;
            preferredDirection.y = 0f;
            if (node.ApproachPoint != null)
            {
                var approach = node.ApproachPoint.position;
                approach.y = floorY;
                candidates.Add(approach);
                preferredDirection = approach - center;
                preferredDirection.y = 0f;
            }

            if (preferredDirection.sqrMagnitude < 0.001f)
            {
                preferredDirection = node.Anchor != null ? node.Anchor.forward : Vector3.forward;
                preferredDirection.y = 0f;
            }
            if (preferredDirection.sqrMagnitude < 0.001f) preferredDirection = Vector3.forward;
            preferredDirection.Normalize();

            var clearance = (navMeshAgent != null ? navMeshAgent.radius : 0.25f) +
                            (config != null ? config.FurnitureMargin : 0.3f) + 0.08f;
            for (var ring = 0; ring < 3; ring++)
            {
                var extraClearance = ring * 0.25f;
                for (var i = 0; i < 16; i++)
                {
                    var angle = AlternatingAngle(i, 16);
                    var direction = Quaternion.Euler(0f, angle, 0f) * preferredDirection;
                    var support = Mathf.Abs(direction.x) * bounds.extents.x +
                                  Mathf.Abs(direction.z) * bounds.extents.z;
                    candidates.Add(center + direction * (support + clearance + extraClearance));
                }
            }

            return TryResolveBestReachableOutsideBounds(
                candidates,
                Mathf.Max(0.4f, Mathf.Min(SampleRadius, 0.75f)),
                bounds,
                (navMeshAgent != null ? navMeshAgent.radius : 0.25f) + 0.03f,
                out resolvedPosition);
        }

        public bool TryGetLastRoomDestination(RoomNode node, out Vector3 resolvedPosition)
        {
            resolvedPosition = lastResolvedDestination;
            return node != null && hasLastResolvedDestination &&
                   string.Equals(lastResolvedRoomNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
        }

        public ActionResult ResumeAtResolved(Vector3 resolvedPosition)
        {
            if (navMeshAgent == null)
            {
                return ActionResult.Failure("NavMeshAgent is missing.");
            }

            navMeshAgent.enabled = false;
            transform.position = resolvedPosition;
            navMeshAgent.enabled = true;
            if (!navMeshAgent.isOnNavMesh || !navMeshAgent.Warp(resolvedPosition))
            {
                navMeshAgent.enabled = false;
                return ActionResult.Failure("NavMeshAgent could not resume at the standing position.");
            }

            navMeshAgent.isStopped = false;
            lastResolvedDestination = resolvedPosition;
            hasLastResolvedDestination = true;
            return ActionResult.Success();
        }

        private async Task<ActionResult> MoveToResolvedAsync(
            Vector3 destination,
            float stoppingDistance,
            CancellationToken cancellationToken)
        {
            CancelFollowing();

            navMeshAgent.stoppingDistance = Mathf.Max(0.05f, stoppingDistance);
            navMeshAgent.isStopped = false;
            if (!navMeshAgent.SetDestination(destination))
            {
                return ActionResult.Failure("NavMesh rejected the destination.");
            }

            lastResolvedDestination = destination;
            hasLastResolvedDestination = true;

            girlAnimator?.SetWalking(true);
            var startedAt = Time.realtimeSinceStartup;
            try
            {
                while (navMeshAgent.pathPending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    return ActionResult.Failure("A complete NavMesh path is unavailable.");
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
                        if (TryResolveUserDestination(
                                config != null ? config.FollowDistance : 1.2f,
                                out var destination))
                        {
                            navMeshAgent.stoppingDistance = 0.12f;
                            navMeshAgent.isStopped = false;
                            navMeshAgent.SetDestination(destination);
                            lastResolvedDestination = destination;
                            hasLastResolvedDestination = true;
                            lastResolvedRoomNodeId = string.Empty;
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

        private bool TryResolveBestReachable(
            IReadOnlyList<Vector3> requestedPositions,
            float sampleRadius,
            out Vector3 resolvedPosition)
        {
            return TryResolveBestReachableInternal(
                requestedPositions,
                sampleRadius,
                false,
                default,
                0f,
                out resolvedPosition);
        }

        private bool TryResolveBestReachableOutsideBounds(
            IReadOnlyList<Vector3> requestedPositions,
            float sampleRadius,
            Bounds blockedBounds,
            float clearance,
            out Vector3 resolvedPosition)
        {
            return TryResolveBestReachableInternal(
                requestedPositions,
                sampleRadius,
                true,
                blockedBounds,
                clearance,
                out resolvedPosition);
        }

        private bool TryResolveBestReachableInternal(
            IReadOnlyList<Vector3> requestedPositions,
            float sampleRadius,
            bool rejectBlockedBounds,
            Bounds blockedBounds,
            float clearance,
            out Vector3 resolvedPosition)
        {
            resolvedPosition = transform.position;
            var bestLength = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < requestedPositions.Count; i++)
            {
                var requested = requestedPositions[i];
                if (!TrySamplePosition(requested, sampleRadius, 0.65f, out var sampled) ||
                    rejectBlockedBounds && !IsOutsideHorizontalBounds(sampled, blockedBounds, clearance) ||
                    !TryCalculateCompletePath(sampled, out var pathLength))
                {
                    continue;
                }

                // Direction preference is useful, but accessibility and path length
                // must win when PICO reports an unreliable furniture orientation.
                var preferencePenalty = Mathf.Min(i * 0.02f, 0.3f);
                var score = pathLength + preferencePenalty;
                if (score >= bestLength) continue;
                bestLength = score;
                resolvedPosition = sampled;
                found = true;
            }

            return found;
        }

        private bool TryCalculateCompletePath(Vector3 destination, out float pathLength)
        {
            pathLength = 0f;
            if (!IsNavigable)
            {
                return false;
            }

            var path = new NavMeshPath();
            var start = navMeshAgent != null ? navMeshAgent.nextPosition : transform.position;
            if (!NavMesh.CalculatePath(start, destination, AreaMask, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0)
            {
                return false;
            }

            for (var i = 1; i < path.corners.Length; i++)
            {
                pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return true;
        }

        private static void AddRadialCandidates(
            ICollection<Vector3> candidates,
            Vector3 center,
            Vector3 preferredDirection,
            float radius,
            int directionCount)
        {
            for (var i = 0; i < directionCount; i++)
            {
                var direction = Quaternion.Euler(0f, AlternatingAngle(i, directionCount), 0f) *
                                preferredDirection;
                candidates.Add(center + direction * radius);
            }
        }

        private static float AlternatingAngle(int index, int directionCount)
        {
            if (index == 0) return 0f;
            var step = 360f / Mathf.Max(1, directionCount);
            var magnitude = (index + 1) / 2;
            return (index % 2 == 1 ? 1f : -1f) * magnitude * step;
        }

        private static bool IsOutsideHorizontalBounds(Vector3 point, Bounds bounds, float clearance)
        {
            var nearestX = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
            var nearestZ = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);
            var deltaX = point.x - nearestX;
            var deltaZ = point.z - nearestZ;
            return deltaX * deltaX + deltaZ * deltaZ >= clearance * clearance;
        }

        private bool TrySampleNearUser(float radius, out Vector3 position)
        {
            position = transform.position;
            if (userHead == null) return false;
            var userGround = new Vector3(userHead.position.x, transform.position.y, userHead.position.z);
            return TrySamplePosition(userGround, radius, float.PositiveInfinity, out position);
        }

        private bool TrySamplePosition(
            Vector3 requestedPosition,
            float radius,
            float maxVerticalDifference,
            out Vector3 resolvedPosition)
        {
            resolvedPosition = requestedPosition;
            if (!NavMesh.SamplePosition(requestedPosition, out var hit, radius, AreaMask))
            {
                return false;
            }

            if (Mathf.Abs(hit.position.y - requestedPosition.y) > maxVerticalDifference)
            {
                return false;
            }

            resolvedPosition = hit.position;
            return true;
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
        private int AreaMask => navMeshAgent != null ? navMeshAgent.areaMask : NavMesh.AllAreas;
    }
}
