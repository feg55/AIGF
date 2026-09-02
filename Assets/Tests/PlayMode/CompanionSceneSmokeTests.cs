using System.Collections;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Aigf.Companion.Tests
{
    public sealed class CompanionSceneSmokeTests
    {
        [UnityTest]
        [Timeout(30000)]
        public IEnumerator GeneratedSceneExecutesCoreMockCommands()
        {
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
