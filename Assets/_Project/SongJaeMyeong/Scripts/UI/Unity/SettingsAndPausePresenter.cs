using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 기능정의서 UI-04 (설정 화면) 및 UI-05 (일시정지 메뉴) 담당 컴포넌트.
    /// </summary>
    public sealed class SettingsPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Text masterVolumeValueText;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Text bgmVolumeValueText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text sfxVolumeValueText;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Text mouseSensitivityValueText;
        [SerializeField] private Button cycleMicrophoneButton;
        [SerializeField] private Text microphoneDeviceText;
        [SerializeField] private Button closeButton;

        private int selectedMicIndex;
        private IStoreAudioPlayer boundAudioPlayer;

        public bool IsOpen => (panelRoot != null ? panelRoot : gameObject).activeSelf;
        public float MasterVolume { get; private set; } = 1.0f;
        public float BgmVolume { get; private set; } = 0.75f;
        public float SfxVolume { get; private set; } = 1.0f;
        public float MouseSensitivityMultiplier { get; private set; } = 1.0f;
        public string SelectedMicrophoneDevice { get; private set; } = string.Empty;

        public event Action CloseRequested;
        public event Action<float> MasterVolumeChanged;
        public event Action<float> BgmVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<float> MouseSensitivityChanged;
        public event Action<string> MicrophoneDeviceChanged;

        public void BindAudioPlayer(IStoreAudioPlayer audioPlayer)
        {
            boundAudioPlayer = audioPlayer;
            if (boundAudioPlayer != null)
            {
                StoreAudioVolumeState state = boundAudioPlayer.VolumeState;
                MasterVolume = state.MasterVolume;
                BgmVolume = state.BgmVolume;
                SfxVolume = state.SfxVolume;
            }
            else
            {
                MasterVolume = Mathf.Clamp01(AudioListener.volume);
            }

            SyncSlidersWithoutNotify();
            RefreshLabels();
        }

        private void OnEnable()
        {
            if (boundAudioPlayer != null)
            {
                StoreAudioVolumeState state = boundAudioPlayer.VolumeState;
                MasterVolume = state.MasterVolume;
                BgmVolume = state.BgmVolume;
                SfxVolume = state.SfxVolume;
            }
            else
            {
                MasterVolume = Mathf.Clamp01(AudioListener.volume);
            }

            SyncSlidersWithoutNotify();

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
            }

            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivityMultiplier);
                mouseSensitivitySlider.onValueChanged.AddListener(HandleMouseSensitivityChanged);
            }

            if (cycleMicrophoneButton != null)
            {
                cycleMicrophoneButton.onClick.AddListener(HandleCycleMicClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }

            RefreshLabels();
        }

        private void OnDisable()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
            }

            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.RemoveListener(HandleMouseSensitivityChanged);
            }

            if (cycleMicrophoneButton != null)
            {
                cycleMicrophoneButton.onClick.RemoveListener(HandleCycleMicClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        public void SetOpen(bool open)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != open)
            {
                target.SetActive(open);
            }

            if (open)
            {
                SyncSlidersWithoutNotify();
                RefreshLabels();
            }
        }

        private void SyncSlidersWithoutNotify()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(MasterVolume);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(BgmVolume);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(SfxVolume);
            }
        }

        private void HandleMasterVolumeChanged(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            boundAudioPlayer?.SetMasterVolume(MasterVolume);
            RefreshLabels();
            MasterVolumeChanged?.Invoke(MasterVolume);
        }

        private void HandleBgmVolumeChanged(float value)
        {
            BgmVolume = Mathf.Clamp01(value);
            boundAudioPlayer?.SetBgmVolume(BgmVolume);
            RefreshLabels();
            BgmVolumeChanged?.Invoke(BgmVolume);
        }

        private void HandleSfxVolumeChanged(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            if (boundAudioPlayer != null)
            {
                boundAudioPlayer.SetSfxVolume(SfxVolume);
                boundAudioPlayer.PlaySfx(StoreSfxId.UiClick, 0.7f);
            }
            RefreshLabels();
            SfxVolumeChanged?.Invoke(SfxVolume);
        }

        private void HandleMouseSensitivityChanged(float value)
        {
            MouseSensitivityMultiplier = Mathf.Clamp(value, 0.2f, 2.0f);
            RefreshLabels();
            MouseSensitivityChanged?.Invoke(MouseSensitivityMultiplier);
        }

        private void HandleCycleMicClicked()
        {
            boundAudioPlayer?.PlaySfx(StoreSfxId.UiClick);
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                SelectedMicrophoneDevice = string.Empty;
                RefreshLabels();
                return;
            }

            selectedMicIndex = (selectedMicIndex + 1) % devices.Length;
            SelectedMicrophoneDevice = devices[selectedMicIndex];
            RefreshLabels();
            MicrophoneDeviceChanged?.Invoke(SelectedMicrophoneDevice);
        }

        private void RefreshLabels()
        {
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(MasterVolume * 100f)}%";
            }

            if (bgmVolumeValueText != null)
            {
                bgmVolumeValueText.text = $"{Mathf.RoundToInt(BgmVolume * 100f)}%";
            }

            if (sfxVolumeValueText != null)
            {
                sfxVolumeValueText.text = $"{Mathf.RoundToInt(SfxVolume * 100f)}%";
            }

            if (mouseSensitivityValueText != null)
            {
                mouseSensitivityValueText.text = $"{MouseSensitivityMultiplier:F1}x";
            }

            if (microphoneDeviceText != null)
            {
                string[] devices = Microphone.devices;
                if (devices == null || devices.Length == 0)
                {
                    microphoneDeviceText.text = "마이크 장치: 연결된 마이크 없음 ([Tab] 텍스트 지시 사용)";
                }
                else
                {
                    if (selectedMicIndex >= devices.Length) selectedMicIndex = 0;
                    SelectedMicrophoneDevice = devices[selectedMicIndex];
                    microphoneDeviceText.text = $"마이크 ({selectedMicIndex + 1}/{devices.Length}): {SelectedMicrophoneDevice}  [클릭하여 변경]";
                }
            }
        }

        private void HandleCloseClicked()
        {
            boundAudioPlayer?.PlaySfx(StoreSfxId.UiPopupOpenClose);
            CloseRequested?.Invoke();
        }
    }

    public sealed class PausePanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button openSettingsButton;
        [SerializeField] private Button returnLobbyButton;

        public bool IsOpen => (panelRoot != null ? panelRoot : gameObject).activeSelf;

        public event Action ResumeClicked;
        public event Action OpenSettingsClicked;
        public event Action ReturnLobbyClicked;

        private void OnEnable()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResumeClicked);
            if (openSettingsButton != null) openSettingsButton.onClick.AddListener(HandleSettingsClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(HandleReturnLobbyClicked);
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(HandleResumeClicked);
            if (openSettingsButton != null) openSettingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.RemoveListener(HandleReturnLobbyClicked);
        }

        public void SetOpen(bool open)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != open)
            {
                target.SetActive(open);
            }
        }

        private void HandleResumeClicked() => ResumeClicked?.Invoke();
        private void HandleSettingsClicked() => OpenSettingsClicked?.Invoke();
        private void HandleReturnLobbyClicked() => ReturnLobbyClicked?.Invoke();
    }
}
