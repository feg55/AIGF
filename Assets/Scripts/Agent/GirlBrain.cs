using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.AI;
using Aigf.Companion.Core;
using Aigf.Companion.Memory;
using Aigf.Companion.Room;
using Aigf.Companion.Voice;
using UnityEngine;

namespace Aigf.Companion.Agent
{
    public sealed class GirlBrain : MonoBehaviour
    {
        [SerializeField] private Transform userHead;
        [SerializeField] private Transform avatarRoot;
        [SerializeField] private ActionExecutor actionExecutor;

        private ILocalLLM localLlm;
        private ITts tts;
        private MemoryRetriever memoryRetriever;
        private IMemoryStore memoryStore;
        private readonly Queue<ConversationTurn> recentTurns = new Queue<ConversationTurn>();
        private RoomGraph room;
        private AppConfig config;
        private CancellationTokenSource activeRequest;
        private bool speaking;
        private bool generating;
        private float requestStartedAt;

        public bool CanProcessInput => config != null && room != null && actionExecutor != null;
        public bool IsReady => CanProcessInput && localLlm != null && localLlm.IsReady;
        public string LlmStatus => localLlm == null ? "Loading model; commands available" :
            localLlm is MockLocalLLM ? "Demo commands (no neural conversation)" :
            localLlm.IsReady ? generating ? $"Qwen generating · {Time.realtimeSinceStartup - requestStartedAt:0}s" : "Qwen local · Ready" : "Qwen unavailable; commands available";
        public string LastModelJson { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public long LastInferenceMilliseconds { get; private set; }
        public AgentState CurrentState => speaking ? AgentState.Speaking : actionExecutor != null ? actionExecutor.State : AgentState.Idle;
        public ActionExecutor Executor => actionExecutor;
        public RoomGraph Room => room;

        public event Action DiagnosticsChanged;

        private void OnDestroy()
        {
            CancelActiveRequest();
            activeRequest?.Dispose();
            activeRequest = null;
            if (localLlm is IDisposable disposable) disposable.Dispose();
        }

        public void CancelActiveRequest()
        {
            activeRequest?.Cancel();
            tts?.Stop();
        }

        public void UpdateRoom(RoomGraph roomGraph)
        {
            if (roomGraph == null) return;
            room = roomGraph;
            DiagnosticsChanged?.Invoke();
        }

        public void Initialize(
            ILocalLLM llm,
            ITts textToSpeech,
            ActionExecutor executor,
            RoomGraph roomGraph,
            Transform hmdTransform,
            Transform agentTransform,
            AppConfig appConfig,
            MemoryRetriever retriever = null)
        {
            localLlm = llm;
            tts = textToSpeech;
            actionExecutor = executor;
            room = roomGraph;
            userHead = hmdTransform;
            avatarRoot = agentTransform != null ? agentTransform : transform;
            config = appConfig != null ? appConfig : AppConfig.CreateRuntimeDefaults();
            memoryRetriever = retriever;
            if (llm is LlamaCppLocalLLM native && !native.IsReady) LastError = native.LastError;
            DiagnosticsChanged?.Invoke();
        }

        public void SetMemoryStore(IMemoryStore store)
        {
            memoryStore = store;
            memoryRetriever = store != null ? new MemoryRetriever(store) : null;
        }

        public void SetLanguageModel(ILocalLLM llm)
        {
            localLlm = llm;
            if (llm is LlamaCppLocalLLM native && !native.IsReady) LastError = native.LastError;
            DiagnosticsChanged?.Invoke();
        }

        public Task<ActionResult> ProcessCommandAsync(CompanionCommand command, CancellationToken token = default)
        {
            return ProcessRequestAsync(command.ToString(), DirectCommand.Create(command, room), token);
        }

        public Task<ActionResult> ProcessUserMessageAsync(
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            DirectCommand.TryParse(userMessage, room, out var command);
            return ProcessRequestAsync(userMessage, command, cancellationToken);
        }

        private async Task<ActionResult> ProcessRequestAsync(
            string userMessage, AgentReply command, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return ActionResult.Failure("User message is empty.");
            }

            if (!CanProcessInput || (command == null && !IsReady))
            {
                LastError = localLlm is LlamaCppLocalLLM native ? native.LastError : "Conversation model is loading. Movement commands are available after room loading.";
                Debug.LogWarning($"[AI] Input rejected: {LastError}", this);
                DiagnosticsChanged?.Invoke();
                return ActionResult.Failure(LastError);
            }

            activeRequest?.Cancel();
            activeRequest?.Dispose();
            var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            activeRequest = request;
            var token = request.Token;
            tts?.Stop();
            speaking = false;
            generating = false;

            try
            {
                LastError = string.Empty;
                LastInferenceMilliseconds = 0;
                Debug.Log($"[AI] {(command != null ? "Direct command" : "Conversation")}: {userMessage}", this);
                var reply = command;
                if (reply == null)
                {
                    generating = true;
                    requestStartedAt = Time.realtimeSinceStartup;
                    DiagnosticsChanged?.Invoke();
                    var memories = memoryRetriever != null
                        ? await memoryRetriever.RetrieveAsync(userMessage, 4, token)
                        : (IReadOnlyList<MemoryItem>)Array.Empty<MemoryItem>();
                    var context = BuildContext(memories);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    reply = await localLlm.GenerateAsync(userMessage.Trim(), context, token);
                    token.ThrowIfCancellationRequested();
                    stopwatch.Stop();
                    LastInferenceMilliseconds = stopwatch.ElapsedMilliseconds;
                    generating = false;
                }

                if (!AgentJson.ValidateReply(reply, room, out var validationError, config.MaxActionsPerReply))
                {
                    LastError = validationError;
                    Debug.LogWarning($"[AI] Rejected model reply: {validationError}", this);
                    DiagnosticsChanged?.Invoke();
                    return ActionResult.Failure(validationError);
                }

                LastModelJson = AgentJson.Serialize(reply, true);
                if (config.EnableDebugLogs)
                {
                    Debug.Log($"[{(command != null ? "COMMAND" : "LLM")}] {LastInferenceMilliseconds} ms\n{LastModelJson}", this);
                }

                var avatarAnimator = avatarRoot != null ? avatarRoot.GetComponentInChildren<Avatar.GirlAnimator>() : null;
                avatarAnimator?.SetEmotion(reply.Emotion);

                if (tts != null && tts.IsReady && !string.IsNullOrWhiteSpace(reply.Speech))
                {
                    speaking = true;
                    DiagnosticsChanged?.Invoke();
                    try
                    {
                        await tts.SpeakAsync(reply.Speech, token);
                    }
                    catch (Exception exception) when (!(exception is OperationCanceledException))
                    {
                        Debug.LogWarning($"[VOICE] TTS failed; actions will continue: {exception.Message}", this);
                    }
                    finally
                    {
                        if (ReferenceEquals(activeRequest, request)) speaking = false;
                    }
                }

                // A missing route must never make the companion appear deaf. Speak first,
                // then reject only the physical action if the current room is not navigable.
                if (!actionExecutor.TryPrepare(reply, out var preparationError))
                {
                    LastError = preparationError;
                    Debug.LogWarning($"[AI] Action preparation failed after speech: {preparationError}", this);
                    DiagnosticsChanged?.Invoke();
                    return ActionResult.Failure(preparationError);
                }

                var result = await actionExecutor.ExecuteAsync(reply, token);
                Debug.Log($"[AI] Request result: success={result.Succeeded}; {result.Message}", this);
                if (result.Succeeded)
                {
                    RememberTurn(userMessage.Trim(), reply.Speech);
                    if (memoryStore != null)
                    {
                        await memoryStore.AddAsync(
                            new MemoryItem($"User: {userMessage.Trim()} Companion: {reply.Speech}", 0.35f),
                            token);
                    }
                }
                LastError = result.Succeeded ? string.Empty : result.Message;
                DiagnosticsChanged?.Invoke();
                return result;
            }
            catch (OperationCanceledException)
            {
                return ActionResult.Failure("Request cancelled by a newer command.");
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"[AI] Request failed: {exception}", this);
                DiagnosticsChanged?.Invoke();
                return ActionResult.Failure(exception.Message);
            }
            finally
            {
                if (ReferenceEquals(activeRequest, request))
                {
                    speaking = false;
                    generating = false;
                    DiagnosticsChanged?.Invoke();
                }
                else request.Dispose();
            }
        }

        private AgentContext BuildContext(IReadOnlyList<MemoryItem> memories)
        {
            var userPosition = userHead != null ? userHead.position : Vector3.zero;
            var userForward = userHead != null ? userHead.forward : Vector3.forward;
            userForward.y = 0f;
            if (userForward.sqrMagnitude > 0.0001f) userForward.Normalize();

            return new AgentContext
            {
                UserPosition = userPosition,
                UserForward = userForward,
                AvatarPosition = avatarRoot != null ? avatarRoot.position : transform.position,
                State = CurrentState,
                Room = room,
                Memories = memories,
                RecentTurns = new List<ConversationTurn>(recentTurns)
            };
        }

        private void RememberTurn(string user, string assistant)
        {
            recentTurns.Enqueue(new ConversationTurn(user, assistant));
            while (recentTurns.Count > 6) recentTurns.Dequeue();
        }
    }
}
