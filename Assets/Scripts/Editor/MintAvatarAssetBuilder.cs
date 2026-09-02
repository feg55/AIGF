using System;
using System.Collections.Generic;
using System.IO;
using Aigf.Companion.Agent;
using Aigf.Companion.Avatar;
using Aigf.Companion.Voice;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Aigf.Companion.Editor
{
    public sealed class MintAvatarImportProcessor : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Avatars/Mint/Model/Mint_Optimized.fbx";
        private const string MotionModelPath = "Assets/Resources/CompanionAnimations/Mint_Motion_CC0.fbx";

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (string.Equals(assetPath, MotionModelPath, StringComparison.Ordinal))
            {
                ConfigureMotionImporter(importer);
                return;
            }
            if (!string.Equals(assetPath, ModelPath, StringComparison.Ordinal)) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.maxBonesPerVertex = 4;
            importer.optimizeGameObjects = false;
        }

        private static void ConfigureMotionImporter(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = true;
            importer.importAnimatedCustomProperties = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importConstraints = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.removeConstantScaleCurves = true;
            importer.optimizeGameObjects = true;

            var clips = importer.defaultClipAnimations;
            for (var i = 0; i < clips.Length; i++)
            {
                var name = clips[i].name ?? string.Empty;
                var loop = name.EndsWith("Mint_Idle", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith("Mint_Walk", StringComparison.OrdinalIgnoreCase) ||
                           name.EndsWith("Mint_SitIdle", StringComparison.OrdinalIgnoreCase);
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = false;
                clips[i].keepOriginalPositionY = false;
                clips[i].keepOriginalPositionXZ = false;
                clips[i].heightFromFeet = true;
            }
            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Avatars/Mint/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = assetPath.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      assetPath.IndexOf("eyes", StringComparison.OrdinalIgnoreCase) >= 0
                ? 2048
                : 1024;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            android.compressionQuality = 50;
            importer.SetPlatformTextureSettings(android);
        }
    }

    public static class MintAvatarAssetBuilder
    {
        public const string ModelPath = "Assets/Avatars/Mint/Model/Mint_Optimized.fbx";
        public const string PrefabPath = "Assets/Avatars/Mint/Prefabs/MintCompanion.prefab";
        public const string MotionModelPath = "Assets/Resources/CompanionAnimations/Mint_Motion_CC0.fbx";
        private const string MaterialsDirectory = "Assets/Avatars/Mint/Materials";
        private const string TexturesDirectory = "Assets/Avatars/Mint/Textures";

        [MenuItem("AIGF/Rebuild Mint Companion Prefab")]
        public static void Build()
        {
            BuildForAutomation();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        public static void BuildForAutomation()
        {
            EnsureFolder("Assets/Avatars/Mint", "Materials");
            EnsureFolder("Assets/Avatars/Mint", "Prefabs");
            AssetDatabase.ImportAsset(MotionModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException($"Mint model is missing at {ModelPath}");

            var root = new GameObject("MintCompanion");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "MintVisual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                ConfigureMaterials(visual);
                ConfigureLods(root, visual);
                ConfigureRuntime(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Unity failed to save the Mint companion prefab.");

                var animator = prefab.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    Debug.LogError("[AVATAR] Mint imported, but Unity Humanoid mapping is incomplete. Body animation stays disabled to protect the rig.");
                }
                else
                {
                    Debug.Log("[AVATAR] Mint prefab built with authored CC0 Humanoid motion and 3 LOD levels.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ConfigureRuntime(GameObject root)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.24f;
            agent.height = 1.63f;
            agent.baseOffset = 0f;
            agent.speed = 1.35f;
            agent.angularSpeed = 240f;
            agent.acceleration = 5f;
            agent.stoppingDistance = 0.1f;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 0.4f;
            audio.maxDistance = 8f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;

            root.AddComponent<MintFacialDriver>();
            root.AddComponent<CompanionAnimationPlayer>();
            root.AddComponent<GirlAnimator>();
            root.AddComponent<GirlNavigation>();
            root.AddComponent<AvatarInteraction>();
            root.AddComponent<LookAtUser>();
            root.AddComponent<ActionExecutor>();
            root.AddComponent<GirlBrain>();
            root.AddComponent<SherpaTtsAdapter>();
        }

        private static void ConfigureLods(GameObject root, GameObject visual)
        {
            var importedGroup = visual.GetComponentInChildren<LODGroup>(true);
            if (importedGroup != null)
            {
                importedGroup.animateCrossFading = false;
                importedGroup.fadeMode = LODFadeMode.None;
                return;
            }

            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var lodRenderers = new[]
            {
                new List<Renderer>(),
                new List<Renderer>(),
                new List<Renderer>()
            };
            for (var i = 0; i < renderers.Length; i++)
            {
                var index = renderers[i].name.IndexOf("LOD2", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 :
                    renderers[i].name.IndexOf("LOD1", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
                lodRenderers[index].Add(renderers[i]);
                renderers[i].updateWhenOffscreen = false;
                renderers[i].skinnedMotionVectors = false;
                renderers[i].allowOcclusionWhenDynamic = true;
            }

            var group = root.AddComponent<LODGroup>();
            group.animateCrossFading = false;
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[]
            {
                new LOD(0.55f, lodRenderers[0].ToArray()),
                new LOD(0.24f, lodRenderers[1].ToArray()),
                new LOD(0.08f, lodRenderers[2].ToArray())
            });
            group.RecalculateBounds();
        }

        private static void ConfigureMaterials(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var byName = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterials;
                var replacements = new Material[shared.Length];
                for (var j = 0; j < shared.Length; j++)
                {
                    var sourceName = shared[j] != null ? shared[j].name : $"MintMaterial_{j}";
                    if (!byName.TryGetValue(sourceName, out var material))
                    {
                        material = CreateOrUpdateMaterial(sourceName);
                        byName.Add(sourceName, material);
                    }
                    replacements[j] = material;
                }
                renderers[i].sharedMaterials = replacements;
            }
        }

        private static Material CreateOrUpdateMaterial(string sourceName)
        {
            var safeName = Sanitize(sourceName);
            var path = $"{MaterialsDirectory}/{safeName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (material == null)
            {
                material = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            var texture = FindTexture(sourceName);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", sourceName.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.28f : 0.05f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FindTexture(string materialName)
        {
            var key = materialName.ToLowerInvariant();
            var filename = key.Contains("eyes") ? "T_player_019_mint_eyes_d.png" :
                key.Contains("face") ? "T_player_019_mint_face_d1.png" :
                key.Contains("hair_02") || key.Contains("hair02") ? "T_player_019_mint_hair_02_d.png" :
                key.Contains("hair") ? "T_019_mint_swimsuit_hair_01_d.png" :
                key.Contains("03") ? "T_player_019_mint_swimsuit_03_d.png" :
                key.Contains("02") ? "T_player_019_mint_swimsuit_02_d.png" :
                "T_player_019_mint_swimsuit_01_d.png";
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDirectory}/{filename}");
        }

        private static string Sanitize(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "MintMaterial" : value;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
