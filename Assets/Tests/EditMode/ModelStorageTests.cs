using System.IO;
using Aigf.Companion.AI;
using NUnit.Framework;
using UnityEngine;

namespace Aigf.Companion.Tests
{
    public sealed class ModelStorageTests
    {
        [Test]
        public void ParsesModelManifest()
        {
            const string json = "{\"id\":\"small-model\",\"filename\":\"small.gguf\",\"version\":2,\"sha256\":\"\",\"sizeBytes\":42,\"contextSize\":2048,\"maxTokens\":128,\"quantization\":\"Q4\"}";
            Assert.That(ModelManifest.TryParse(json, out var manifest, out var error), Is.True, error);
            Assert.That(manifest.Filename, Is.EqualTo("small.gguf"));
            Assert.That(manifest.SizeBytes, Is.EqualTo(42));
        }

        [Test]
        public void RejectsManifestPathTraversal()
        {
            const string json = "{\"id\":\"bad\",\"filename\":\"../bad.gguf\",\"version\":1,\"contextSize\":2048,\"maxTokens\":1}";
            Assert.That(ModelManifest.TryParse(json, out _, out _), Is.False);
        }

        [Test]
        public void EditorPathPointsDirectlyIntoProjectStreamingAssets()
        {
            var path = LocalModelPathResolver.GetEditorModelPath("model.gguf");
            var expected = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Models",
                "model.gguf"));
            Assert.That(path, Is.EqualTo(expected));
        }

        [Test]
        public void BundledRelativePathIsPortable()
        {
            Assert.That(LocalModelPathResolver.GetBundledModelRelativePath("model.gguf"), Is.EqualTo("Models/model.gguf"));
        }
    }
}
