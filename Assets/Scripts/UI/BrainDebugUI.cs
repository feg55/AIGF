using System;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Room;
using Aigf.Companion.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace Aigf.Companion.UI
{
    public sealed class BrainDebugUI : MonoBehaviour
    {
        [SerializeField] private InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text llmStatusText;
        [SerializeField] private Text modelJsonText;
        [SerializeField] private Text actionText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text roomText;
        [SerializeField] private Text targetText;
        [SerializeField] private Text errorText;
        [SerializeField] private Text latencyText;

        private GirlBrain brain;
        private float nextRefresh;
        private SherpaVoiceInput voice;

        private void Awake()
        {
            if (sendButton != null) sendButton.onClick.AddListener(SendCurrentInput);
        }

        private void OnDestroy()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(SendCurrentInput);
            if (brain != null) brain.DiagnosticsChanged -= Refresh;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
        }

        public void Bind(GirlBrain girlBrain)
        {
            if (brain != null) brain.DiagnosticsChanged -= Refresh;
            brain = girlBrain;
            if (brain != null) brain.DiagnosticsChanged += Refresh;
            Refresh();
        }

        public void SendCurrentInput()
        {
            var command = inputField != null ? inputField.text : string.Empty;
            if (inputField != null) inputField.text = string.Empty;
            RunCommand(command);
        }

        public void SendComeHere() => RunDirectCommand(CompanionCommand.ComeHere);
        public void SendSit() => RunDirectCommand(CompanionCommand.Sit);
        public void SendStand() => RunDirectCommand(CompanionCommand.Stand);
        public void SendFollow() => RunDirectCommand(CompanionCommand.Follow);
        public void SendStop() => RunDirectCommand(CompanionCommand.Stop);
        public void SendWave() => RunDirectCommand(CompanionCommand.Wave);

        private async void RunDirectCommand(CompanionCommand command)
        {
            try
            {
                if (brain == null) brain = FindAnyObjectByType<GirlBrain>();
                if (brain != null) await brain.ProcessCommandAsync(command, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogError($"[AI] Menu command failed: {exception}", this); }
            finally { Refresh(); }
        }

        public void Configure(
            InputField input,
            Button send,
            Text llmStatus,
            Text modelJson,
            Text action,
            Text state,
            Text room,
            Text target,
            Text error,
            Text latency)
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(SendCurrentInput);
            inputField = input;
            sendButton = send;
            llmStatusText = llmStatus;
            modelJsonText = modelJson;
            actionText = action;
            stateText = state;
            roomText = room;
            targetText = target;
            errorText = error;
            latencyText = latency;
            if (sendButton != null) sendButton.onClick.AddListener(SendCurrentInput);
        }

        private async void RunCommand(string command)
        {
            try
            {
                if (brain != null) await brain.ProcessUserMessageAsync(command);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AI] Debug command failed: {exception}", this);
            }
            finally
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (voice == null) voice = FindAnyObjectByType<SherpaVoiceInput>();
            if (brain == null)
            {
                Set(llmStatusText, "LLM: waiting for bootstrap");
                return;
            }

            var executor = brain.Executor;
            Set(llmStatusText, $"LLM: {brain.LlmStatus}");
            var voiceStatus = voice != null ? $"Mic (X): {voice.Status}\nLevel: {voice.InputLevel:0.000} | Heard: {voice.LastTranscript}\n\n" : "Mic: missing\n";
            Set(modelJsonText, $"{voiceStatus}Last JSON:\n{brain.LastModelJson}");
            Set(actionText, $"Action: {(executor != null ? executor.CurrentAction : string.Empty)}");
            Set(stateText, $"State: {brain.CurrentState}");
            Set(roomText, BuildRoomStatus(brain.Room));
            Set(targetText, $"Target: {(executor != null ? executor.CurrentTarget : string.Empty)}");
            Set(errorText, $"Last error: {brain.LastError}");
            Set(latencyText, $"Inference: {brain.LastInferenceMilliseconds} ms");
        }

        private static void Set(Text target, string value)
        {
            if (target != null) target.text = value;
        }

        private static string BuildRoomStatus(RoomGraph room)
        {
            if (room == null) return "Room: unavailable";
            var sofas = 0;
            var seats = 0;
            var furniture = 0;
            for (var i = 0; i < room.Nodes.Count; i++)
            {
                var node = room.Nodes[i];
                if (node == null) continue;
                if (node.Type == RoomNodeType.Sofa) sofas++;
                if (node.CanSit) seats++;
                if (node.Type == RoomNodeType.Sofa ||
                    node.Type == RoomNodeType.Chair ||
                    node.Type == RoomNodeType.Table ||
                    node.Type == RoomNodeType.Bed ||
                    node.Type == RoomNodeType.Cabinet ||
                    node.Type == RoomNodeType.OtherFurniture)
                {
                    furniture++;
                }
            }

            return $"Room: {room.Count} | furniture: {furniture} | sofas: {sofas} | seats: {seats}";
        }
    }
}
