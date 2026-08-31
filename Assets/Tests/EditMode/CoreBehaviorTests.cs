using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.AI;
using Aigf.Companion.Agent;
using Aigf.Companion.Room;
using NUnit.Framework;
using UnityEngine;

namespace Aigf.Companion.Tests
{
    public sealed class CoreBehaviorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < objects.Count; i++) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void RoomGraphLooksUpIdsCaseInsensitively()
        {
            var node = CreateNode("sofa_1", RoomNodeType.Sofa);
            var graph = new RoomGraph(new[] { node });
            Assert.That(graph.TryGetNode("SOFA_1", out var found), Is.True);
            Assert.That(found, Is.SameAs(node));
        }

        [Test]
        public void PromptContainsOnlySemanticRoomSummary()
        {
            var graph = new RoomGraph(new[] { CreateNode("table_1", RoomNodeType.Table) });
            var context = new AgentContext { Room = graph, UserForward = Vector3.forward };
            var prompt = new PromptBuilder().Build("go to table", context);
            StringAssert.Contains("id=table_1 type=table canSit=false", prompt);
            StringAssert.DoesNotContain("GameObject", prompt);
            StringAssert.Contains("Return one JSON object only", prompt);
        }

        [Test]
        public async Task MockLlmMapsRussianAndEnglishCommands()
        {
            var graph = new RoomGraph(new[] { CreateNode("sofa_1", RoomNodeType.Sofa, true) });
            var context = new AgentContext { Room = graph };
            var llm = new MockLocalLLM();

            var come = await llm.GenerateAsync("иди ко мне", context);
            var sit = await llm.GenerateAsync("sit on the sofa", context);
            var follow = await llm.GenerateAsync("следуй за мной", context);

            Assert.That(come.Actions[0].Type, Is.EqualTo(AgentActionTypes.WalkToUser));
            Assert.That(sit.Actions[0].Type, Is.EqualTo(AgentActionTypes.WalkTo));
            Assert.That(sit.Actions[1].Type, Is.EqualTo(AgentActionTypes.Sit));
            Assert.That(follow.Actions[0].Type, Is.EqualTo(AgentActionTypes.FollowUser));
        }

        [Test]
        public async Task ActionSequenceRunsInOrderAndStopsOnFailure()
        {
            var observed = new List<string>();
            var actions = new[]
            {
                new AgentAction("first"),
                new AgentAction("second"),
                new AgentAction("third")
            };

            var result = await ActionSequenceRunner.RunAsync(actions, (action, token) =>
            {
                observed.Add(action.Type);
                return Task.FromResult(action.Type == "second"
                    ? ActionResult.Failure("expected")
                    : ActionResult.Success());
            }, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            CollectionAssert.AreEqual(new[] { "first", "second" }, observed);
        }

        private RoomNode CreateNode(string id, RoomNodeType type, bool canSit = false)
        {
            var root = new GameObject(id);
            objects.Add(root);
            InteractionAnchor interaction = null;
            Transform approach = null;
            Transform sit = null;
            if (canSit)
            {
                approach = new GameObject("approach").transform;
                approach.SetParent(root.transform);
                sit = new GameObject("sit").transform;
                sit.SetParent(root.transform);
                interaction = root.AddComponent<InteractionAnchor>();
                interaction.Configure(approach, sit);
            }

            var node = root.AddComponent<RoomNode>();
            node.Configure(id, type, canSit, approach, sit, interaction);
            return node;
        }
    }
}
