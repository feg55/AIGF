using Aigf.Companion.Core;
using UnityEngine;

namespace Aigf.Companion.UI
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private AppConfig config;
        [SerializeField] private GameObject panelRoot;

        public AppConfig Config => config;

        public void ToggleVisible()
        {
            if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf);
        }
    }
}
