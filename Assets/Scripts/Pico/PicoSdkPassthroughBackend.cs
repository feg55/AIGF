using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.XR.PXR;

namespace Aigf.Companion.Pico
{
    public sealed class PicoSdkPassthroughBackend : MonoBehaviour, IPicoPassthroughBackend
    {
        [SerializeField] private Camera xrCamera;

        public bool IsAvailable => Application.platform == RuntimePlatform.Android;

        public Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (xrCamera == null) xrCamera = Camera.main;
            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                xrCamera.allowHDR = false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PXR_Manager.EnableVideoSeeThrough = enabled;
            PXR_MixedReality.EnableVideoSeeThroughEffect(enabled);
            return Task.FromResult(true);
#else
            return Task.FromResult(false);
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!hasFocus) return;
            if (xrCamera == null) xrCamera = Camera.main;
            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                xrCamera.allowHDR = false;
            }
            PXR_Manager.EnableVideoSeeThrough = true;
            PXR_MixedReality.EnableVideoSeeThroughEffect(true);
#endif
        }
    }
}
