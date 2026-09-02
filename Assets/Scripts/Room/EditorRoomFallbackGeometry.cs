using UnityEngine;

namespace Aigf.Companion.Room
{
    public sealed class EditorRoomFallbackGeometry : MonoBehaviour
    {
        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
#endif
        }
    }
}
