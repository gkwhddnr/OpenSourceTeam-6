using System;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    public sealed class LobbyPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitGameButton;

        public event Action StartClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        private void OnEnable()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(HandleStartClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleSettingsClicked);
            }

            if (quitGameButton != null)
            {
                quitGameButton.onClick.AddListener(HandleQuitClicked);
            }
        }

        private void OnDisable()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(HandleStartClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            }

            if (quitGameButton != null)
            {
                quitGameButton.onClick.RemoveListener(HandleQuitClicked);
            }
        }

        public void SetVisible(bool visible)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        private void HandleStartClicked()
        {
            StartClicked?.Invoke();
        }

        private void HandleSettingsClicked()
        {
            SettingsClicked?.Invoke();
        }

        private void HandleQuitClicked()
        {
            QuitClicked?.Invoke();
        }
    }
}
