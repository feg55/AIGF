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

        private IRoomUpdateSource roomUpdateSource;

        public bool IsInitialized { get; private set; }

        private void OnDestroy()
        {
            if (roomUpdateSource != null)
            {
                roomUpdateSource.RoomUpdated -= HandleRoomUpdated;
                roomUpdateSource = null;
            }
        }

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
                var hmd = userCamera != null ? userCamera.transform : null;
                navigation.Configure(config, hmd);
                navigation.SetNavigationEnabled(false);
                var navMeshBuilt = roomNavMeshBuilder != null && roomNavMeshBuilder.Rebuild(room);
                if (navMeshBuilt)
                {
                    var placement = navigation.EnsurePlacedOnNavMesh();
                    if (!placement.Succeeded)
                    {
                        Debug.LogError($"[NAV] {placement.Message}", this);
                    }
                }
                avatarInteraction?.Configure(config);
                lookAtUser?.Configure(hmd);
                actionExecutor.Configure(room, config);

                var memoryStore = new LocalMemoryStore();
                var memoryRetriever = new MemoryRetriever(memoryStore);
                brain.Initialize(
                    null,
                    ttsComponent as ITts,
                    actionExecutor,
                    room,
                    hmd,
                    avatarRoot,
                    config,
                    memoryRetriever);
                brain.SetMemoryStore(memoryStore);

                debugUI?.Bind(brain);
                roomUpdateSource = roomProvider as IRoomUpdateSource;
                if (roomUpdateSource != null)
                {
                    roomUpdateSource.RoomUpdated += HandleRoomUpdated;
                }
                IsInitialized = true;
                Debug.Log("[AI] Room and direct commands ready; loading conversation model.", this);
                var llm = await CreateLlmAsync(config);
                if (destroyCancellationToken.IsCancellationRequested)
                {
                    (llm as IDisposable)?.Dispose();
                    return;
                }
                brain.SetLanguageModel(llm);
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

        private void HandleRoomUpdated(RoomGraph room)
        {
            if (room == null || !IsInitialized) return;

            // Captured geometry changed. Cancel movement planned against stale
            // furniture, rebuild navigation, then publish the refreshed graph.
            brain?.CancelActiveRequest();
            navigation?.Stop();
            navigation?.SetNavigationEnabled(false);
            var navMeshBuilt = roomNavMeshBuilder != null && roomNavMeshBuilder.Rebuild(room);
            if (navMeshBuilt && navigation != null)
            {
                var placement = navigation.EnsurePlacedOnNavMesh();
                if (!placement.Succeeded)
                {
                    Debug.LogError($"[NAV] {placement.Message}", this);
                }
            }

            actionExecutor?.Configure(room, config);
            brain?.UpdateRoom(room);
            Debug.Log($"[PICO] Applied refreshed room with {room.Count} semantic nodes.", this);
        }

        private async System.Threading.Tasks.Task<ILocalLLM> CreateLlmAsync(AppConfig appConfig)
        {
            if (appConfig.UseMockLLM || (Application.isEditor && appConfig.FallBackToMockWhenModelUnavailable))
            {
                return new MockLocalLLM();
            }

            var native = new LlamaCppLocalLLM(appConfig);
            if (await native.InitializeAsync(destroyCancellationToken))
            {
                return native;
            }

            var error = native.LastError;
            if (Application.isEditor && appConfig.FallBackToMockWhenModelUnavailable)
            {
                native.Dispose();
                Debug.LogWarning($"[MODEL] Native local LLM unavailable; using mock. {error}", this);
                return new MockLocalLLM();
            }

            // Preserve the real error in the bound diagnostics UI. A failed model
            // must never masquerade as a ready chatbot backed by canned commands.
            Debug.LogError($"[MODEL] Local model initialization failed: {error}", this);
            return native;
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
