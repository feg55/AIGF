using Aigf.Companion.Voice;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Aigf.Companion.UI
{
    [DisallowMultipleComponent]
    public sealed class PicoMicrophoneHud : MonoBehaviour
    {
        [SerializeField] private SherpaVoiceInput voice;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Vector3 peripheralPosition = new Vector3(-0.28f, -0.24f, 0.8f);
        private InputDevice leftController;
        private bool wasPressed;
        private bool hasButtonSample;
        private float changedAt;
        private CanvasGroup group;
        private MicrophoneIcon icon;
        private Text label;
        private GameObject hud;

        private void Awake()
        {
            if (voice == null) voice = GetComponent<SherpaVoiceInput>();
            if (voice != null) voice.StateChanged += HandleStateChanged;
            changedAt = Time.unscaledTime;
        }

        private void OnDisable()
        {
            hasButtonSample = false;
            if (hud != null) hud.SetActive(false);
        }

        private void OnDestroy()
        {
            if (voice != null) voice.StateChanged -= HandleStateChanged;
            if (hud != null) Destroy(hud);
        }

        private void HandleStateChanged() => changedAt = Time.unscaledTime;

        private void Update()
        {
            if (voice == null) return;
            if (!leftController.isValid)
            {
                leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                hasButtonSample = false;
            }
            var hasSample = leftController.isValid &&
                            leftController.TryGetFeatureValue(CommonUsages.primaryButton, out _);
            if (hasSample)
            {
                leftController.TryGetFeatureValue(CommonUsages.primaryButton, out var pressed);
                // Require a fresh press after reconnecting a controller.
                if (hasButtonSample && pressed && !wasPressed)
                {
                    voice.ToggleMute();
                    if (leftController.TryGetHapticCapabilities(out var haptics) && haptics.supportsImpulse)
                        leftController.SendHapticImpulse(0, 0.25f, 0.045f);
                }
                wasPressed = pressed;
                hasButtonSample = true;
            }
            else hasButtonSample = false;
#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.X)) voice.ToggleMute();
#endif
            if (xrCamera == null) xrCamera = Camera.main;
            if (xrCamera == null) return;
            if (hud == null) CreateHud();
            hud.SetActive(true);
            var unavailable = !voice.IsReady || (!voice.IsListening && !voice.IsRecognizing);
            var tint = voice.IsMuted ? new Color(1f, 0.25f, 0.3f) :
                unavailable ? new Color(1f, 0.72f, 0.25f) : new Color(0.35f, 1f, 0.75f);
            icon.SetState(tint, voice.IsMuted || unavailable);
            label.text = voice.IsMuted ? "MIC OFF · X" : unavailable ? "MIC WAIT · X" : "MIC ON · X";
            label.color = tint;
            var transient = 1f - Mathf.Clamp01((Time.unscaledTime - changedAt - 1.8f) / 0.6f);
            // Muted/error stays visible; live mic becomes a quiet peripheral indicator.
            group.alpha = Mathf.Max(voice.IsMuted || unavailable ? 0.7f : 0.22f, transient);
        }

        private void CreateHud()
        {
            hud = new GameObject("Microphone HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            hud.transform.SetParent(xrCamera.transform, false);
            hud.transform.localPosition = peripheralPosition;
            hud.transform.localRotation = Quaternion.identity;
            hud.transform.localScale = Vector3.one * 0.001f;
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            canvas.sortingOrder = 100;
            group = hud.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var glyph = new GameObject("Microphone", typeof(RectTransform), typeof(CanvasRenderer), typeof(MicrophoneIcon));
            glyph.transform.SetParent(hud.transform, false);
            icon = glyph.GetComponent<MicrophoneIcon>();
            icon.rectTransform.sizeDelta = new Vector2(42f, 52f);
            icon.raycastTarget = false;
            var textObject = new GameObject("State", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(hud.transform, false);
            label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(150f, 25f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -42f);
        }
    }
}
