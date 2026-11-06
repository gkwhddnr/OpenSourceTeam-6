using System;
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
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Text mouseSensitivityValueText;
        [SerializeField] private Button cycleMicrophoneButton;
        [SerializeField] private Text microphoneDeviceText;
        [SerializeField] private Button closeButton;

        private int selectedMicIndex;

        public bool IsOpen => (panelRoot != null ? panelRoot : gameObject).activeSelf;
        public float MouseSensitivityMultiplier { get; private set; } = 1.0f;
        public string SelectedMicrophoneDevice { get; private set; } = string.Empty;

        public event Action CloseRequested;
        public event Action<float> MouseSensitivityChanged;
        public event Action<string> MicrophoneDeviceChanged;

        private void OnEnable()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
                masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
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
                RefreshLabels();
            }
        }

        private void HandleMasterVolumeChanged(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            RefreshLabels();
        }

        private void HandleMouseSensitivityChanged(float value)
        {
            MouseSensitivityMultiplier = Mathf.Clamp(value, 0.2f, 2.0f);
            RefreshLabels();
            MouseSensitivityChanged?.Invoke(MouseSensitivityMultiplier);
        }

        private void HandleCycleMicClicked()
        {
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
                masterVolumeValueText.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
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
