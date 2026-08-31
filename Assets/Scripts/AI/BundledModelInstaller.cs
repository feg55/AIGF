using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Aigf.Companion.AI
{
    public sealed class BundledModelInstaller
    {
        private const string InstalledManifestFilename = "installed-manifest.json";
        private readonly ModelManifest manifest;

        public BundledModelInstaller(ModelManifest modelManifest)
        {
            manifest = modelManifest ?? throw new ArgumentNullException(nameof(modelManifest));
        }

        public async Task<ModelInstallResult> EnsureInstalledAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Application.isEditor)
                {
                    var editorPath = LocalModelPathResolver.GetEditorModelPath(manifest.Filename);
                    return File.Exists(editorPath)
                        ? ModelInstallResult.Success(editorPath)
                        : ModelInstallResult.Failure($"GGUF is missing at canonical project path: {editorPath}");
                }

                if (Application.platform != RuntimePlatform.Android)
                {
                    var bundledPath = LocalModelPathResolver.CombineStreamingPath(
                        LocalModelPathResolver.GetBundledModelRelativePath(manifest.Filename));
                    return File.Exists(bundledPath)
                        ? ModelInstallResult.Success(bundledPath)
                        : ModelInstallResult.Failure($"Bundled GGUF is missing: {bundledPath}");
                }

                var destination = LocalModelPathResolver.GetRuntimeModelPath(manifest.Filename);
                var directory = Path.GetDirectoryName(destination);
                if (string.IsNullOrEmpty(directory))
                {
                    return ModelInstallResult.Failure("Permanent model directory could not be resolved.");
                }

                Directory.CreateDirectory(directory);
                var installedManifestPath = Path.Combine(directory, InstalledManifestFilename);
                if (File.Exists(destination) && File.Exists(installedManifestPath))
                {
                    var installedJson = File.ReadAllText(installedManifestPath);
                    if (ModelManifest.TryParse(installedJson, out var installed, out _) &&
                        installed.Id == manifest.Id &&
                        installed.Version == manifest.Version &&
                        installed.Filename == manifest.Filename &&
                        installed.Sha256 == manifest.Sha256 &&
                        await VerifyFileAsync(destination, cancellationToken))
                    {
                        return ModelInstallResult.Success(destination);
                    }
                }

                var stagingPath = destination + ".installing";
                if (File.Exists(stagingPath)) File.Delete(stagingPath);
                var bundledUri = LocalModelPathResolver.CombineStreamingPath(
                    LocalModelPathResolver.GetBundledModelRelativePath(manifest.Filename));

                using (var request = UnityWebRequest.Get(bundledUri))
                {
                    request.downloadHandler = new DownloadHandlerFile(stagingPath);
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (File.Exists(stagingPath)) File.Delete(stagingPath);
                        return ModelInstallResult.Failure($"Bundled GGUF installation failed: {request.error}");
                    }
                }

                if (!await VerifyFileAsync(stagingPath, cancellationToken))
                {
                    if (File.Exists(stagingPath)) File.Delete(stagingPath);
                    return ModelInstallResult.Failure("Bundled GGUF failed size or SHA-256 validation.");
                }

                if (File.Exists(destination)) File.Delete(destination);
                File.Move(stagingPath, destination);
                File.WriteAllText(installedManifestPath, manifest.ToJson(true));
                return ModelInstallResult.Success(destination);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ModelInstallResult.Failure(exception.Message);
            }
        }

        private Task<bool> VerifyFileAsync(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                if (!info.Exists || (manifest.SizeBytes > 0 && info.Length != manifest.SizeBytes))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(manifest.Sha256))
                {
                    return true;
                }

                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(stream);
                    var actual = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                    return actual == manifest.Sha256;
                }
            }, cancellationToken);
        }
    }
}
