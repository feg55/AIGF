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
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PXR_Manager.EnableVideoSeeThrough = enabled;
            return Task.FromResult(true);
#else
            return Task.FromResult(false);
#endif
        }
    }
}
