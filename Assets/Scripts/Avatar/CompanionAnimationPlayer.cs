using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Aigf.Companion.Avatar
{
    [DisallowMultipleComponent]
    public sealed class CompanionAnimationPlayer : MonoBehaviour
    {
        private enum Motion
        {
            Idle,
            Walk,
            SitEnter,
            SitIdle,
            SitTalk,
            SitExit,
            Greet,
            Talk
        }

        private static readonly string[] ClipNames =
        {
            "Mint_Idle",
            "Mint_Walk",
            "Mint_SitEnter",
            "Mint_SitIdle",
            "Mint_SitTalk",
            "Mint_SitExit",
            "Mint_Greet",
            "Mint_Talk"
        };

        [SerializeField] private Animator animator;
        [SerializeField] private string resourcePath = "CompanionAnimations/Mint_Motion_CC0";
        [SerializeField, Min(0f)] private float locomotionBlendSeconds = 0.18f;
        [SerializeField, Min(0f)] private float actionBlendSeconds = 0.12f;

        private AnimationClip[] clips = Array.Empty<AnimationClip>();
        private AnimationClipPlayable[] playables = Array.Empty<AnimationClipPlayable>();
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private int current = -1;
        private int previous = -1;
        private float blendElapsed;
        private float blendDuration;
        private bool walkingRequested;
        private bool sittingRequested;

        public bool IsReady { get; private set; }
        public float SitEnterDuration => ClipLength(Motion.SitEnter) + actionBlendSeconds;
        public float SitExitDuration => ClipLength(Motion.SitExit) + actionBlendSeconds;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            TryBuildGraph();
        }

        private void OnEnable()
        {
            if (graph.IsValid()) graph.Play();
        }

        private void OnDisable()
        {
            if (graph.IsValid()) graph.Stop();
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        private void Update()
        {
            if (!IsReady) return;

            AdvanceCurrentMotion();
            AdvanceBlend(Time.deltaTime);
        }

        public bool SetWalking(bool value)
        {
            walkingRequested = value;
            if (!IsReady) return false;
            if (sittingRequested || IsSeatedMotion((Motion)current) || IsOneShot((Motion)current)) return true;

            SwitchTo(value ? Motion.Walk : Motion.Idle, locomotionBlendSeconds);
            return true;
        }

        public bool Sit()
        {
            walkingRequested = false;
            sittingRequested = true;
            if (!IsReady) return false;

            SwitchTo(Motion.SitEnter, actionBlendSeconds, true);
            return true;
        }

        public bool Stand()
        {
            sittingRequested = false;
            if (!IsReady) return false;

            if (IsSeatedMotion((Motion)current))
            {
                SwitchTo(Motion.SitExit, actionBlendSeconds, true);
            }
            else
            {
                RestoreRequestedMotion(actionBlendSeconds);
            }
            return true;
        }

        public bool Greet()
        {
            if (!IsReady) return false;

            SwitchTo(sittingRequested ? Motion.SitTalk : Motion.Greet, actionBlendSeconds, true);
            return true;
        }

        private void TryBuildGraph()
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Debug.LogError("[AVATAR] Mint needs a valid Humanoid Avatar before authored motion can play.", this);
                return;
            }

            var available = Resources.LoadAll<AnimationClip>(resourcePath);
            clips = new AnimationClip[ClipNames.Length];
            for (var i = 0; i < ClipNames.Length; i++)
            {
                clips[i] = FindClip(available, ClipNames[i]);
                if (clips[i] == null)
                {
                    Debug.LogError($"[AVATAR] Required authored clip '{ClipNames[i]}' is missing from Resources/{resourcePath}.", this);
                    clips = Array.Empty<AnimationClip>();
                    return;
                }

                if (!clips[i].humanMotion)
                {
                    Debug.LogError($"[AVATAR] Clip '{clips[i].name}' was not imported as Humanoid; refusing unsafe transform retargeting.", this);
                    clips = Array.Empty<AnimationClip>();
                    return;
                }
            }

            animator.applyRootMotion = false;
            // Seat alignment reads hips even when the HMD is facing elsewhere.
            // CullUpdateTransforms freezes those bones in the previous pose.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Mint authored humanoid motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            playables = new AnimationClipPlayable[clips.Length];
            for (var i = 0; i < clips.Length; i++)
            {
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                playables[i].SetApplyFootIK(true);
                playables[i].SetApplyPlayableIK(false);
                graph.Connect(playables[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }

            var output = AnimationPlayableOutput.Create(graph, "Mint humanoid output", animator);
            output.SetSourcePlayable(mixer);
            current = (int)Motion.Idle;
            mixer.SetInputWeight(current, 1f);
            Restart(current);
            IsReady = true;
            graph.Play();
        }

        private void AdvanceCurrentMotion()
        {
            var motion = (Motion)current;
            var clip = clips[current];
            var time = playables[current].GetTime();
            if (IsLoop(motion))
            {
                if (clip.length > 0f && time >= clip.length)
                {
                    playables[current].SetTime(time % clip.length);
                    playables[current].SetDone(false);
                }
                return;
            }

            if (time < clip.length) return;
            switch (motion)
            {
                case Motion.SitEnter:
                    SwitchTo(sittingRequested ? Motion.SitIdle : Motion.SitExit, actionBlendSeconds, true);
                    break;
                case Motion.SitExit:
                case Motion.Greet:
                case Motion.Talk:
                    RestoreRequestedMotion(actionBlendSeconds);
                    break;
                case Motion.SitTalk:
                    SwitchTo(Motion.SitIdle, actionBlendSeconds, true);
                    break;
            }
        }

        private void RestoreRequestedMotion(float blendSeconds)
        {
            if (sittingRequested)
            {
                SwitchTo(Motion.SitIdle, blendSeconds);
            }
            else
            {
                SwitchTo(walkingRequested ? Motion.Walk : Motion.Idle, blendSeconds);
            }
        }

        private void SwitchTo(Motion motion, float seconds, bool restart = false)
        {
            var next = (int)motion;
            if (next == current && !restart) return;

            previous = current;
            current = next;
            blendElapsed = 0f;
            blendDuration = Mathf.Max(0f, seconds);
            Restart(current);

            for (var i = 0; i < clips.Length; i++)
            {
                if (i != previous && i != current) mixer.SetInputWeight(i, 0f);
            }

            if (previous < 0 || blendDuration <= 0f || previous == current)
            {
                if (previous >= 0 && previous != current) mixer.SetInputWeight(previous, 0f);
                mixer.SetInputWeight(current, 1f);
                previous = -1;
            }
        }

        private void AdvanceBlend(float deltaTime)
        {
            if (previous < 0) return;

            blendElapsed += Mathf.Max(0f, deltaTime);
            var weight = blendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / blendDuration);
            mixer.SetInputWeight(previous, 1f - weight);
            mixer.SetInputWeight(current, weight);
            if (weight < 1f) return;

            mixer.SetInputWeight(previous, 0f);
            previous = -1;
        }

        private void Restart(int index)
        {
            playables[index].SetTime(0d);
            playables[index].SetDone(false);
            playables[index].SetSpeed(1d);
        }

        private float ClipLength(Motion motion)
        {
            var index = (int)motion;
            return index >= 0 && index < clips.Length && clips[index] != null ? clips[index].length : 0f;
        }

        private static AnimationClip FindClip(AnimationClip[] available, string expectedName)
        {
            for (var i = 0; i < available.Length; i++)
            {
                if (string.Equals(available[i].name, expectedName, StringComparison.OrdinalIgnoreCase) ||
                    available[i].name.EndsWith(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return available[i];
                }
            }
            return null;
        }

        private static bool IsLoop(Motion motion)
        {
            return motion == Motion.Idle || motion == Motion.Walk || motion == Motion.SitIdle;
        }

        private static bool IsOneShot(Motion motion)
        {
            return motion == Motion.SitEnter || motion == Motion.SitTalk || motion == Motion.SitExit ||
                   motion == Motion.Greet || motion == Motion.Talk;
        }

        private static bool IsSeatedMotion(Motion motion)
        {
            return motion == Motion.SitEnter || motion == Motion.SitIdle || motion == Motion.SitTalk || motion == Motion.SitExit;
        }
    }
}
