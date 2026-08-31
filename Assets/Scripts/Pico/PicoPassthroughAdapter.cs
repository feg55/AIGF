using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Pico
{
    public interface IPicoPassthroughBackend
    {
        bool IsAvailable { get; }
        Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
    }

    public sealed class PicoPassthroughAdapter : MonoBehaviour
    {
        [Tooltip("Assign a PICO-SDK-specific component implementing IPicoPassthroughBackend after installing the SDK.")]
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private Camera xrCamera;

        public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            var backend = backendComponent as IPicoPassthroughBackend;
            if (backend != null && backend.IsAvailable)
            {
                return await backend.SetEnabledAsync(enabled, cancellationToken);
            }

            if (xrCamera != null && enabled)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            Debug.LogWarning("[PICO] Passthrough backend is not installed. Editor scene remains usable.", this);
            return false;
        }
    }
}
