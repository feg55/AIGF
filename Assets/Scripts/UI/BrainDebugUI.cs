using System;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
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

        public void SendComeHere() => RunCommand("come here");
        public void SendSit() => RunCommand("sit on the sofa");
        public void SendStand() => RunCommand("stand up");
        public void SendFollow() => RunCommand("follow me");
        public void SendStop() => RunCommand("stop");
        public void SendWave() => RunCommand("wave");

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
            if (brain == null)
            {
                Set(llmStatusText, "LLM: waiting for bootstrap");
                return;
            }

            var executor = brain.Executor;
            Set(llmStatusText, $"LLM: {brain.LlmStatus}");
            Set(modelJsonText, $"Last JSON:\n{brain.LastModelJson}");
            Set(actionText, $"Action: {(executor != null ? executor.CurrentAction : string.Empty)}");
            Set(stateText, $"State: {brain.CurrentState}");
            Set(roomText, $"Room nodes: {(brain.Room != null ? brain.Room.Count : 0)}");
            Set(targetText, $"Target: {(executor != null ? executor.CurrentTarget : string.Empty)}");
            Set(errorText, $"Last error: {brain.LastError}");
            Set(latencyText, $"Inference: {brain.LastInferenceMilliseconds} ms");
        }

        private static void Set(Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
