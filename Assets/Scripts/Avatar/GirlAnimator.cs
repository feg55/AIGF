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

        private bool warnedMissingAnimator;

        public Animator Animator => animator;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        public void SetWalking(bool walking)
        {
            TrySetBool(walkingParameter, walking);
        }

        public void Sit()
        {
            TrySetTrigger(sitTrigger);
        }

        public void Stand()
        {
            TrySetTrigger(standTrigger);
        }

        public void Wave()
        {
            if (!TrySetTrigger(waveTrigger))
            {
                Debug.Log("[AVATAR] Wave requested; placeholder avatar has no Wave trigger.", this);
            }
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
                    Debug.LogWarning("[AVATAR] Animator is missing; actions will use safe logged placeholders.", this);
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
