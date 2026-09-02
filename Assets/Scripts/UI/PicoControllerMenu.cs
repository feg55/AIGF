using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Aigf.Companion.UI
{
    /// <summary>
    /// Keeps the debug menu in the room and drives it with the right PICO controller.
    /// A toggles the menu; the trigger clicks UI controls.
    /// </summary>
    public sealed class PicoControllerMenu : MonoBehaviour
    {
        [SerializeField] private Canvas menuCanvas;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Transform trackingOrigin;
        [SerializeField, Min(0.5f)] private float menuDistance = 1.15f;
        [SerializeField] private float menuVerticalOffset = -0.12f;
        [SerializeField, Min(1f)] private float rayLength = 6f;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private GraphicRaycaster graphicRaycaster;
        private RectTransform menuRect;
        private PointerEventData pointer;
        private InputDevice rightController;
        private LineRenderer pointerLine;
        private Material pointerMaterial;
        private GameObject hoveredObject;
        private GameObject pressedObject;
        private bool primaryWasPressed;
        private bool triggerWasPressed;
        private float nextDeviceSearch;

        public void Configure(Canvas canvas, Camera camera, Transform origin)
        {
            menuCanvas = canvas;
            xrCamera = camera;
            trackingOrigin = origin;
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            CreatePointerLine();
            SetMenuVisible(false);
        }

        private void OnDisable()
        {
            ClearPointerState();
        }

        private void OnDestroy()
        {
            if (pointerMaterial != null) Destroy(pointerMaterial);
        }

        private void Update()
        {
            if (xrCamera == null) xrCamera = Camera.main;
            AcquireControllerIfNeeded();

            var primaryPressed = ReadButton(CommonUsages.primaryButton);
#if UNITY_EDITOR
            primaryPressed |= Input.GetKey(KeyCode.M);
#endif
            if (primaryPressed && !primaryWasPressed) ToggleMenu();
            primaryWasPressed = primaryPressed;

            if (menuCanvas == null || !menuCanvas.gameObject.activeSelf)
            {
                if (pointerLine != null) pointerLine.enabled = false;
                triggerWasPressed = ReadButton(CommonUsages.triggerButton);
                return;
            }

            var triggerPressed = ReadButton(CommonUsages.triggerButton);
            UpdatePointer(triggerPressed);
            triggerWasPressed = triggerPressed;
        }

        private void ToggleMenu()
        {
            if (menuCanvas == null) return;
            var show = !menuCanvas.gameObject.activeSelf;
            if (show) PlaceMenuInFrontOfUser();
            SetMenuVisible(show);
        }

        private void PlaceMenuInFrontOfUser()
        {
            if (xrCamera == null || menuCanvas == null) return;

            var up = Vector3.up;
            var forward = Vector3.ProjectOnPlane(xrCamera.transform.forward, up);
            if (forward.sqrMagnitude < 0.001f) forward = xrCamera.transform.forward;
            forward.Normalize();

            var menuTransform = menuCanvas.transform;
            menuTransform.SetParent(null, true);
            menuTransform.SetPositionAndRotation(
                xrCamera.transform.position + forward * menuDistance + up * menuVerticalOffset,
                Quaternion.LookRotation(forward, up));
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuCanvas != null && menuCanvas.gameObject.activeSelf != visible)
            {
                menuCanvas.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                if (pointerLine != null) pointerLine.enabled = false;
                ClearPointerState();
            }
        }

        private void UpdatePointer(bool triggerPressed)
        {
            if (!TryGetControllerRay(out var controllerRay))
            {
                if (pointerLine != null) pointerLine.enabled = false;
                ClearHover();
                return;
            }

            var rayEnd = controllerRay.GetPoint(rayLength);
            var hitObject = RaycastMenu(controllerRay, out var menuHitPoint, out var raycastResult);
            if (menuHitPoint.HasValue) rayEnd = menuHitPoint.Value;
            DrawPointer(controllerRay.origin, rayEnd, hitObject != null);
            UpdateHover(hitObject, raycastResult);

            if (triggerPressed && !triggerWasPressed) Press(hitObject, raycastResult);
            if (!triggerPressed && triggerWasPressed) Release(hitObject, raycastResult);
        }

        private GameObject RaycastMenu(Ray controllerRay, out Vector3? menuHitPoint, out RaycastResult result)
        {
            menuHitPoint = null;
            result = default;
            if (menuCanvas == null || xrCamera == null || graphicRaycaster == null || menuRect == null)
            {
                return null;
            }

            var plane = new Plane(menuCanvas.transform.forward, menuCanvas.transform.position);
            if (!plane.Raycast(controllerRay, out var distance) || distance < 0f || distance > rayLength)
            {
                return null;
            }

            var worldPoint = controllerRay.GetPoint(distance);
            menuHitPoint = worldPoint;
            var screenPoint = xrCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f) return null;

            EnsurePointer();
            if (pointer == null) return null;
            pointer.position = screenPoint;
            raycastResults.Clear();
            graphicRaycaster.Raycast(pointer, raycastResults);
            if (raycastResults.Count == 0) return null;

            result = raycastResults[0];
            pointer.pointerCurrentRaycast = result;
            return result.gameObject;
        }

        private void UpdateHover(GameObject hitObject, RaycastResult result)
        {
            var nextHover = hitObject != null
                ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hitObject)
                : null;

            if (nextHover == hoveredObject) return;
            EnsurePointer();
            if (pointer == null) return;

            if (hoveredObject != null)
            {
                ExecuteEvents.Execute(hoveredObject, pointer, ExecuteEvents.pointerExitHandler);
            }

            hoveredObject = nextHover;
            pointer.pointerEnter = hoveredObject;
            pointer.pointerCurrentRaycast = result;
            if (hoveredObject != null)
            {
                ExecuteEvents.Execute(hoveredObject, pointer, ExecuteEvents.pointerEnterHandler);
            }
        }

        private void Press(GameObject hitObject, RaycastResult result)
        {
            if (hitObject == null) return;
            EnsurePointer();
            if (pointer == null) return;

            pointer.eligibleForClick = true;
            pointer.pressPosition = pointer.position;
            pointer.pointerPressRaycast = result;
            pressedObject = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hitObject);
            if (pressedObject == null)
            {
                pressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
            }

            pointer.pointerPress = pressedObject;
            if (pressedObject != null)
            {
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerDownHandler);
            }

            var selected = ExecuteEvents.GetEventHandler<ISelectHandler>(hitObject);
            EventSystem.current?.SetSelectedGameObject(selected, pointer);
        }

        private void Release(GameObject hitObject, RaycastResult result)
        {
            EnsurePointer();
            if (pointer == null) return;
            pointer.pointerCurrentRaycast = result;

            if (pressedObject != null)
            {
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
                var clickTarget = hitObject != null
                    ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject)
                    : null;
                if (pointer.eligibleForClick && clickTarget == pressedObject)
                {
                    ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerClickHandler);
                    SendClickHaptic();
                }
            }

            pointer.eligibleForClick = false;
            pointer.pointerPress = null;
            pressedObject = null;
        }

        private bool TryGetControllerRay(out Ray ray)
        {
            ray = default;
            if (!rightController.isValid ||
                !rightController.TryGetFeatureValue(CommonUsages.devicePosition, out var localPosition) ||
                !rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation))
            {
                return false;
            }

            var origin = trackingOrigin != null ? trackingOrigin : transform;
            var worldPosition = origin.TransformPoint(localPosition);
            var worldRotation = origin.rotation * localRotation;
            ray = new Ray(worldPosition, worldRotation * Vector3.forward);
            return true;
        }

        private void AcquireControllerIfNeeded()
        {
            if (rightController.isValid || Time.unscaledTime < nextDeviceSearch) return;
            nextDeviceSearch = Time.unscaledTime + 1f;
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private bool ReadButton(InputFeatureUsage<bool> usage)
        {
            return rightController.isValid &&
                   rightController.TryGetFeatureValue(usage, out var pressed) &&
                   pressed;
        }

        private void SendClickHaptic()
        {
            if (rightController.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                rightController.SendHapticImpulse(0u, 0.35f, 0.05f);
            }
        }

        private void CacheReferences()
        {
            if (menuCanvas == null) return;
            graphicRaycaster = menuCanvas.GetComponent<GraphicRaycaster>();
            menuRect = menuCanvas.GetComponent<RectTransform>();
            if (xrCamera != null) menuCanvas.worldCamera = xrCamera;
        }

        private void EnsurePointer()
        {
            if (EventSystem.current == null) return;
            if (pointer == null)
            {
                pointer = new PointerEventData(EventSystem.current)
                {
                    pointerId = -100,
                    button = PointerEventData.InputButton.Left
                };
            }
        }

        private void CreatePointerLine()
        {
            var pointerObject = new GameObject("Right Controller UI Ray");
            pointerObject.transform.SetParent(transform, false);
            pointerLine = pointerObject.AddComponent<LineRenderer>();
            pointerLine.useWorldSpace = true;
            pointerLine.positionCount = 2;
            pointerLine.startWidth = 0.004f;
            pointerLine.endWidth = 0.002f;
            pointerLine.numCapVertices = 4;
            pointerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pointerLine.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                pointerMaterial = new Material(shader);
                pointerMaterial.color = Color.white;
                pointerLine.material = pointerMaterial;
            }

            pointerLine.enabled = false;
        }

        private void DrawPointer(Vector3 start, Vector3 end, bool isInteractive)
        {
            if (pointerLine == null) return;
            var color = isInteractive ? new Color(0.2f, 1f, 0.75f, 1f) : Color.white;
            pointerLine.startColor = color;
            pointerLine.endColor = color;
            pointerLine.SetPosition(0, start);
            pointerLine.SetPosition(1, end);
            pointerLine.enabled = true;
        }

        private void ClearHover()
        {
            if (hoveredObject == null) return;
            EnsurePointer();
            if (pointer != null)
            {
                ExecuteEvents.Execute(hoveredObject, pointer, ExecuteEvents.pointerExitHandler);
                pointer.pointerEnter = null;
            }
            hoveredObject = null;
        }

        private void ClearPointerState()
        {
            ClearHover();
            if (pressedObject != null && pointer != null)
            {
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
            }
            pressedObject = null;
            if (pointer != null)
            {
                pointer.eligibleForClick = false;
                pointer.pointerPress = null;
            }
        }
    }
}
