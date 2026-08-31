using System;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using UnityEngine;

namespace Aigf.Companion.Voice
{
    public sealed class VoicePipeline : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour vadComponent;
        [SerializeField] private MonoBehaviour sttComponent;
        [SerializeField] private GirlBrain brain;

        public async Task<ActionResult> ProcessClipAsync(
            AudioClip clip,
            CancellationToken cancellationToken = default)
        {
            if (clip == null) return ActionResult.Failure("Voice clip is missing.");
            var vad = vadComponent as IVad;
            var stt = sttComponent as IStt;
            if (vad == null || stt == null || !stt.IsReady)
            {
                return ActionResult.Failure("Local VAD/STT is unavailable; use debug text input.");
            }

            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            if (!vad.ContainsSpeech(samples, samples.Length, clip.frequency))
            {
                return ActionResult.Failure("No speech detected.");
            }

            var transcript = await stt.TranscribeAsync(clip, cancellationToken);
            return brain != null
                ? await brain.ProcessUserMessageAsync(transcript, cancellationToken)
                : ActionResult.Failure("GirlBrain is missing.");
        }

        public void Configure(MonoBehaviour vad, MonoBehaviour stt, GirlBrain girlBrain)
        {
            vadComponent = vad;
            sttComponent = stt;
            brain = girlBrain;
        }
    }
}
