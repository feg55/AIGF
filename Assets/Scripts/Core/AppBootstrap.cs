using System;
using Aigf.Companion.AI;
using Aigf.Companion.Agent;
using Aigf.Companion.Avatar;
using Aigf.Companion.Memory;
using Aigf.Companion.Pico;
using Aigf.Companion.Room;
using Aigf.Companion.UI;
using Aigf.Companion.Voice;
using UnityEngine;

namespace Aigf.Companion.Core
{
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private AppConfig config;
        [SerializeField] private Camera userCamera;
        [SerializeField] private MonoBehaviour roomProviderComponent;
        [SerializeField] private RoomNavMeshBuilder roomNavMeshBuilder;
        [SerializeField] private PicoPassthroughAdapter passthrough;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private GirlBrain brain;
        [SerializeField] private ActionExecutor actionExecutor;
        [SerializeField] private GirlNavigation navigation;
        [SerializeField] private AvatarInteraction avatarInteraction;
        [SerializeField] private LookAtUser lookAtUser;
        [SerializeField] private MonoBehaviour ttsComponent;
        [SerializeField] private BrainDebugUI debugUI;

        public bool IsInitialized { get; private set; }

        private async void Start()
        {
            try
            {
                ResolveSceneReferences();
                if (config == null) config = AppConfig.CreateRuntimeDefaults();
                var roomProvider = roomProviderComponent as IRoomProvider;
                if (roomProvider == null) throw new InvalidOperationException("A room provider is missing.");
                if (avatarRoot == null) throw new InvalidOperationException("Avatar root is missing.");
                if (brain == null || actionExecutor == null || navigation == null)
                {
                    throw new InvalidOperationException("Avatar brain/navigation components are incomplete.");
                }

                if (passthrough != null)
                {
                    await passthrough.SetEnabledAsync(true, destroyCancellationToken);
                }
                var room = await roomProvider.LoadAsync(destroyCancellationToken);
                roomNavMeshBuilder?.Rebuild(room);
                var hmd = userCamera != null ? userCamera.transform : null;
                navigation.Configure(config, hmd);
                avatarInteraction?.Configure(config);
                lookAtUser?.Configure(hmd);
                actionExecutor.Configure(room, config);

                var llm = await CreateLlmAsync(config);
                var memoryStore = new LocalMemoryStore();
                var memoryRetriever = new MemoryRetriever(memoryStore);
                brain.Initialize(
                    llm,
                    ttsComponent as ITts,
                    actionExecutor,
                    room,
                    hmd,
                    avatarRoot,
                    config,
                    memoryRetriever);
                brain.SetMemoryStore(memoryStore);

                debugUI?.Bind(brain);
                IsInitialized = true;
                Debug.Log("[AI] Companion bootstrap complete.", this);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AI] Bootstrap failed: {exception}", this);
            }
        }

        private async System.Threading.Tasks.Task<ILocalLLM> CreateLlmAsync(AppConfig appConfig)
        {
            if (appConfig.UseMockLLM)
            {
                return new MockLocalLLM();
            }

            var native = new LlamaCppLocalLLM(appConfig);
            if (await native.InitializeAsync(destroyCancellationToken))
            {
                return native;
            }

            var error = native.LastError;
            native.Dispose();
            if (appConfig.FallBackToMockWhenModelUnavailable)
            {
                Debug.LogWarning($"[MODEL] Native local LLM unavailable; using mock. {error}", this);
                return new MockLocalLLM();
            }

            throw new InvalidOperationException($"Local model initialization failed: {error}");
        }

        private void ResolveSceneReferences()
        {
            if (userCamera == null) userCamera = Camera.main;
            if (!(roomProviderComponent is IRoomProvider))
            {
                var providers = FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                for (var i = 0; i < providers.Length; i++)
                {
                    if (providers[i] is IRoomProvider)
                    {
                        roomProviderComponent = providers[i];
                        break;
                    }
                }
            }
            if (roomNavMeshBuilder == null) roomNavMeshBuilder = FindAnyObjectByType<RoomNavMeshBuilder>();
            if (passthrough == null) passthrough = FindAnyObjectByType<PicoPassthroughAdapter>();
            if (brain == null) brain = FindAnyObjectByType<GirlBrain>();
            if (avatarRoot == null && brain != null) avatarRoot = brain.transform;
            if (actionExecutor == null && avatarRoot != null) actionExecutor = avatarRoot.GetComponent<ActionExecutor>();
            if (navigation == null && avatarRoot != null) navigation = avatarRoot.GetComponent<GirlNavigation>();
            if (avatarInteraction == null && avatarRoot != null) avatarInteraction = avatarRoot.GetComponent<AvatarInteraction>();
            if (lookAtUser == null && avatarRoot != null) lookAtUser = avatarRoot.GetComponent<LookAtUser>();
            if (ttsComponent == null && avatarRoot != null)
            {
                var components = avatarRoot.GetComponents<MonoBehaviour>();
                for (var i = 0; i < components.Length; i++)
                {
                    if (components[i] is ITts)
                    {
                        ttsComponent = components[i];
                        break;
                    }
                }
            }
            if (debugUI == null) debugUI = FindAnyObjectByType<BrainDebugUI>();
        }
    }
}
