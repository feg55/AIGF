using System.Collections;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using Aigf.Companion.Voice;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Aigf.Companion.Tests
{
    public sealed class CompanionSceneSmokeTests
    {
        [Test]
        public void EmptyRoomStillBuildsFallbackNavigationFloor()
        {
            var root = new GameObject("Fallback NavMesh Test");
            var surface = root.AddComponent<NavMeshSurface>();
            var builder = root.AddComponent<RoomNavMeshBuilder>();
            try
            {
                Assert.That(builder.Rebuild(new RoomGraph()), Is.True);
                Assert.That(NavMesh.CalculateTriangulation().vertices.Length, Is.GreaterThanOrEqualTo(3));
            }
            finally
            {
                surface.RemoveData();
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator GeneratedSceneExecutesCoreMockCommands()
        {
            // This suite exercises scene navigation and authored motion. Android
            // speech plugins are tested on the headset, not loaded in Windows.
            SceneManager.sceneLoaded += DisableDeviceSpeech;
            SceneManager.LoadScene("CompanionMR", LoadSceneMode.Single);
            yield return null;

            var bootstrapDeadline = Time.realtimeSinceStartup + 8f;
            AppBootstrap bootstrap = null;
            while (Time.realtimeSinceStartup < bootstrapDeadline)
            {
                bootstrap = Object.FindAnyObjectByType<AppBootstrap>();
                if (bootstrap != null && bootstrap.IsInitialized) break;
                yield return null;
            }

            Assert.That(bootstrap, Is.Not.Null, "Demo scene has no AppBootstrap.");
            Assert.That(bootstrap.IsInitialized, Is.True, "Companion bootstrap did not initialize.");
            var brain = Object.FindAnyObjectByType<GirlBrain>();
            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.Room.Count, Is.GreaterThanOrEqualTo(1));

            var come = brain.ProcessUserMessageAsync("come here");
            yield return WaitFor(come, 10f);
            Assert.That(come.Result.Succeeded, Is.True, come.Result.Message);
            var camera = Camera.main;
            var avatarFlat = brain.transform.position;
            var userFlat = camera.transform.position;
            avatarFlat.y = 0f;
            userFlat.y = 0f;
            Assert.That(Vector3.Distance(avatarFlat, userFlat), Is.LessThan(1.25f));

            var wave = brain.ProcessUserMessageAsync("wave");
            yield return WaitFor(wave, 3f);
            Assert.That(wave.Result.Succeeded, Is.True, wave.Result.Message);

            var sit = brain.ProcessUserMessageAsync("sit on the sofa");
            yield return WaitFor(sit, 12f);
            Assert.That(sit.Result.Succeeded, Is.True, sit.Result.Message);
            Assert.That(brain.CurrentState, Is.EqualTo(AgentState.Sitting));
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Dot(brain.transform.up, Vector3.up), Is.GreaterThan(0.99f));
            var humanoid = brain.GetComponentInChildren<Animator>();
            if (humanoid != null && humanoid.isHuman)
            {
                var thigh = humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var knee = humanoid.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Debug.Log($"[TEST SEAT] knee={brain.transform.InverseTransformVector(knee.position - thigh.position)} " +
                    $"hips={brain.transform.InverseTransformPoint(humanoid.GetBoneTransform(HumanBodyBones.Hips).position)} " +
                    $"visualRotation={humanoid.transform.localEulerAngles} culling={humanoid.cullingMode}");
                Assert.That(Vector3.Dot(knee.position - thigh.position, brain.transform.forward), Is.GreaterThan(0.1f),
                    "Seated knees must extend in the avatar's facing direction.");
            }

            var stand = brain.ProcessUserMessageAsync("stand up");
            yield return WaitFor(stand, 5f);
            Assert.That(stand.Result.Succeeded, Is.True, stand.Result.Message);
            Assert.That(brain.CurrentState, Is.EqualTo(AgentState.Idle));

            var follow = brain.ProcessUserMessageAsync("follow me");
            yield return WaitFor(follow, 3f);
            Assert.That(follow.Result.Succeeded, Is.True, follow.Result.Message);
            Assert.That(brain.CurrentState, Is.EqualTo(AgentState.Following));

            var stop = brain.ProcessUserMessageAsync("stop");
            yield return WaitFor(stop, 3f);
            Assert.That(stop.Result.Succeeded, Is.True, stop.Result.Message);
            Assert.That(brain.CurrentState, Is.EqualTo(AgentState.Idle));

            var look = brain.ProcessUserMessageAsync("look at me");
            yield return WaitFor(look, 3f);
            Assert.That(look.Result.Succeeded, Is.True, look.Result.Message);
        }

        private static void DisableDeviceSpeech(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= DisableDeviceSpeech;
            foreach (var input in Object.FindObjectsByType<SherpaVoiceInput>(FindObjectsSortMode.None)) input.enabled = false;
            foreach (var tts in Object.FindObjectsByType<SherpaTtsAdapter>(FindObjectsSortMode.None)) tts.enabled = false;
        }

        private static IEnumerator WaitFor(Task<ActionResult> task, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "Command timed out.");
            Assert.That(task.IsFaulted, Is.False, task.Exception != null ? task.Exception.ToString() : string.Empty);
        }
    }
}
