using System.Collections.Generic;
using Aigf.Companion.AI;
using Aigf.Companion.Room;
using NUnit.Framework;
using UnityEngine;

namespace Aigf.Companion.Tests
{
    public sealed class AgentJsonTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < objects.Count; i++) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void ParsesValidSequentialSitReply()
        {
            var room = CreateRoomWithSofa();
            const string json = "{\"speech\":\"Хорошо.\",\"emotion\":\"warm\",\"actions\":[{\"type\":\"walk_to\",\"target\":\"sofa_1\"},{\"type\":\"sit\",\"target\":\"sofa_1\"}]}";

            Assert.That(AgentJson.TryParse(json, room, out var reply, out var error), Is.True, error);
            Assert.That(reply.Actions.Count, Is.EqualTo(2));
            Assert.That(reply.Actions[0].Type, Is.EqualTo(AgentActionTypes.WalkTo));
            Assert.That(reply.Actions[1].Type, Is.EqualTo(AgentActionTypes.Sit));
        }

        [TestCase("{bad")]
        [TestCase("")]
        [TestCase("```json {} ```")]
        public void RejectsMalformedOrWrappedJson(string json)
        {
            Assert.That(AgentJson.TryParse(json, new RoomGraph(), out _, out _), Is.False);
        }

        [Test]
        public void RejectsUnsupportedAction()
        {
            const string json = "{\"speech\":\"\",\"emotion\":\"neutral\",\"actions\":[{\"type\":\"invoke_method\"}]}";
            Assert.That(AgentJson.TryParse(json, new RoomGraph(), out _, out var error), Is.False);
            StringAssert.Contains("Unsupported", error);
        }

        [Test]
        public void RejectsUnknownProperty()
        {
            const string json = "{\"speech\":\"\",\"emotion\":\"neutral\",\"method\":\"Delete\",\"actions\":[]}";
            Assert.That(AgentJson.TryParse(json, new RoomGraph(), out _, out var error), Is.False);
            StringAssert.Contains("Unknown JSON property", error);
        }

        [Test]
        public void RejectsMoreThanFiveActions()
        {
            const string json = "{\"speech\":\"\",\"emotion\":\"neutral\",\"actions\":[{\"type\":\"wave\"},{\"type\":\"wave\"},{\"type\":\"wave\"},{\"type\":\"wave\"},{\"type\":\"wave\"},{\"type\":\"wave\"}]}";
            Assert.That(AgentJson.TryParse(json, new RoomGraph(), out _, out var error), Is.False);
            StringAssert.Contains("maximum", error);
        }

        [Test]
        public void RejectsUnknownRoomTarget()
        {
            const string json = "{\"speech\":\"\",\"emotion\":\"neutral\",\"actions\":[{\"type\":\"sit\",\"target\":\"invented_sofa\"}]}";
            Assert.That(AgentJson.TryParse(json, new RoomGraph(), out _, out var error), Is.False);
            StringAssert.Contains("Unknown room target", error);
        }

        private RoomGraph CreateRoomWithSofa()
        {
            var root = new GameObject("sofa");
            objects.Add(root);
            var approach = new GameObject("approach").transform;
            approach.SetParent(root.transform);
            var sit = new GameObject("sit").transform;
            sit.SetParent(root.transform);
            var interaction = root.AddComponent<InteractionAnchor>();
            interaction.Configure(approach, sit);
            var node = root.AddComponent<RoomNode>();
            node.Configure("sofa_1", RoomNodeType.Sofa, true, approach, sit, interaction);
            return new RoomGraph(new[] { node });
        }
    }
}
