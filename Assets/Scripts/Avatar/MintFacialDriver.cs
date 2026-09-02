using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class MintFacialDriver : MonoBehaviour
    {
        [SerializeField] private AudioSource voiceSource;
        [SerializeField, Range(1f, 80f)] private float voiceGain = 32f;
        [SerializeField, Range(1f, 30f)] private float smoothing = 14f;

        private readonly float[] samples = new float[128];
        private readonly Dictionary<Transform, Quaternion> neutral = new Dictionary<Transform, Quaternion>();
        private Transform mouth;
        private Transform upperLip;
        private Transform lowerLip;
        private Transform browLeft;
        private Transform browRight;
        private Transform upperLidLeft;
        private Transform upperLidRight;
        private float mouthAmount;
        private float emotion;
        private float blink;
        private float nextBlink;

        private void Start()
        {
            if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
            mouth = FindDeep(transform, "mouth");
            upperLip = FindDeep(transform, "Bon_uplip_M");
            lowerLip = FindDeep(transform, "Bon_Lolip_M");
            browLeft = FindDeep(transform, "Bon_eyebrow02_L");
            browRight = FindDeep(transform, "Bon_eyebrow02_R");
            upperLidLeft = FindDeep(transform, "BON_eyelid_up_L");
            upperLidRight = FindDeep(transform, "BON_eyelid_up_R");
            Capture(mouth, upperLip, lowerLip, browLeft, browRight, upperLidLeft, upperLidRight);
            ScheduleBlink();
        }

        private void LateUpdate()
        {
            var targetMouth = 0f;
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.GetOutputData(samples, 0);
                var energy = 0f;
                for (var i = 0; i < samples.Length; i++) energy += samples[i] * samples[i];
                targetMouth = Mathf.Clamp01(Mathf.Sqrt(energy / samples.Length) * voiceGain);
            }
            mouthAmount = Mathf.Lerp(mouthAmount, targetMouth, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            if (Time.time >= nextBlink && blink <= 0f) blink = 1f;
            if (blink > 0f)
            {
                blink = Mathf.Max(0f, blink - Time.deltaTime * 7f);
                if (blink <= 0f) ScheduleBlink();
            }
            var eyelid = Mathf.Sin(blink * Mathf.PI);

            Apply(mouth, Quaternion.Euler(mouthAmount * 12f, 0f, 0f));
            Apply(upperLip, Quaternion.Euler(-mouthAmount * 7f, 0f, 0f));
            Apply(lowerLip, Quaternion.Euler(mouthAmount * 10f, 0f, 0f));
            Apply(browLeft, Quaternion.Euler(0f, 0f, emotion * 5f));
            Apply(browRight, Quaternion.Euler(0f, 0f, -emotion * 5f));
            Apply(upperLidLeft, Quaternion.Euler(eyelid * 18f, 0f, 0f));
            Apply(upperLidRight, Quaternion.Euler(eyelid * 18f, 0f, 0f));
        }

        public void SetEmotion(float value) => emotion = Mathf.Clamp(value, -1f, 1f);

        private void ScheduleBlink() => nextBlink = Time.time + UnityEngine.Random.Range(2.2f, 5.5f);

        private void Capture(params Transform[] bones)
        {
            for (var i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null) neutral[bones[i]] = bones[i].localRotation;
            }
        }

        private void Apply(Transform bone, Quaternion additive)
        {
            if (bone != null && neutral.TryGetValue(bone, out var baseRotation)) bone.localRotation = baseRotation * additive;
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase)) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
