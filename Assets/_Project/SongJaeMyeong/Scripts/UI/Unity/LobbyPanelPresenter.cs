using System;
using OpenSourceTeam6.UI.Core;
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
        [SerializeField] private Button bgmQuickButton;
        [SerializeField] private Text bgmQuickButtonLabel;

        private IStoreAudioPlayer _audioPlayer;

        public event Action StartClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        public void BindAudioPlayer(IStoreAudioPlayer audioPlayer)
        {
            _audioPlayer = audioPlayer;
            RefreshBgmQuickLabel();
        }

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

            if (bgmQuickButton != null)
            {
                bgmQuickButton.onClick.AddListener(HandleBgmQuickClicked);
            }

            RefreshBgmQuickLabel();
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

            if (bgmQuickButton != null)
            {
                bgmQuickButton.onClick.RemoveListener(HandleBgmQuickClicked);
            }
        }

        public void SetVisible(bool visible)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }

            if (visible)
            {
                RefreshBgmQuickLabel();
            }
        }

        private void HandleStartClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.StoreDoorChime);
            StartClicked?.Invoke();
        }

        private void HandleSettingsClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            SettingsClicked?.Invoke();
        }

        private void HandleQuitClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            QuitClicked?.Invoke();
        }

        private void HandleBgmQuickClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            if (_audioPlayer != null)
            {
                bool nextMuted = !_audioPlayer.VolumeState.IsBgmMuted;
                _audioPlayer.SetBgmMuted(nextMuted);
                RefreshBgmQuickLabel();
            }
            else
            {
                SettingsClicked?.Invoke();
            }
        }

        private void RefreshBgmQuickLabel()
        {
            if (bgmQuickButtonLabel == null) return;
            bool isMuted = _audioPlayer != null && _audioPlayer.VolumeState.IsBgmMuted;
            bgmQuickButtonLabel.text = isMuted ? "♪✕" : "♪";
        }
    }
}
