using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class AvatarLipSync : MonoBehaviour
    {
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private string mouthBlendShape = "MouthOpen";
        [SerializeField, Range(0f, 100f)] private float gain = 85f;
        [SerializeField, Range(1f, 30f)] private float smoothing = 16f;

        private readonly float[] samples = new float[128];
        private int blendShapeIndex = -1;
        private float currentWeight;

        private void Awake()
        {
            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
            }

            if (faceRenderer != null && faceRenderer.sharedMesh != null)
            {
                blendShapeIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(mouthBlendShape);
            }
        }

        private void Update()
        {
            var targetWeight = 0f;
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.GetOutputData(samples, 0);
                var sum = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    sum += samples[i] * samples[i];
                }

                targetWeight = Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * gain) * 100f;
            }

            currentWeight = Mathf.Lerp(currentWeight, targetWeight, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            if (faceRenderer != null && blendShapeIndex >= 0)
            {
                faceRenderer.SetBlendShapeWeight(blendShapeIndex, currentWeight);
            }
        }
    }
}
