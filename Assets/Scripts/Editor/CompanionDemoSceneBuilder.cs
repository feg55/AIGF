using System.IO;
using System.Linq;
using Aigf.Companion.Agent;
using Aigf.Companion.Avatar;
using Aigf.Companion.Core;
using Aigf.Companion.Room;
using Aigf.Companion.UI;
using Aigf.Companion.Voice;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Aigf.Companion.Editor
{
    public static class CompanionDemoSceneBuilder
    {
        public const string DemoScenePath = "Assets/Scenes/CompanionDemo.unity";
        public const string ConfigAssetPath = "Assets/Settings/CompanionAppConfig.asset";
        public const string MaterialDirectory = "Assets/Settings/CompanionDemoMaterials";
        private static Font font;

        [InitializeOnLoadMethod]
        private static void CreateMissingDemoOnFirstImport()
        {
            if (!File.Exists(DemoScenePath))
            {
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(DemoScenePath))
                    {
                        CreateDemoSceneInternal();
                    }
                };
            }
        }

        [MenuItem("AIGF/Create or Rebuild Companion Demo Scene")]
        public static void CreateDemoScene()
        {
            if (File.Exists(DemoScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild demo scene", "Replace the existing CompanionDemo scene?", "Replace", "Cancel"))
            {
                return;
            }

            CreateDemoSceneInternal();
        }

        public static void CreateDemoSceneForAutomation()
        {
            CreateDemoSceneInternal();
        }

        private static void CreateDemoSceneInternal()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            var camera = CreateCamera();
            var roomProvider = CreateRoom(out var sofaNode, out var surface);
            var avatar = CreateAvatar();
            var debugUi = CreateDebugUI();
            var bootstrap = new GameObject("CompanionBootstrap").AddComponent<AppBootstrap>();
            var bootstrapSerialized = new SerializedObject(bootstrap);
            bootstrapSerialized.FindProperty("config").objectReferenceValue = EnsureConfigAsset();
            bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();

            roomProvider.SetNodes(new[] { sofaNode });
            surface.BuildNavMesh();
            EditorSceneManager.SaveScene(scene, DemoScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = avatar;
            Debug.Log($"[AI] Created Editor vertical slice at {DemoScenePath}. Camera={camera.name}, UI={debugUi.name}");
        }

        private static AppConfig EnsureConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AppConfig>(ConfigAssetPath);
            if (existing != null) return existing;
            var config = ScriptableObject.CreateInstance<AppConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            return config;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, -2f), Quaternion.identity);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static ManualRoomProvider CreateRoom(out RoomNode sofaNode, out NavMeshSurface surface)
        {
            var environment = new GameObject("ManualRoom");
            var provider = environment.AddComponent<ManualRoomProvider>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(environment.transform);
            floor.transform.SetPositionAndRotation(new Vector3(0f, -0.05f, 1f), Quaternion.identity);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            SetColor(floor, new Color(0.35f, 0.38f, 0.42f));

            var sofa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sofa.name = "Sofa_1";
            sofa.transform.SetParent(environment.transform);
            sofa.transform.SetPositionAndRotation(new Vector3(2f, 0.45f, 1.2f), Quaternion.identity);
            sofa.transform.localScale = new Vector3(2f, 0.9f, 0.8f);
            SetColor(sofa, new Color(0.14f, 0.36f, 0.55f));

            var approach = CreateAnchor("ApproachPoint", sofa.transform, new Vector3(2f, 0f, 0.25f), Quaternion.identity);
            var sit = CreateAnchor("SitPoint", sofa.transform, new Vector3(2f, 0.9f, 1.05f), Quaternion.Euler(0f, 180f, 0f));
            var interaction = sofa.AddComponent<InteractionAnchor>();
            interaction.Configure(approach, sit, sit);
            sofaNode = sofa.AddComponent<RoomNode>();
            sofaNode.Configure(
                "sofa_1",
                RoomNodeType.Sofa,
                true,
                approach,
                sit,
                interaction,
                sofa.GetComponent<Collider>(),
                "manual editor sofa");

            surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return provider;
        }

        private static GameObject CreateAvatar()
        {
            var root = new GameObject("GirlAvatar_Placeholder");
            root.transform.position = new Vector3(-1f, 0f, 0.5f);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 1.75f;
            agent.speed = 1.4f;
            agent.stoppingDistance = 0.1f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlaceholderBody";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) Object.DestroyImmediate(visualCollider);
            SetColor(visual, new Color(0.74f, 0.4f, 0.62f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            head.transform.localScale = Vector3.one * 0.38f;
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null) Object.DestroyImmediate(headCollider);
            SetColor(head, new Color(0.92f, 0.72f, 0.65f));

            root.AddComponent<GirlAnimator>();
            root.AddComponent<GirlNavigation>();
            root.AddComponent<AvatarInteraction>();
            var look = root.AddComponent<LookAtUser>();
            var lookSerialized = new SerializedObject(look);
            lookSerialized.FindProperty("headBone").objectReferenceValue = head.transform;
            lookSerialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<ActionExecutor>();
            root.AddComponent<GirlBrain>();
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            root.AddComponent<MockTts>();
            root.AddComponent<AvatarLipSync>();
            return root;
        }

        private static BrainDebugUI CreateDebugUI()
        {
            var canvasObject = new GameObject("CompanionDebugCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateUiObject("Panel", canvasObject.transform, typeof(Image));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(20f, -20f), new Vector2(700f, 970f), new Vector2(0f, 1f));
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.88f);

            var debug = panel.AddComponent<BrainDebugUI>();
            var input = CreateInput(panel.transform, new Vector2(20f, -20f), new Vector2(520f, 60f));
            var send = CreateButton(panel.transform, "Send", new Vector2(555f, -20f), new Vector2(125f, 60f));
            var status = CreateText(panel.transform, "LLM: waiting", new Vector2(20f, -100f), new Vector2(660f, 36f), 24);
            var state = CreateText(panel.transform, "State:", new Vector2(20f, -142f), new Vector2(320f, 32f), 22);
            var action = CreateText(panel.transform, "Action:", new Vector2(350f, -142f), new Vector2(330f, 32f), 22);
            var room = CreateText(panel.transform, "Room nodes:", new Vector2(20f, -180f), new Vector2(320f, 32f), 22);
            var target = CreateText(panel.transform, "Target:", new Vector2(350f, -180f), new Vector2(330f, 32f), 22);
            var latency = CreateText(panel.transform, "Inference:", new Vector2(20f, -218f), new Vector2(660f, 32f), 22);
            var error = CreateText(panel.transform, "Last error:", new Vector2(20f, -256f), new Vector2(660f, 58f), 20);
            error.color = new Color(1f, 0.55f, 0.55f);
            var json = CreateText(panel.transform, "Last JSON:", new Vector2(20f, -330f), new Vector2(660f, 370f), 18);

            var labels = new[] { "Come here", "Sit", "Stand", "Follow", "Stop", "Wave" };
            for (var i = 0; i < labels.Length; i++)
            {
                var button = CreateButton(panel.transform, labels[i], new Vector2(20f + (i % 3) * 220f, -730f - (i / 3) * 72f), new Vector2(200f, 56f));
                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(button.onClick, debug.SendComeHere); break;
                    case 1: UnityEventTools.AddPersistentListener(button.onClick, debug.SendSit); break;
                    case 2: UnityEventTools.AddPersistentListener(button.onClick, debug.SendStand); break;
                    case 3: UnityEventTools.AddPersistentListener(button.onClick, debug.SendFollow); break;
                    case 4: UnityEventTools.AddPersistentListener(button.onClick, debug.SendStop); break;
                    case 5: UnityEventTools.AddPersistentListener(button.onClick, debug.SendWave); break;
                }
            }

            debug.Configure(input, send, status, json, action, state, room, target, error, latency);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(canvasObject.transform.parent);
            return debug;
        }

        private static InputField CreateInput(Transform parent, Vector2 position, Vector2 size)
        {
            var root = CreateUiObject("CommandInput", parent, typeof(Image), typeof(InputField));
            SetRect(root.GetComponent<RectTransform>(), position, size, new Vector2(0f, 1f));
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            var text = CreateText(root.transform, string.Empty, new Vector2(12f, -5f), new Vector2(size.x - 24f, size.y - 10f), 24);
            text.alignment = TextAnchor.MiddleLeft;
            var placeholder = CreateText(root.transform, "Type Russian or English command", new Vector2(12f, -5f), new Vector2(size.x - 24f, size.y - 10f), 22);
            placeholder.color = new Color(1f, 1f, 1f, 0.42f);
            placeholder.fontStyle = FontStyle.Italic;
            var input = root.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
        {
            var root = CreateUiObject(label + "Button", parent, typeof(Image), typeof(Button));
            SetRect(root.GetComponent<RectTransform>(), position, size, new Vector2(0f, 1f));
            root.GetComponent<Image>().color = new Color(0.16f, 0.46f, 0.72f, 0.95f);
            var text = CreateText(root.transform, label, Vector2.zero, size, 22);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            return root.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var root = CreateUiObject("Text", parent, typeof(Text));
            SetRect(root.GetComponent<RectTransform>(), position, size, new Vector2(0f, 1f));
            var text = root.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.text = value;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
        {
            var root = new GameObject(name, components.Prepend(typeof(RectTransform)).ToArray());
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Transform CreateAnchor(string name, Transform parent, Vector3 worldPosition, Quaternion worldRotation)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent);
            anchor.SetPositionAndRotation(worldPosition, worldRotation);
            return anchor;
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
        }

        private static void SetColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            if (!AssetDatabase.IsValidFolder(MaterialDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "CompanionDemoMaterials");
            }

            var path = $"{MaterialDirectory}/{target.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = target.name, color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.color = color;
                EditorUtility.SetDirty(material);
            }

            renderer.sharedMaterial = material;
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != DemoScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(DemoScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
