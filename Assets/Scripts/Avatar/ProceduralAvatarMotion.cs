using UnityEngine;

namespace Aigf.Companion.Avatar
{
    public sealed class ProceduralAvatarMotion : MonoBehaviour
    {
        // Compatibility shim for old prefabs. Direct bone rotation was intentionally removed:
        // authored Humanoid clips are now the only supported source of body motion.
        public void SetWalking(bool value) { }
        public void SetSeated(bool value) { }
        public void SetEmotion(float value) { }
        public void Wave() { }
    }
}
