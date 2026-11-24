using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Time = UnityEngine.Time;
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
        private string _defaultConfirmButtonLabel = "AI 일급 심사하기  →";
        private string _lastOfflineFailedSignature = string.Empty;
        private bool _appliedCrispTypography;
        private GameObject _pledgeTextObject;
        private Text _wageSectionLabelText;
        private Image _wageBoxImage;
        private Color _defaultWageBoxColor = new Color(0.96f, 0.93f, 0.87f, 0.72f);
        private Outline _wageBoxOutline;
        private Coroutine _wageHighlightCoroutine;
        private bool _hasVerifiedEvaluation;
        private string _verifiedEvaluationSignature = string.Empty;
        private EmployeePersonaSetupData _verifiedPersonaData;

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
            ApplyCrispPaperTypography();
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
                ApplyCrispPaperTypography();
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
            ClearVerifiedEvaluationState();
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

        /// <summary>
        /// 손글씨 폰트(KyoboHandwriting2025lyb)에 Unity 합성 Bold가 걸리면 작은 크기에서 획이 뭉개지고 깨지므로
        /// 모든 종이 텍스트를 Normal 스타일로 보정하고, 입력창 외 라벨 텍스트는 2x 슈퍼샘플링으로 선명하게 렌더링한다.
        /// </summary>
        private void ApplyCrispPaperTypography()
        {
            if (_appliedCrispTypography)
            {
                return;
            }

            GameObject root = _panelRoot != null ? _panelRoot : gameObject;
            if (root == null || !root.activeInHierarchy)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                Text txt = allTexts[i];
                if (txt == null) continue;

                // 단일 웨이트 손글씨 폰트의 합성 Bold 뭉개짐 제거
                if (txt.fontStyle == FontStyle.Bold)
                {
                    txt.fontStyle = FontStyle.Normal;
                }

                // InputField 내부 텍스트·플레이스홀더는 캐럿 정렬 유지를 위해 1x 스케일 유지
                bool isInputChild = txt.GetComponentInParent<InputField>() != null;
                if (isInputChild)
                {
                    continue;
                }

                RectTransform rt = txt.rectTransform;
                if (rt == null || rt.localScale.x < 0.75f)
                {
                    continue;
                }

                Rect currentRect = rt.rect;
                if (currentRect.width < 4f || currentRect.height < 4f)
                {
                    continue;
                }

                // 현재 화면상 위치를 그대로 유지하면서 2x 해상도 아틀라스로 렌더링
                Vector3 worldCenter = rt.TransformPoint(currentRect.center);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(currentRect.width * 2f, currentRect.height * 2f);
                rt.localScale = new Vector3(0.5f, 0.5f, 1f);
                rt.position = worldCenter;
                txt.fontSize = Mathf.Max(24, txt.fontSize * 2);
            }

            _appliedCrispTypography = true;
        }

        private void EnsureRuntimeStatusTextAndButtonLabel()
        {
            if (_confirmStartButton != null && _confirmButtonLabelText == null)
            {
                _confirmButtonLabelText = _confirmStartButton.GetComponentInChildren<Text>(true);
                if (_confirmButtonLabelText != null)
                {
                    _confirmButtonLabelText.text = _defaultConfirmButtonLabel;
                }
            }

            Transform parent = _confirmStartButton != null ? _confirmStartButton.transform.parent : null;
            if (parent != null)
            {
                if (_wageSectionLabelText == null)
                {
                    Transform wageLabelTr = parent.Find("WageLabel");
                    if (wageLabelTr != null)
                    {
                        _wageSectionLabelText = wageLabelTr.GetComponent<Text>();
                        if (_wageSectionLabelText != null)
                        {
                            _wageSectionLabelText.text = "일급 (1일 인건비)";
                        }
                    }
                }

                if (_wageBoxImage == null && _hourlyWageValueText != null && _hourlyWageValueText.transform.parent != null)
                {
                    _wageBoxImage = _hourlyWageValueText.transform.parent.GetComponent<Image>();
                    if (_wageBoxImage != null)
                    {
                        _defaultWageBoxColor = _wageBoxImage.color;
                        _wageBoxOutline = _wageBoxImage.GetComponent<Outline>();
                        if (_wageBoxOutline == null)
                        {
                            _wageBoxOutline = _wageBoxImage.gameObject.AddComponent<Outline>();
                        }
                        _wageBoxOutline.effectColor = new Color(0.86f, 0.62f, 0.14f, 0f);
                        _wageBoxOutline.effectDistance = new Vector2(3f, -3f);
                    }
                }
            }

            if (_evaluationStatusText != null || _confirmStartButton == null)
            {
                return;
            }

            if (parent == null) return;

            if (_pledgeTextObject == null)
            {
                Transform pledgeTr = parent.Find("ResumePledgeText");
                if (pledgeTr != null)
                {
                    _pledgeTextObject = pledgeTr.gameObject;
                }
            }

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
                statusRt.anchoredPosition = new Vector2(0f, btnRt.anchoredPosition.y + btnRt.sizeDelta.y + 8f);
                statusRt.sizeDelta = new Vector2(0f, 32f);
            }

            Text txt = statusObj.GetComponent<Text>();
            Font referenceFont = _hourlyWageValueText != null
                ? _hourlyWageValueText.font
                : (_confirmButtonLabelText != null ? _confirmButtonLabelText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            txt.font = referenceFont;
            txt.fontSize = 16;
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
            bool hasMessage = !string.IsNullOrWhiteSpace(message);

            if (_pledgeTextObject != null && _pledgeTextObject.activeSelf == hasMessage)
            {
                _pledgeTextObject.SetActive(!hasMessage);
            }

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
            ClearVerifiedEvaluationState();
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
            ClearVerifiedEvaluationState();
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
                _hourlyWageValueText.text = _hasVerifiedEvaluation
                    ? $"확정 일급  {dailyCost:N0}원"
                    : $"예상 일급  {dailyCost:N0}원  (심사 전)";
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

            // 2단계: 이미 AI 일급 심사가 끝난 상태라면 확정된 일급으로 1일차 바로 시작
            string signature = $"{trimmedName}|{trimmedTraits}";
            if (_hasVerifiedEvaluation && string.Equals(_verifiedEvaluationSignature, signature, StringComparison.Ordinal))
            {
                _audioPlayer?.PlaySfx(StoreSfxId.PencilWrite);
                ConfirmStartClicked?.Invoke(_verifiedPersonaData);
                return;
            }

            // 1단계: 아직 미심사 상태라면 AI 일급 심사 요청 후 결과를 강조 표시
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
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
                _confirmButtonLabelText.text = "AI 일급 심사 중...";
            }

            SetStatusMessage("AI 면접관이 성격을 분석하여 일급과 성향 배율을 심사 중입니다...", isError: false);

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

            EmployeePersonaSetupData rawConfirmed = outcome.PreparedPersona;
            int confirmedDailyWage = EmployeePersonaPresets.ComputeDailyOperatingCostWon(rawConfirmed.HourlyWageWon);
            string formattedSummary = FormatSummaryForDailyWage(outcome.SummaryReason, confirmedDailyWage);
            EmployeePersonaSetupData confirmed = new EmployeePersonaSetupData(
                rawConfirmed.EmployeeName,
                rawConfirmed.PersonalityTraits,
                rawConfirmed.HourlyWageWon,
                rawConfirmed.Profile,
                formattedSummary);

            _currentProfile = confirmed.Profile;
            _currentHourlyWageWon = confirmed.HourlyWageWon;
            _currentEvaluationSummary = formattedSummary;
            _verifiedPersonaData = confirmed;
            _verifiedEvaluationSignature = $"{expectedName}|{expectedTraits}";
            _hasVerifiedEvaluation = true;

            RefreshCounterAndWage();
            SetStatusMessage($"[심사 완료] {formattedSummary}", isError: false);

            if (_confirmButtonLabelText != null)
            {
                _confirmButtonLabelText.text = $"일급 {confirmedDailyWage:N0}원으로 채용 확정 · 시작 →";
            }

            _audioPlayer?.PlaySfx(StoreSfxId.CheckoutCoin, 0.85f);
            PlayWageHighlightEffect();
        }

        private void ClearVerifiedEvaluationState()
        {
            _hasVerifiedEvaluation = false;
            _verifiedEvaluationSignature = string.Empty;
            if (_wageHighlightCoroutine != null)
            {
                StopCoroutine(_wageHighlightCoroutine);
                _wageHighlightCoroutine = null;
            }

            if (_wageBoxImage != null)
            {
                _wageBoxImage.color = _defaultWageBoxColor;
                _wageBoxImage.rectTransform.localScale = Vector3.one;
            }

            if (_wageBoxOutline != null)
            {
                Color c = _wageBoxOutline.effectColor;
                c.a = 0f;
                _wageBoxOutline.effectColor = c;
            }

            if (_confirmButtonLabelText != null && !_isEvaluating)
            {
                _confirmButtonLabelText.text = _defaultConfirmButtonLabel;
            }
        }

        private void PlayWageHighlightEffect()
        {
            if (_wageHighlightCoroutine != null)
            {
                StopCoroutine(_wageHighlightCoroutine);
            }

            if (isActiveAndEnabled && _wageBoxImage != null)
            {
                _wageHighlightCoroutine = StartCoroutine(CoPlayWageHighlightEffect());
            }
        }

        private IEnumerator CoPlayWageHighlightEffect()
        {
            RectTransform boxRt = _wageBoxImage.rectTransform;
            Color flashColor = new Color(1.0f, 0.90f, 0.46f, 0.96f);
            Color settledHighlightColor = new Color(1.0f, 0.95f, 0.72f, 0.92f);
            Color borderColor = new Color(0.84f, 0.52f, 0.10f, 1f);

            // 1. 스케일 펀치 + 황금빛 플래시 (0.45초)
            const float punchDuration = 0.45f;
            float elapsed = 0f;
            while (elapsed < punchDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / punchDuration);
                float bump = Mathf.Sin(t * Mathf.PI);
                boxRt.localScale = Vector3.one * (1f + 0.10f * bump);
                _wageBoxImage.color = Color.Lerp(flashColor, settledHighlightColor, t);
                if (_wageBoxOutline != null)
                {
                    borderColor.a = Mathf.Lerp(1f, 0.85f, t);
                    _wageBoxOutline.effectColor = borderColor;
                }
                yield return null;
            }

            boxRt.localScale = Vector3.one;
            _wageBoxImage.color = settledHighlightColor;
            if (_wageBoxOutline != null)
            {
                borderColor.a = 0.85f;
                _wageBoxOutline.effectColor = borderColor;
            }

            // 2. 확정 상태가 유지되는 동안 은은한 황금 테두리 맥동
            while (_hasVerifiedEvaluation && _wageBoxImage != null)
            {
                if (_wageBoxOutline != null)
                {
                    float pulse = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.8f);
                    borderColor.a = pulse;
                    _wageBoxOutline.effectColor = borderColor;
                }
                yield return null;
            }

            _wageHighlightCoroutine = null;
        }

        private static string FormatSummaryForDailyWage(string rawSummary, int dailyWageWon)
        {
            if (string.IsNullOrWhiteSpace(rawSummary))
            {
                return $"확정 일급은 {dailyWageWon:N0}원입니다.";
            }

            string replaced = Regex.Replace(
                rawSummary,
                @"시급은\s*[0-9,]+원입니다\.?",
                $"확정 일급은 {dailyWageWon:N0}원입니다.");
            if (replaced == rawSummary)
            {
                replaced = $"{rawSummary.Trim()} (확정 일급 {dailyWageWon:N0}원)";
            }

            return replaced;
        }
    }
}
