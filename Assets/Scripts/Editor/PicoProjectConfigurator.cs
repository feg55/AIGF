using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using Unity.XR.PXR;

namespace Aigf.Companion.Editor
{
    public static class PicoProjectConfigurator
    {
        private const string XrSettingsPath = "Assets/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem("AIGF/Configure Project for PICO 4")]
        public static void Configure()
        {
            ConfigureForAutomation();
            Debug.Log("[PICO] Android, XR loader, passthrough and Scene Capture settings configured.");
        }

        public static void ConfigureForAutomation()
        {
            PlayerSettings.companyName = "AIGF";
            PlayerSettings.productName = "Mint AR Companion";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.aigf.mintcompanion");
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.resizeableActivity = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            EnsureScriptingDefine(NamedBuildTarget.Android, "SHERPA_ONNX");
            EnsureScriptingDefine(NamedBuildTarget.Standalone, "SHERPA_ONNX");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            DisableHdrForPico();

            ConfigureXrLoader();
            var project = PXR_ProjectSetting.GetProjectConfig();
            var serialized = new SerializedObject(project);
            SetBool(serialized, "videoSeeThrough", true);
            SetBool(serialized, "sceneCapture", true);
            SetBool(serialized, "handTracking", true);
            SetBool(serialized, "spatialAnchor", false);
            SetBool(serialized, "spatialMesh", false);
            SetBool(serialized, "openMRC", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(project);

            var settings = PXR_Settings.GetSettings() ??
                AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/XR/Settings/PXR_Settings.asset");
            if (settings != null)
            {
                settings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
                settings.optimizeBufferDiscards = true;
                EditorUtility.SetDirty(settings);
                EditorBuildSettings.AddConfigObject("Unity.XR.PXR.Settings", settings, true);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureXrLoader()
        {
            var perTarget = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
                .Select(guid => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
            }

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                AssetDatabase.AddObjectToAsset(general, perTarget);
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                general.Manager = manager;
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }

            general.InitManagerOnStart = true;
            if (general.Manager != null)
            {
                general.Manager.automaticLoading = true;
                general.Manager.automaticRunning = true;
                for (var i = general.Manager.activeLoaders.Count - 1; i >= 0; i--)
                {
                    var loader = general.Manager.activeLoaders[i];
                    if (loader != null && !(loader is PXR_Loader))
                    {
                        XRPackageMetadataStore.RemoveLoader(general.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
                    }
                }
                if (general.Manager.activeLoaders.All(loader => !(loader is PXR_Loader)))
                {
                    XRPackageMetadataStore.AssignLoader(general.Manager, "PXR_Loader", BuildTargetGroup.Android);
                }
            }

            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(general);
        }

        private static void SetBool(SerializedObject target, string name, bool value)
        {
            var property = target.FindProperty(name);
            if (property != null) property.boolValue = value;
        }

        private static void DisableHdrForPico()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                var serialized = new SerializedObject(asset);
                var supportsHdr = serialized.FindProperty("m_SupportsHDR");
                if (supportsHdr == null || !supportsHdr.boolValue) continue;
                supportsHdr.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        private static void EnsureScriptingDefine(NamedBuildTarget target, string define)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            if (symbols.Contains(define)) return;
            PlayerSettings.SetScriptingDefineSymbols(target, symbols.Append(define).ToArray());
        }
    }
}
