using System;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 0일차 : 알바 채용 이력서 화면 프리젠터.
    /// 프리셋 1~3번 불러오기, 랜덤 생성, 이름·성격(1~50자) 직접 입력,
    /// 채용 확정 전 비동기 AI 이력서 심사(Gateway / 로컬 밸런스) 및 1일차 시작 확정을 담당한다.
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
        [SerializeField] private Text _evaluationStatusText;

        private IStoreAudioPlayer _audioPlayer;
        private IPersonaConfirmEvaluator _confirmEvaluator;
        private EmployeePersonalityProfile _currentProfile = EmployeePersonalityProfile.CreateDefault();
        private int _currentHourlyWageWon = EmployeePersonaPresets.DefaultHourlyWageWon;
        private string _currentEvaluationSummary = string.Empty;
        private int _lastRandomIndex;
        private bool _initializedDefault;
        private bool _isEvaluating;
        private CancellationTokenSource _evaluationCts;
        private int _evaluationGeneration;
        private Text _confirmButtonLabelText;
        private string _defaultConfirmButtonLabel = "채용 확정  (1일차 영업 시작)";
        private string _lastOfflineFailedSignature = string.Empty;

        public bool IsVisible => (_panelRoot != null ? _panelRoot : gameObject).activeSelf;
        public bool IsEvaluating => _isEvaluating;

        public event Action<EmployeePersonaSetupData> ConfirmStartClicked;

        public void BindAudioPlayer(IStoreAudioPlayer audioPlayer)
        {
            _audioPlayer = audioPlayer;
        }

        /// <summary>
        /// 외부(VoiceMovementIntegration 브리지 등)에서 채용 확정 전 비동기 이력서 심사기를 등록한다.
        /// </summary>
        public void SetConfirmEvaluator(IPersonaConfirmEvaluator evaluator)
        {
            _confirmEvaluator = evaluator;
        }

        private void Awake()
        {
            ConfigureInputFields();
            EnsureRuntimeStatusTextAndButtonLabel();
        }

        private void OnEnable()
        {
            ConfigureInputFields();
            EnsureRuntimeStatusTextAndButtonLabel();
            CancelPendingEvaluation();

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
            CancelPendingEvaluation();

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
            if (!isVisible)
            {
                CancelPendingEvaluation();
            }

            GameObject target = _panelRoot != null ? _panelRoot : gameObject;
            if (target.activeSelf != isVisible)
            {
                target.SetActive(isVisible);
            }

            if (isVisible)
            {
                EnsureRuntimeStatusTextAndButtonLabel();
                if (!_initializedDefault)
                {
                    ApplyCandidate(EmployeePersonaPresets.CreatePreset1());
                    _initializedDefault = true;
                }
                RefreshCounterAndWage();
            }
        }

        public void CancelPendingEvaluation()
        {
            _evaluationGeneration++;
            _isEvaluating = false;

            if (_evaluationCts != null)
            {
                try
                {
                    _evaluationCts.Cancel();
                    _evaluationCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
                _evaluationCts = null;
            }

            _confirmEvaluator?.CancelEvaluation();
            RestoreConfirmButtonState();
        }

        public EmployeePersonaSetupData BuildCurrentPersonaData()
        {
            string rawName = _nameInputField != null ? _nameInputField.text : EmployeePersonaPresets.Preset1Name;
            string rawTraits = _traitInputField != null ? _traitInputField.text : EmployeePersonaPresets.Preset1Traits;
            return new EmployeePersonaSetupData(
                rawName,
                rawTraits,
                _currentHourlyWageWon,
                _currentProfile,
                _currentEvaluationSummary);
        }

        public void ApplyCandidate(EmployeePersonaSetupData candidate)
        {
            CancelPendingEvaluation();
            _lastOfflineFailedSignature = string.Empty;
            _currentProfile = candidate.Profile;
            _currentHourlyWageWon = candidate.HourlyWageWon > 0
                ? candidate.HourlyWageWon
                : EmployeePersonaPresets.DefaultHourlyWageWon;
            _currentEvaluationSummary = candidate.EvaluationSummary ?? string.Empty;

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
            SetStatusMessage(string.Empty, isError: false);
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

        private void EnsureRuntimeStatusTextAndButtonLabel()
        {
            if (_confirmStartButton != null && _confirmButtonLabelText == null)
            {
                _confirmButtonLabelText = _confirmStartButton.GetComponentInChildren<Text>(true);
                if (_confirmButtonLabelText != null && !string.IsNullOrWhiteSpace(_confirmButtonLabelText.text))
                {
                    _defaultConfirmButtonLabel = _confirmButtonLabelText.text;
                }
            }

            if (_evaluationStatusText != null || _confirmStartButton == null)
            {
                return;
            }

            Transform parent = _confirmStartButton.transform.parent;
            if (parent == null) return;

            Transform existing = parent.Find("EvaluationStatusText");
            if (existing != null)
            {
                _evaluationStatusText = existing.GetComponent<Text>();
                return;
            }

            GameObject statusObj = new GameObject("EvaluationStatusText", typeof(RectTransform), typeof(Text));
            statusObj.transform.SetParent(parent, false);
            RectTransform statusRt = statusObj.GetComponent<RectTransform>();
            RectTransform btnRt = _confirmStartButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                statusRt.anchorMin = new Vector2(0f, 0f);
                statusRt.anchorMax = new Vector2(1f, 0f);
                statusRt.pivot = new Vector2(0.5f, 0f);
                statusRt.anchoredPosition = new Vector2(0f, btnRt.anchoredPosition.y + btnRt.sizeDelta.y + 4f);
                statusRt.sizeDelta = new Vector2(0f, 36f);
            }

            Text txt = statusObj.GetComponent<Text>();
            Font referenceFont = _hourlyWageValueText != null
                ? _hourlyWageValueText.font
                : (_confirmButtonLabelText != null ? _confirmButtonLabelText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            txt.font = referenceFont;
            txt.fontSize = 21;
            txt.fontStyle = FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.color = new Color(0.16f, 0.36f, 0.32f, 1f);
            txt.text = string.Empty;
            _evaluationStatusText = txt;
        }

        private void RestoreConfirmButtonState()
        {
            if (_confirmStartButton != null)
            {
                _confirmStartButton.interactable = true;
            }

            if (_confirmButtonLabelText != null)
            {
                _confirmButtonLabelText.text = _defaultConfirmButtonLabel;
            }
        }

        private void SetStatusMessage(string message, bool isError)
        {
            EnsureRuntimeStatusTextAndButtonLabel();
            if (_evaluationStatusText == null) return;

            _evaluationStatusText.text = message ?? string.Empty;
            _evaluationStatusText.color = isError
                ? new Color(0.68f, 0.16f, 0.14f, 1f)
                : new Color(0.14f, 0.38f, 0.32f, 1f);
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
            CancelPendingEvaluation();
            _lastOfflineFailedSignature = string.Empty;
            SetStatusMessage(string.Empty, isError: false);

            if (_nameInputField == null) return;
            if (newValue != null && newValue.Length > EmployeePersonaPresets.MaxNameLength)
            {
                _nameInputField.SetTextWithoutNotify(newValue.Substring(0, EmployeePersonaPresets.MaxNameLength));
            }
        }

        private void HandleTraitValueChanged(string newValue)
        {
            CancelPendingEvaluation();
            _lastOfflineFailedSignature = string.Empty;

            string clamped = EmployeePersonaPresets.ClampTraitInput(newValue);
            if (_traitInputField != null && clamped != newValue)
            {
                _traitInputField.SetTextWithoutNotify(clamped);
            }

            _currentProfile = EmployeePersonaPresets.InferProfileFromTraits(clamped);
            string rawName = _nameInputField != null && !string.IsNullOrWhiteSpace(_nameInputField.text)
                ? _nameInputField.text
                : EmployeePersonaPresets.Preset1Name;
            EmployeePersonaSetupData previewBaseline = new EmployeePersonaSetupData(
                rawName,
                clamped,
                EmployeePersonaPresets.DefaultHourlyWageWon,
                _currentProfile);
            PersonaEvaluationOutcome preview = EmployeePersonaPresets.EvaluateLocalPersonaBalance(
                rawName,
                clamped,
                previewBaseline);
            if (preview.Succeeded)
            {
                _currentHourlyWageWon = preview.PreparedPersona.HourlyWageWon;
                _currentProfile = preview.PreparedPersona.Profile;
                _currentEvaluationSummary = preview.SummaryReason;
            }
            else
            {
                _currentHourlyWageWon = EmployeePersonaPresets.DefaultHourlyWageWon;
                _currentEvaluationSummary = string.Empty;
            }

            RefreshCounterAndWage();
            SetStatusMessage(string.Empty, isError: false);
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
                int wage = _currentHourlyWageWon > 0 ? _currentHourlyWageWon : EmployeePersonaPresets.DefaultHourlyWageWon;
                int dailyCost = EmployeePersonaPresets.ComputeDailyOperatingCostWon(wage);
                _hourlyWageValueText.text = $"{wage:N0}원  (일일 인건비 {dailyCost:N0}원)";
            }
        }

        private void HandleConfirmStartClicked()
        {
            if (_isEvaluating || (_confirmEvaluator != null && _confirmEvaluator.IsEvaluationPending))
            {
                return;
            }

            string rawName = _nameInputField != null ? _nameInputField.text : string.Empty;
            string rawTraits = _traitInputField != null ? _traitInputField.text : string.Empty;
            if (!EmployeePersonaPresets.TryValidateRawInputs(
                    rawName,
                    rawTraits,
                    out string trimmedName,
                    out string trimmedTraits,
                    out string validationError))
            {
                _audioPlayer?.PlaySfx(StoreSfxId.ReactionComplain);
                SetStatusMessage(validationError, isError: true);
                return;
            }

            _audioPlayer?.PlaySfx(StoreSfxId.PencilWrite);
            _ = EvaluateAndConfirmAsync(trimmedName, trimmedTraits);
        }

        private async Task EvaluateAndConfirmAsync(string expectedName, string expectedTraits)
        {
            CancelPendingEvaluation();
            int generation = ++_evaluationGeneration;
            _isEvaluating = true;
            CancellationTokenSource cts = new CancellationTokenSource();
            _evaluationCts = cts;

            if (_confirmStartButton != null)
            {
                _confirmStartButton.interactable = false;
            }

            if (_confirmButtonLabelText != null)
            {
                _confirmButtonLabelText.text = "AI 이력서 심사 중...";
            }

            SetStatusMessage("AI 면접관이 성격을 분석하여 시급·능력치·스트레스 배율을 심사 중입니다...", isError: false);

            EmployeePersonalityProfile baselineProfile = EmployeePersonaPresets.InferProfileFromTraits(expectedTraits);
            EmployeePersonaSetupData baseline = new EmployeePersonaSetupData(
                expectedName,
                expectedTraits,
                EmployeePersonaPresets.DefaultHourlyWageWon,
                baselineProfile);

            PersonaEvaluationOutcome outcome;
            try
            {
                if (_confirmEvaluator != null)
                {
                    outcome = await _confirmEvaluator.EvaluateAndPrepareAsync(
                        expectedName,
                        expectedTraits,
                        baseline,
                        cts.Token);
                }
                else
                {
                    outcome = EmployeePersonaPresets.EvaluateLocalPersonaBalance(
                        expectedName,
                        expectedTraits,
                        baseline);
                }
            }
            catch (OperationCanceledException)
            {
                if (generation == _evaluationGeneration)
                {
                    _isEvaluating = false;
                    RestoreConfirmButtonState();
                }
                return;
            }
            catch (Exception ex)
            {
                outcome = PersonaEvaluationOutcome.Failure(ex.Message, isServerUnreachable: false, fallbackBaseline: baseline);
            }

            if (this == null || !isActiveAndEnabled || !IsVisible || generation != _evaluationGeneration || cts.IsCancellationRequested)
            {
                if (generation == _evaluationGeneration)
                {
                    _isEvaluating = false;
                    RestoreConfirmButtonState();
                }
                return;
            }

            string currentName = _nameInputField != null && _nameInputField.text != null ? _nameInputField.text.Trim() : string.Empty;
            string currentTraits = _traitInputField != null && _traitInputField.text != null
                ? _traitInputField.text.Replace("\r", string.Empty).Replace("\n", " ").Trim()
                : string.Empty;
            if (!string.Equals(currentName, expectedName, StringComparison.Ordinal) ||
                !string.Equals(currentTraits, expectedTraits, StringComparison.Ordinal))
            {
                _isEvaluating = false;
                _confirmEvaluator?.CancelEvaluation();
                RestoreConfirmButtonState();
                SetStatusMessage("이력서가 변경되어 다시 심사해야 합니다.", isError: true);
                return;
            }

            if (!outcome.Succeeded)
            {
                string signature = $"{expectedName}|{expectedTraits}";
                if (outcome.IsServerUnreachable &&
                    string.Equals(_lastOfflineFailedSignature, signature, StringComparison.Ordinal))
                {
                    // 로컬 중계서버가 꺼져 있는 상태에서 동일 입력으로 재시도한 경우 자체 밸런스 심사 결과로 진행 허용
                    outcome = EmployeePersonaPresets.EvaluateLocalPersonaBalance(expectedName, expectedTraits, baseline);
                }
                else
                {
                    _isEvaluating = false;
                    if (outcome.IsServerUnreachable)
                    {
                        _lastOfflineFailedSignature = signature;
                        SetStatusMessage($"{outcome.ErrorMessage} (한 번 더 누르면 오프라인 자체 심사로 시작)", isError: true);
                    }
                    else
                    {
                        _lastOfflineFailedSignature = string.Empty;
                        SetStatusMessage(outcome.ErrorMessage, isError: true);
                    }

                    RestoreConfirmButtonState();
                    if (_confirmButtonLabelText != null)
                    {
                        _confirmButtonLabelText.text = "채용 확정 재시도  (1일차 영업 시작)";
                    }
                    return;
                }
            }

            _lastOfflineFailedSignature = string.Empty;
            _isEvaluating = false;
            RestoreConfirmButtonState();

            EmployeePersonaSetupData confirmed = outcome.PreparedPersona;
            _currentProfile = confirmed.Profile;
            _currentHourlyWageWon = confirmed.HourlyWageWon;
            _currentEvaluationSummary = outcome.SummaryReason;
            RefreshCounterAndWage();

            if (!string.IsNullOrWhiteSpace(outcome.SummaryReason))
            {
                SetStatusMessage($"[심사 완료] {outcome.SummaryReason}", isError: false);
            }

            ConfirmStartClicked?.Invoke(confirmed);
        }
    }
}
