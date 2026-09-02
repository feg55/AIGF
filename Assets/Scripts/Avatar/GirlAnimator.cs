using System;
using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class GirlAnimator : MonoBehaviour
    {
        [Serializable]
        private struct NamedAnimationBinding
        {
            public string Id;
            public string Trigger;
        }

        [SerializeField] private Animator animator;
        [Header("Centralized Animator parameters")]
        [SerializeField] private string walkingParameter = "Walking";
        [SerializeField] private string sitTrigger = "Sit";
        [SerializeField] private string standTrigger = "Stand";
        [SerializeField] private string waveTrigger = "Wave";
        [SerializeField] private string emotionParameter = "Emotion";
        [SerializeField] private NamedAnimationBinding[] safeNamedAnimations = Array.Empty<NamedAnimationBinding>();
        [SerializeField] private ProceduralAvatarMotion proceduralMotion;
        [SerializeField] private CompanionAnimationPlayer authoredMotion;
        [SerializeField] private MintFacialDriver facialDriver;

        private bool warnedMissingAnimator;

        public Animator Animator => animator;
        public float SitTransitionDuration => authoredMotion != null ? authoredMotion.SitEnterDuration : 0f;
        public float StandTransitionDuration => authoredMotion != null ? authoredMotion.SitExitDuration : 0f;

        public bool TryGetHips(out Transform hips)
        {
            hips = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            return hips != null;
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (proceduralMotion == null) proceduralMotion = GetComponent<ProceduralAvatarMotion>();
            if (authoredMotion == null) authoredMotion = GetComponent<CompanionAnimationPlayer>();
            if (authoredMotion == null && animator != null) authoredMotion = gameObject.AddComponent<CompanionAnimationPlayer>();
            if (proceduralMotion != null) proceduralMotion.enabled = false;
            if (facialDriver == null) facialDriver = GetComponent<MintFacialDriver>();
        }

        public void SetWalking(bool walking)
        {
            if (authoredMotion != null && authoredMotion.SetWalking(walking)) return;
            TrySetBool(walkingParameter, walking);
        }

        public void Sit()
        {
            if (authoredMotion != null && authoredMotion.Sit()) return;
            TrySetTrigger(sitTrigger);
        }

        public void Stand()
        {
            if (authoredMotion != null && authoredMotion.Stand()) return;
            TrySetTrigger(standTrigger);
        }

        public void Wave()
        {
            if (authoredMotion != null && authoredMotion.Greet()) return;
            TrySetTrigger(waveTrigger);
        }

        public bool PlayNamedAnimation(string safeAnimationId)
        {
            for (var i = 0; i < safeNamedAnimations.Length; i++)
            {
                if (string.Equals(safeNamedAnimations[i].Id, safeAnimationId, StringComparison.OrdinalIgnoreCase))
                {
                    return TrySetTrigger(safeNamedAnimations[i].Trigger);
                }
            }

            Debug.LogWarning($"[AVATAR] Animation ID '{safeAnimationId}' is not in the safe animation map.", this);
            return false;
        }

        public void SetEmotion(string emotion)
        {
            var value = 0f;
            switch ((emotion ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "warm": value = 0.35f; break;
                case "happy": value = 1f; break;
                case "calm": value = -0.25f; break;
            }

            TrySetFloat(emotionParameter, value);
            facialDriver?.SetEmotion(value);
        }

        private bool TrySetBool(string parameter, bool value)
        {
            if (!HasParameter(parameter, AnimatorControllerParameterType.Bool))
            {
                return false;
            }

            animator.SetBool(parameter, value);
            return true;
        }

        private bool TrySetFloat(string parameter, float value)
        {
            if (!HasParameter(parameter, AnimatorControllerParameterType.Float))
            {
                return false;
            }

            animator.SetFloat(parameter, value);
            return true;
        }

        private bool TrySetTrigger(string parameter)
        {
            if (!HasParameter(parameter, AnimatorControllerParameterType.Trigger))
            {
                return false;
            }

            animator.SetTrigger(parameter);
            return true;
        }

        private bool HasParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null)
            {
                if (!warnedMissingAnimator)
                {
                    warnedMissingAnimator = true;
                    Debug.LogWarning("[AVATAR] Animator is missing; body animation is disabled to protect the rig.", this);
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == parameter)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
