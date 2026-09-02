using UnityEngine;

namespace Aigf.Companion.Core
{
    [CreateAssetMenu(menuName = "AIGF/Companion App Config", fileName = "CompanionAppConfig")]
    public sealed class AppConfig : ScriptableObject
    {
        [Header("Local LLM")]
        [SerializeField] private bool useMockLLM;
        [SerializeField] private bool fallBackToMockWhenModelUnavailable = true;
        [SerializeField] private string modelFilename = "qwen3-0.6b-q8_0.gguf";
        [SerializeField, Min(512)] private int contextSize = 2048;
        [SerializeField, Range(16, 512)] private int maxTokens = 128;
        [SerializeField, Range(1, 12)] private int threads = 4;
        [SerializeField, Range(0f, 2f)] private float temperature = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float topP = 0.9f;
        [SerializeField] private bool useGpuOffload;

        [Header("Movement and safety")]
        [SerializeField, Range(0.6f, 1.5f)] private float socialDistance = 0.9f;
        [SerializeField, Range(0.6f, 2f)] private float followDistance = 1.2f;
        [SerializeField, Range(0.05f, 1f)] private float followRefreshThreshold = 0.25f;
        [SerializeField, Range(0.5f, 4f)] private float movementSpeed = 1.4f;
        [SerializeField, Range(0.05f, 1f)] private float navMeshSampleRadius = 0.75f;
        [SerializeField, Range(5f, 120f)] private float movementTimeoutSeconds = 45f;
        [SerializeField, Range(0.1f, 0.5f)] private float wallMargin = 0.25f;
        [SerializeField, Range(0.1f, 0.6f)] private float furnitureMargin = 0.3f;

        [Header("Runtime")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private int maxActionsPerReply = 5;
        [SerializeField, Range(0f, 2f)] private float sitTransitionSeconds = 0.35f;
        [SerializeField, Range(0f, 2f)] private float standTransitionSeconds = 0.35f;

        public bool UseMockLLM => useMockLLM;
        public bool FallBackToMockWhenModelUnavailable => fallBackToMockWhenModelUnavailable;
        public string ModelFilename => modelFilename;
        public int ContextSize => contextSize;
        public int MaxTokens => maxTokens;
        public int Threads => threads;
        public float Temperature => temperature;
        public float TopP => topP;
        public bool UseGpuOffload => useGpuOffload;
        public float SocialDistance => socialDistance;
        public float FollowDistance => followDistance;
        public float FollowRefreshThreshold => followRefreshThreshold;
        public float MovementSpeed => movementSpeed;
        public float NavMeshSampleRadius => navMeshSampleRadius;
        public float MovementTimeoutSeconds => movementTimeoutSeconds;
        public float WallMargin => wallMargin;
        public float FurnitureMargin => furnitureMargin;
        public bool EnableDebugLogs => enableDebugLogs;
        public int MaxActionsPerReply => Mathf.Clamp(maxActionsPerReply, 1, 5);
        public float SitTransitionSeconds => sitTransitionSeconds;
        public float StandTransitionSeconds => standTransitionSeconds;

        public static AppConfig CreateRuntimeDefaults()
        {
            var config = CreateInstance<AppConfig>();
            config.hideFlags = HideFlags.DontSave;
            return config;
        }
    }
}
