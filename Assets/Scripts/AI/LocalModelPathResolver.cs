using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Aigf.Companion.AI
{
    public static class LocalModelPathResolver
    {
        public const string ModelsDirectoryName = "Models";
        public const string ManifestFilename = "model-manifest.json";

        public static string GetEditorModelPath(string modelFilename)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                ModelsDirectoryName,
                modelFilename));
        }

        public static string GetBundledModelRelativePath(string modelFilename)
        {
            return $"{ModelsDirectoryName}/{modelFilename}";
        }

        public static string GetRuntimeModelPath(string modelFilename)
        {
            return Path.GetFullPath(Path.Combine(
                Application.persistentDataPath,
                "models",
                modelFilename));
        }

        public static async Task<ModelManifest> LoadBundledManifestAsync(
            CancellationToken cancellationToken = default)
        {
            var relativePath = $"{ModelsDirectoryName}/{ManifestFilename}";
            var json = await ReadStreamingTextAsync(relativePath, cancellationToken);
            if (!ModelManifest.TryParse(json, out var manifest, out var error))
            {
                throw new InvalidDataException(error);
            }

            return manifest;
        }

        public static Task<ModelInstallResult> EnsureRuntimeModelAvailableAsync(
            ModelManifest manifest,
            CancellationToken cancellationToken = default)
        {
            return new BundledModelInstaller(manifest).EnsureInstalledAsync(cancellationToken);
        }

        internal static async Task<string> ReadStreamingTextAsync(
            string relativePath,
            CancellationToken cancellationToken)
        {
            var path = CombineStreamingPath(relativePath);
            if (Application.isEditor || Application.platform != RuntimePlatform.Android)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllText(path);
            }

            using (var request = UnityWebRequest.Get(path))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new IOException($"Could not read bundled asset '{relativePath}': {request.error}");
                }

                return request.downloadHandler.text;
            }
        }

        internal static string CombineStreamingPath(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            var root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            return $"{root}/{normalized}";
        }
    }

    public sealed class ModelInstallResult
    {
        public bool Succeeded { get; }
        public string ModelPath { get; }
        public string Error { get; }

        private ModelInstallResult(bool succeeded, string path, string error)
        {
            Succeeded = succeeded;
            ModelPath = path ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public static ModelInstallResult Success(string path) => new ModelInstallResult(true, path, string.Empty);
        public static ModelInstallResult Failure(string error) => new ModelInstallResult(false, string.Empty, error);
    }
}
