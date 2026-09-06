using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.AI;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using Aigf.Companion.Voice;
using NUnit.Framework;
using PonyuDev.SherpaOnnx.Vad;
using PonyuDev.SherpaOnnx.Vad.Data;
using PonyuDev.SherpaOnnx.Vad.Engine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aigf.Companion.Tests
{
    public sealed class CompanionRegressionTests
    {
        [TestCase(0, 0)]
        [TestCase(0, 180)]
        [TestCase(90, 35)]
        [TestCase(-90, 210)]
        public void CapturedSeatStaysUprightAndFacesItsApproach(float pitch, float yaw)
        {
            var root = new GameObject("captured sofa");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(2, 0.45f, 3), Quaternion.Euler(pitch, yaw, 0));
                var box = root.AddComponent<BoxCollider>();
                box.size = Mathf.Abs(pitch) > 45 ? new Vector3(2, 0.8f, 0.9f) : new Vector3(2, 0.9f, 0.8f);
                var observer = new Vector3(0, 1.6f, 0);
                SeatGeometry.Calculate(box, observer, true, out var approach, out var seat, out var facing);
                Assert.That(Vector3.Dot(facing * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
                Assert.That(Vector3.Dot(facing * Vector3.forward, (approach - seat).normalized), Is.GreaterThan(0.7f));
                Assert.That(Vector3.Dot(facing * Vector3.forward, observer - seat), Is.GreaterThan(0));
                Assert.That(approach.y, Is.EqualTo(0).Within(0.001));
                Assert.That(seat.y, Is.InRange(0.38f, 0.52f));
                Assert.That(Vector3.Distance(approach, seat), Is.GreaterThan(0.45f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FinalSpeechSegmentIsDeliveredDuringSilenceWithoutAnotherUtterance()
        {
            var root = new GameObject("voice regression");
            try
            {
                var input = root.AddComponent<SherpaVoiceInput>();
                var engine = new SilenceCompletesVad();
                var vad = new VadService();
                SetField(vad, "_engine", engine);
                SetField(input, "vad", vad);
                var received = 0;
                vad.OnSegment += _ => received++;
                typeof(SherpaVoiceInput).GetMethod("FeedVad", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(input, new object[] { new float[] { 1, 1, 1, 1, 0, 0, 0, 0 } });
                Assert.That(received, Is.EqualTo(1));
                Assert.That(engine.IsSpeechDetected(), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MutingDiscardsPartialSpeechAndDoesNotFlushIt()
        {
            var root = new GameObject("mute regression");
            try
            {
                var input = root.AddComponent<SherpaVoiceInput>();
                var engine = new SilenceCompletesVad();
                var vad = new VadService();
                SetField(vad, "_engine", engine);
                SetField(input, "vad", vad);
                vad.AcceptWaveform(new float[] { 1, 1, 1, 1 });
                input.SetMuted(true);
                Assert.That(input.IsMuted, Is.True);
                Assert.That(engine.IsSpeechDetected(), Is.False);
                Assert.That(engine.FlushCalled, Is.False);
                input.enabled = false;
                input.enabled = true;
                Assert.That(input.IsMuted, Is.True, "Lifecycle must preserve explicit mute.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public async Task ConversationWithoutActionsWorksWithAnEmptyRoom()
        {
            var root = new GameObject("conversation regression");
            var config = AppConfig.CreateRuntimeDefaults();
            try
            {
                var executor = root.AddComponent<ActionExecutor>();
                var brain = root.AddComponent<GirlBrain>();
                var room = new RoomGraph();
                var tts = new RecordingTts();
                executor.Configure(room, config);
                brain.Initialize(new ConversationalLlm(), tts, executor, room, null, root.transform, config);
                var result = await brain.ProcessUserMessageAsync("Как прошёл твой день?");
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(tts.Spoken, Is.EqualTo("Хорошо! А твой?"));
                Assert.That(executor.CurrentAction, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [TestCase("Минт, пожалуйста, помаши!", AgentActionTypes.Wave)]
        [TestCase("Встань.", AgentActionTypes.Stand)]
        [TestCase("Иди ко мне", AgentActionTypes.WalkToUser)]
        [TestCase("Следуй за мной", AgentActionTypes.FollowUser)]
        [TestCase("Stop!", AgentActionTypes.Stop)]
        [TestCase("Please look at me", AgentActionTypes.LookAtUser)]
        public void ExactImperativesHaveDirectActions(string text, string action)
        {
            Assert.That(DirectCommand.TryParse(text, new RoomGraph(), out var reply), Is.True);
            Assert.That(reply.Actions.Count, Is.EqualTo(1));
            Assert.That(reply.Actions[0].Type, Is.EqualTo(action));
        }

        [TestCase("Не садись")]
        [TestCase("Почему ты сидишь на диване?")]
        [TestCase("Мне понравилось, как ты помахала")]
        [TestCase("Don't stop")]
        [TestCase("Как прошёл твой день?")]
        public void ConversationAndNegationsAreNotCannedCommands(string text)
        {
            Assert.That(DirectCommand.TryParse(text, new RoomGraph(), out _), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task MenuAndTranscriptCommandsWorkWithoutAnyModel(bool fromMenu)
        {
            var root = new GameObject("model unavailable regression");
            var config = AppConfig.CreateRuntimeDefaults();
            try
            {
                var executor = root.AddComponent<ActionExecutor>();
                var brain = root.AddComponent<GirlBrain>();
                var room = new RoomGraph();
                executor.Configure(room, config);
                brain.Initialize(null, null, executor, room, null, root.transform, config);
                Assert.That(brain.IsReady, Is.False);
                Assert.That(brain.CanProcessInput, Is.True);
                var result = fromMenu ? await brain.ProcessCommandAsync(CompanionCommand.Stop) :
                    await brain.ProcessUserMessageAsync("Стоп!");
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(brain.LastError, Is.Empty);
                Assert.That(brain.LastModelJson, Does.Contain("stop"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        [Test]
        public async Task DirectCommandCancelsConversationWithoutWaitingForModel()
        {
            var root = new GameObject("slow model regression");
            var config = AppConfig.CreateRuntimeDefaults();
            var llm = new PendingLlm();
            try
            {
                var executor = root.AddComponent<ActionExecutor>();
                var brain = root.AddComponent<GirlBrain>();
                var room = new RoomGraph();
                executor.Configure(room, config);
                brain.Initialize(llm, null, executor, room, null, root.transform, config);
                var conversation = brain.ProcessUserMessageAsync("Расскажи о себе");
                Assert.That(conversation.IsCompleted, Is.False);
                var stop = brain.ProcessCommandAsync(CompanionCommand.Stop);
                Assert.That(stop.IsCompleted, Is.True, "Stop must not wait for native generation.");
                Assert.That((await stop).Succeeded, Is.True);
                Assert.That(llm.Token.IsCancellationRequested, Is.True);
                llm.Completion.SetResult(new AgentReply("Поздний ответ", "neutral"));
                Assert.That((await conversation).Succeeded, Is.False);
                Assert.That(brain.LastModelJson, Does.Contain("stop"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        private sealed class PendingLlm : ILocalLLM
        {
            public bool IsReady => true;
            public CancellationToken Token;
            public readonly TaskCompletionSource<AgentReply> Completion = new TaskCompletionSource<AgentReply>();
            public Task<AgentReply> GenerateAsync(string text, AgentContext context, CancellationToken token = default)
            {
                Token = token;
                return Completion.Task;
            }
        }

        private sealed class ConversationalLlm : ILocalLLM
        {
            public bool IsReady => true;
            public Task<AgentReply> GenerateAsync(string text, AgentContext context, CancellationToken token = default) =>
                Task.FromResult(new AgentReply("Хорошо! А твой?", "warm"));
        }

        private sealed class RecordingTts : ITts
        {
            public string Spoken;
            public bool IsReady => true;
            public Task SpeakAsync(string text, CancellationToken token = default) { Spoken = text; return Task.CompletedTask; }
            public void Stop() { }
        }

        private sealed class SilenceCompletesVad : IVadEngine
        {
            private bool speech;
            private bool pending;
            public bool FlushCalled;
            public bool IsLoaded => true;
            public int WindowSize => 4;
            public void Load(VadProfile profile, string directory) { }
            public void Unload() { }
            public void Dispose() { }
            public bool IsSpeechDetected() => speech;
            public void AcceptWaveform(float[] samples)
            {
                var next = samples[0] > 0;
                pending |= speech && !next;
                speech = next;
            }
            public List<VadSegment> DrainSegments()
            {
                var result = new List<VadSegment>();
                if (pending)
                    result.Add((VadSegment)Activator.CreateInstance(typeof(VadSegment),
                        BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 0, new float[] { 1, 1, 1, 1 }, 16000 }, null));
                pending = false;
                return result;
            }
            public void Flush() { FlushCalled = true; pending = speech; speech = false; }
            public void Reset() { speech = false; pending = false; }
        }
    }
}
