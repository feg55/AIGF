using System;
using Aigf.Companion.AI;
using Aigf.Companion.Agent;
using Aigf.Companion.Avatar;
using Aigf.Companion.Memory;
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
        [SerializeField] private ManualRoomProvider roomProvider;
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
                if (roomProvider == null) throw new InvalidOperationException("ManualRoomProvider is missing.");
                if (avatarRoot == null) throw new InvalidOperationException("Avatar root is missing.");
                if (brain == null || actionExecutor == null || navigation == null)
                {
                    throw new InvalidOperationException("Avatar brain/navigation components are incomplete.");
                }

                var room = await roomProvider.LoadAsync(destroyCancellationToken);
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
            if (roomProvider == null) roomProvider = FindFirstObjectByType<ManualRoomProvider>();
            if (brain == null) brain = FindFirstObjectByType<GirlBrain>();
            if (avatarRoot == null && brain != null) avatarRoot = brain.transform;
            if (actionExecutor == null && avatarRoot != null) actionExecutor = avatarRoot.GetComponent<ActionExecutor>();
            if (navigation == null && avatarRoot != null) navigation = avatarRoot.GetComponent<GirlNavigation>();
            if (avatarInteraction == null && avatarRoot != null) avatarInteraction = avatarRoot.GetComponent<AvatarInteraction>();
            if (lookAtUser == null && avatarRoot != null) lookAtUser = avatarRoot.GetComponent<LookAtUser>();
            if (ttsComponent == null && avatarRoot != null) ttsComponent = avatarRoot.GetComponent<MockTts>();
            if (debugUI == null) debugUI = FindFirstObjectByType<BrainDebugUI>();
        }
    }
}
