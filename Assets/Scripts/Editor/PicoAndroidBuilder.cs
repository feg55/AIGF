using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Aigf.Companion.AI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aigf.Companion.Editor
{
    public static class PicoAndroidBuilder
    {
        public const string OutputPath = "Builds/MintARCompanion.apk";
        private const long MinimumCleanBuildFreeDiskBytes = 12L * 1024L * 1024L * 1024L;
        private const long MinimumIncrementalBuildFreeDiskBytes = 6L * 1024L * 1024L * 1024L;
        private const string NativePluginPath =
            "Assets/Plugins/Android/arm64-v8a/libgirlfriend_ai.so";

        [MenuItem("AIGF/Validate PICO Release")]
        public static void ValidateFromMenu()
        {
            ValidateReleaseAssets();
            Debug.Log("[BUILD] PICO release assets are valid.");
        }

        [MenuItem("AIGF/Build PICO 4 Release APK")]
        public static void BuildReleaseApk()
        {
            BuildReleaseApkForAutomation();
        }

        public static void BuildReleaseApkForAutomation()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android))
            {
                throw new InvalidOperationException("Could not switch the active build target to Android.");
            }

            PicoProjectConfigurator.ConfigureForAutomation();
            ValidateReleaseAssets();
            ValidateFreeDiskSpace();

            var output = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { CompanionDemoSceneBuilder.DemoScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"PICO APK build failed: {report.summary.result}; " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            }

            Debug.Log(
                $"[BUILD] PICO release APK: {output} " +
                $"({report.summary.totalSize / (1024f * 1024f):0.0} MiB)");
        }

        private static void ValidateFreeDiskSpace()
        {
            var projectRoot = Path.GetFullPath(".");
            var driveRoot = Path.GetPathRoot(projectRoot);
            if (string.IsNullOrEmpty(driveRoot)) return;

            var available = new DriveInfo(driveRoot).AvailableFreeSpace;
            var hasAndroidBuildCache = Directory.Exists(Path.Combine(projectRoot, "Library", "Bee", "Android"));
            var required = hasAndroidBuildCache
                ? MinimumIncrementalBuildFreeDiskBytes
                : MinimumCleanBuildFreeDiskBytes;
            if (available >= required) return;

            throw new BuildFailedException(
                $"Not enough free disk space for the Android build. " +
                $"Free at least {required / (1024L * 1024L * 1024L)} GiB on {driveRoot} and retry; " +
                $"currently available: {available / (1024f * 1024f * 1024f):0.0} GiB.");
        }

        public static void ValidateReleaseAssets()
        {
            if (!File.Exists(CompanionDemoSceneBuilder.DemoScenePath))
                throw new BuildFailedException("CompanionMR scene is missing. Rebuild it from the AIGF menu.");
            if (!File.Exists(MintAvatarAssetBuilder.PrefabPath))
                throw new BuildFailedException("Mint runtime prefab is missing.");
            RequireProjectFile(MintAvatarAssetBuilder.MotionModelPath);
            ValidateMintAnimations();
            if (!File.Exists(NativePluginPath))
                throw new BuildFailedException($"Native llama.cpp plugin is missing: {NativePluginPath}");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "android.permission.RECORD_AUDIO");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "com.picovr.permission.SPATIAL_DATA");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "android.hardware.vr.headtracking");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "pvr.app.type");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "enable_vst");
            RequireText(
                "Assets/Plugins/Android/AndroidManifest.xml",
                "com.picovr.intent.category.VR");
            RequireProjectFile("Assets/Plugins/SherpaOnnx/sherpa-onnx.dll");
            RequireProjectFile("Assets/Plugins/SherpaOnnx/Android/arm64-v8a/libonnxruntime.so");
            RequireProjectFile("Assets/Plugins/SherpaOnnx/Android/arm64-v8a/libsherpa-onnx-c-api.so");
            RequireProjectFile("Assets/Plugins/SherpaOnnx/Android/arm64-v8a/libsherpa-onnx-cxx-api.so");
            RequireProjectFile("Assets/Plugins/SherpaOnnx/Android/arm64-v8a/libsherpa-onnx-jni.so");

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var defines);
            if (!defines.Contains("SHERPA_ONNX"))
                throw new BuildFailedException("Android scripting define SHERPA_ONNX is missing.");

            var manifestPath = Path.Combine(
                Application.streamingAssetsPath,
                LocalModelPathResolver.ModelsDirectoryName,
                LocalModelPathResolver.ManifestFilename);
            if (!File.Exists(manifestPath))
                throw new BuildFailedException($"Local model manifest is missing: {manifestPath}");
            if (!ModelManifest.TryParse(
                    File.ReadAllText(manifestPath),
                    out var manifest,
                    out var error))
                throw new BuildFailedException($"Invalid local model manifest: {error}");

            var modelPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, manifest.Filename);
            if (!File.Exists(modelPath))
                throw new BuildFailedException($"Local GGUF is missing: {modelPath}");

            var modelInfo = new FileInfo(modelPath);
            if (manifest.SizeBytes <= 0 || modelInfo.Length != manifest.SizeBytes)
            {
                throw new BuildFailedException(
                    $"GGUF size mismatch. Expected {manifest.SizeBytes}, found {modelInfo.Length}.");
            }

            using (var stream = File.OpenRead(modelPath))
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
                if (!string.Equals(actual, manifest.Sha256, StringComparison.Ordinal))
                    throw new BuildFailedException("GGUF SHA-256 does not match model-manifest.json.");
            }

            RequireVoiceFile("SherpaOnnx/asr-settings.json");
            RequireVoiceFile("SherpaOnnx/vad-settings.json");
            RequireVoiceFile("SherpaOnnx/tts-settings.json");
            RequireVoiceFile("SherpaOnnx/microphone-settings.json");
            RequireVoiceFile("SherpaOnnx/asr-models/sherpa-onnx-whisper-tiny/tiny-encoder.onnx");
            RequireVoiceFile("SherpaOnnx/asr-models/sherpa-onnx-whisper-tiny/tiny-decoder.onnx");
            RequireVoiceFile("SherpaOnnx/vad-models/silero-vad/silero_vad.onnx");
            const string supertonic =
                "SherpaOnnx/tts-models/sherpa-onnx-supertonic-3-tts-int8-2026-05-11/";
            RequireVoiceFile(supertonic + "duration_predictor.int8.onnx");
            RequireVoiceFile(supertonic + "text_encoder.int8.onnx");
            RequireVoiceFile(supertonic + "vector_estimator.int8.onnx");
            RequireVoiceFile(supertonic + "vocoder.int8.onnx");
            RequireVoiceFile(supertonic + "tts.json");
            RequireVoiceFile(supertonic + "unicode_indexer.bin");
            RequireVoiceFile(supertonic + "voice.bin");
        }

        private static void RequireVoiceFile(string relativePath)
        {
            var path = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(path))
                throw new BuildFailedException($"Offline voice asset is missing: {relativePath}");
        }

        private static void ValidateMintAnimations()
        {
            var required = new[]
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
            var clips = AssetDatabase.LoadAllAssetsAtPath(MintAvatarAssetBuilder.MotionModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            for (var i = 0; i < required.Length; i++)
            {
                var expected = required[i];
                var clip = clips.FirstOrDefault(candidate =>
                    string.Equals(candidate.name, expected, StringComparison.OrdinalIgnoreCase) ||
                    candidate.name.EndsWith(expected, StringComparison.OrdinalIgnoreCase));
                if (clip == null)
                    throw new BuildFailedException($"Required Mint animation is missing: {expected}");
                if (!clip.humanMotion)
                    throw new BuildFailedException($"Mint animation was not imported as Humanoid: {clip.name}");
            }
        }

        private static void RequireProjectFile(string assetPath)
        {
            if (!File.Exists(assetPath))
                throw new BuildFailedException($"Required release asset is missing: {assetPath}");
        }

        private static void RequireText(string assetPath, string requiredText)
        {
            RequireProjectFile(assetPath);
            if (!File.ReadAllText(assetPath).Contains(requiredText))
                throw new BuildFailedException($"{assetPath} is missing required text: {requiredText}");
        }
    }
}
