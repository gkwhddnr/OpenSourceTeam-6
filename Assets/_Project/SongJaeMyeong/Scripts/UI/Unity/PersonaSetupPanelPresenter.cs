using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 0일차 : 알바 채용 이력서 화면 프리젠터.
    /// 프리셋 1번 불러오기, 랜덤 생성, 이름·성격(0/50자) 직접 입력 및 1일차 시작 확정을 담당한다.
    /// </summary>
    public sealed class PersonaSetupPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _preset1Button;
        [SerializeField] private Button _preset2Button;
        [SerializeField] private Button _preset3Button;
        [SerializeField] private Button _randomButton;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private InputField _nameInputField;
        [SerializeField] private InputField _traitInputField;
        [SerializeField] private Text _traitLengthCounterText;
        [SerializeField] private Text _hourlyWageValueText;
        [SerializeField] private Button _confirmStartButton;

        private IStoreAudioPlayer _audioPlayer;
        private EmployeePersonalityProfile _currentProfile = EmployeePersonalityProfile.CreateDefault();
        private int _lastRandomIndex;
        private bool _initializedDefault;

        public bool IsVisible => (_panelRoot != null ? _panelRoot : gameObject).activeSelf;

        public event Action<EmployeePersonaSetupData> ConfirmStartClicked;

        public void BindAudioPlayer(IStoreAudioPlayer audioPlayer)
        {
            _audioPlayer = audioPlayer;
        }

        private void Awake()
        {
            ConfigureInputFields();
        }

        private void OnEnable()
        {
            ConfigureInputFields();

            if (!_initializedDefault)
            {
                ApplyCandidate(EmployeePersonaPresets.CreatePreset1());
                _initializedDefault = true;
            }
            else
            {
                RefreshCounterAndWage();
            }

            if (_preset1Button != null)
            {
                _preset1Button.onClick.AddListener(HandlePreset1Clicked);
            }

            if (_preset2Button != null)
            {
                _preset2Button.onClick.AddListener(HandlePreset2Clicked);
            }

            if (_preset3Button != null)
            {
                _preset3Button.onClick.AddListener(HandlePreset3Clicked);
            }

            if (_randomButton != null)
            {
                _randomButton.onClick.AddListener(HandleRandomClicked);
            }

            if (_nameInputField != null)
            {
                _nameInputField.onValueChanged.AddListener(HandleNameValueChanged);
            }

            if (_traitInputField != null)
            {
                _traitInputField.onValueChanged.AddListener(HandleTraitValueChanged);
            }

            if (_confirmStartButton != null)
            {
                _confirmStartButton.onClick.AddListener(HandleConfirmStartClicked);
            }
        }

        private void OnDisable()
        {
            if (_preset1Button != null)
            {
                _preset1Button.onClick.RemoveListener(HandlePreset1Clicked);
            }

            if (_preset2Button != null)
            {
                _preset2Button.onClick.RemoveListener(HandlePreset2Clicked);
            }

            if (_preset3Button != null)
            {
                _preset3Button.onClick.RemoveListener(HandlePreset3Clicked);
            }

            if (_randomButton != null)
            {
                _randomButton.onClick.RemoveListener(HandleRandomClicked);
            }

            if (_nameInputField != null)
            {
                _nameInputField.onValueChanged.RemoveListener(HandleNameValueChanged);
            }

            if (_traitInputField != null)
            {
                _traitInputField.onValueChanged.RemoveListener(HandleTraitValueChanged);
            }

            if (_confirmStartButton != null)
            {
                _confirmStartButton.onClick.RemoveListener(HandleConfirmStartClicked);
            }
        }

        public void SetVisible(bool isVisible)
        {
            GameObject target = _panelRoot != null ? _panelRoot : gameObject;
            if (target.activeSelf != isVisible)
            {
                target.SetActive(isVisible);
            }

            if (isVisible)
            {
                if (!_initializedDefault)
                {
                    ApplyCandidate(EmployeePersonaPresets.CreatePreset1());
                    _initializedDefault = true;
                }
                RefreshCounterAndWage();
            }
        }

        public EmployeePersonaSetupData BuildCurrentPersonaData()
        {
            string rawName = _nameInputField != null ? _nameInputField.text : EmployeePersonaPresets.Preset1Name;
            string rawTraits = _traitInputField != null ? _traitInputField.text : EmployeePersonaPresets.Preset1Traits;
            return new EmployeePersonaSetupData(
                rawName,
                rawTraits,
                EmployeePersonaPresets.DefaultHourlyWageWon,
                _currentProfile);
        }

        public void ApplyCandidate(EmployeePersonaSetupData candidate)
        {
            _currentProfile = candidate.Profile;

            if (_nameInputField != null)
            {
                _nameInputField.SetTextWithoutNotify(candidate.EmployeeName);
            }

            if (_traitInputField != null)
            {
                string clamped = EmployeePersonaPresets.ClampTraitInput(candidate.PersonalityTraits);
                _traitInputField.SetTextWithoutNotify(clamped);
            }

            RefreshCounterAndWage();
        }

        private void ConfigureInputFields()
        {
            if (_nameInputField != null)
            {
                _nameInputField.characterLimit = EmployeePersonaPresets.MaxNameLength;
            }

            if (_traitInputField != null)
            {
                _traitInputField.characterLimit = EmployeePersonaPresets.MaxTraitLength;
            }
        }

        private void HandlePreset1Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            _lastRandomIndex = 0;
            ApplyCandidate(EmployeePersonaPresets.CreatePreset1());
        }

        private void HandlePreset2Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            _lastRandomIndex = 1;
            ApplyCandidate(EmployeePersonaPresets.CreatePreset2());
        }

        private void HandlePreset3Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            _lastRandomIndex = 2;
            ApplyCandidate(EmployeePersonaPresets.CreatePreset3());
        }

        private void HandleRandomClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            int poolCount = EmployeePersonaPresets.RandomPoolCount;
            if (poolCount > 1)
            {
                int step = UnityEngine.Random.Range(1, poolCount);
                _lastRandomIndex = (_lastRandomIndex + step) % poolCount;
            }

            EmployeePersonaSetupData picked = EmployeePersonaPresets.GetRandomCandidate(_lastRandomIndex);
            ApplyCandidate(picked);
        }

        private void HandleNameValueChanged(string newValue)
        {
            if (_nameInputField == null) return;
            if (newValue != null && newValue.Length > EmployeePersonaPresets.MaxNameLength)
            {
                _nameInputField.SetTextWithoutNotify(newValue.Substring(0, EmployeePersonaPresets.MaxNameLength));
            }
        }

        private void HandleTraitValueChanged(string newValue)
        {
            string clamped = EmployeePersonaPresets.ClampTraitInput(newValue);
            if (_traitInputField != null && clamped != newValue)
            {
                _traitInputField.SetTextWithoutNotify(clamped);
            }

            _currentProfile = EmployeePersonaPresets.InferProfileFromTraits(clamped);
            RefreshCounterAndWage();
        }

        private void RefreshCounterAndWage()
        {
            string currentTraitText = _traitInputField != null ? _traitInputField.text : string.Empty;
            if (_traitLengthCounterText != null)
            {
                _traitLengthCounterText.text = EmployeePersonaPresets.FormatTraitLengthCounter(currentTraitText);
            }

            if (_hourlyWageValueText != null)
            {
                _hourlyWageValueText.text = $"{EmployeePersonaPresets.DefaultHourlyWageWon:N0}원  (일일 기본급 20,000원)";
            }
        }

        private void HandleConfirmStartClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            EmployeePersonaSetupData confirmed = BuildCurrentPersonaData();
            ConfirmStartClicked?.Invoke(confirmed);
        }
    }
}
