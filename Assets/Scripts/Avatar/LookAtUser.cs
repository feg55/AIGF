using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class LookAtUser : MonoBehaviour
    {
        [SerializeField] private Animator humanoidAnimator;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform userHead;
        [SerializeField, Range(10f, 90f)] private float maxYaw = 45f;
        [SerializeField, Range(5f, 60f)] private float maxPitch = 25f;
        [SerializeField, Range(0f, 1f)] private float lookWeight = 0.65f;
        [SerializeField, Range(1f, 20f)] private float smoothing = 8f;
        [SerializeField] private bool lookEnabled = true;

        private Transform currentTarget;
        private Quaternion smoothedLookOffset = Quaternion.identity;

        public bool IsLooking => lookEnabled;
        public Transform CurrentTarget => currentTarget;

        private void Awake()
        {
            if (humanoidAnimator == null)
            {
                humanoidAnimator = GetComponentInChildren<Animator>();
            }

            if (headBone == null && humanoidAnimator != null && humanoidAnimator.isHuman)
            {
                headBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.Head);
            }

            currentTarget = userHead;
        }

        private void LateUpdate()
        {
            if (!lookEnabled || headBone == null || currentTarget == null)
            {
                return;
            }

            var direction = currentTarget.position - headBone.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var rootLocal = transform.InverseTransformDirection(direction.normalized);
            var yaw = Mathf.Clamp(
                Mathf.Atan2(rootLocal.x, rootLocal.z) * Mathf.Rad2Deg,
                -maxYaw,
                maxYaw);
            var pitch = Mathf.Clamp(
                -Mathf.Asin(Mathf.Clamp(rootLocal.y, -1f, 1f)) * Mathf.Rad2Deg,
                -maxPitch,
                maxPitch);
            var targetOffset = Quaternion.Euler(pitch * lookWeight, yaw * lookWeight, 0f);
            smoothedLookOffset = Quaternion.Slerp(
                smoothedLookOffset,
                targetOffset,
                1f - Mathf.Exp(-smoothing * Time.deltaTime));

            // Apply an additive world-space delta. Assigning an absolute rotation here
            // destroys the Humanoid animation's neck/head basis on non-standard rigs.
            var worldOffset = transform.rotation * smoothedLookOffset * Quaternion.Inverse(transform.rotation);
            headBone.rotation = worldOffset * headBone.rotation;
        }

        public void Configure(Transform hmdTransform, Transform explicitHeadBone = null)
        {
            userHead = hmdTransform;
            currentTarget = hmdTransform;
            if (explicitHeadBone != null)
            {
                headBone = explicitHeadBone;
            }
        }

        public void LookAtUserNow()
        {
            currentTarget = userHead;
            lookEnabled = true;
        }

        public void LookAt(Transform target)
        {
            currentTarget = target;
            lookEnabled = target != null;
        }

        public void StopLooking()
        {
            lookEnabled = false;
            smoothedLookOffset = Quaternion.identity;
        }
    }
}
