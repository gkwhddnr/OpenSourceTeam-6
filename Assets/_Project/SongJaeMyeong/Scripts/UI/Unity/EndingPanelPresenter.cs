using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 하루 마감 또는 7일차 종료 후 출력되는 엔딩 화면(EndingPanel) 프리젠터.
    /// 추후 2D 애니메이션/3D 녹화 영상이 들어갈 컷신 슬롯과
    /// 엔딩 4종(노말엔딩 1·2 / 실패엔딩 1·2) × 알바 성격·스트레스 기반 텍스트를 표시한다.
    /// </summary>
    public sealed class EndingPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Image _headerBanner;
        [SerializeField] private Text _headerTitleText;
        [SerializeField] private Text _conditionSummaryText;
        [SerializeField] private Text _cutscenePlaceholderText;
        [SerializeField] private Text _personaSummaryText;
        [SerializeField] private Text _narrativeBodyText;

        [Header("Demo Quick Preview Buttons (4 Endings)")]
        [SerializeField] private Button _previewNormal1Button;
        [SerializeField] private Button _previewNormal2Button;
        [SerializeField] private Button _previewBad1Button;
        [SerializeField] private Button _previewBad2Button;

        [Header("Navigation Buttons")]
        [SerializeField] private Button _retryGameButton;
        [SerializeField] private Button _returnLobbyButton;

        private IStoreAudioPlayer _audioPlayer;
        private EmployeePersonaSetupData _currentPersona = EmployeePersonaPresets.CreatePreset1();
        private int _currentTargetBalanceWon = 10000;
        private bool _currentUsesSalesTarget = true;

        public bool IsVisible => (_panelRoot != null ? _panelRoot : gameObject).activeSelf;
        public StoreEndingType CurrentEndingType { get; private set; }

        public event Action RetryClicked;
        public event Action ReturnLobbyClicked;

        public void BindAudioPlayer(IStoreAudioPlayer audioPlayer)
        {
            _audioPlayer = audioPlayer;
        }

        private void OnEnable()
        {
            if (_previewNormal1Button != null)
            {
                _previewNormal1Button.onClick.AddListener(HandlePreviewNormal1Clicked);
            }

            if (_previewNormal2Button != null)
            {
                _previewNormal2Button.onClick.AddListener(HandlePreviewNormal2Clicked);
            }

            if (_previewBad1Button != null)
            {
                _previewBad1Button.onClick.AddListener(HandlePreviewBad1Clicked);
            }

            if (_previewBad2Button != null)
            {
                _previewBad2Button.onClick.AddListener(HandlePreviewBad2Clicked);
            }

            if (_retryGameButton != null)
            {
                _retryGameButton.onClick.AddListener(HandleRetryClicked);
            }

            if (_returnLobbyButton != null)
            {
                _returnLobbyButton.onClick.AddListener(HandleReturnLobbyClicked);
            }
        }

        private void OnDisable()
        {
            if (_previewNormal1Button != null)
            {
                _previewNormal1Button.onClick.RemoveListener(HandlePreviewNormal1Clicked);
            }

            if (_previewNormal2Button != null)
            {
                _previewNormal2Button.onClick.RemoveListener(HandlePreviewNormal2Clicked);
            }

            if (_previewBad1Button != null)
            {
                _previewBad1Button.onClick.RemoveListener(HandlePreviewBad1Clicked);
            }

            if (_previewBad2Button != null)
            {
                _previewBad2Button.onClick.RemoveListener(HandlePreviewBad2Clicked);
            }

            if (_retryGameButton != null)
            {
                _retryGameButton.onClick.RemoveListener(HandleRetryClicked);
            }

            if (_returnLobbyButton != null)
            {
                _returnLobbyButton.onClick.RemoveListener(HandleReturnLobbyClicked);
            }
        }

        public void SetVisible(bool isVisible)
        {
            GameObject target = _panelRoot != null ? _panelRoot : gameObject;
            if (target.activeSelf != isVisible)
            {
                target.SetActive(isVisible);
            }
        }

        public void RenderEnding(DaySettlementResult settlement, EmployeePersonaSetupData persona)
        {
            _currentPersona = persona;
            _currentUsesSalesTarget = settlement.UsesSalesRevenueTarget;
            _currentTargetBalanceWon = settlement.EffectiveTargetWon > 0 ? settlement.EffectiveTargetWon : 10000;
            StoreEndingReport report = StoreEndingRules.EvaluateAndBuildReport(settlement, persona);
            RenderReport(report);
        }

        public void RenderReport(StoreEndingReport report)
        {
            CurrentEndingType = report.EndingType;

            if (_headerBanner != null)
            {
                switch (report.EndingType)
                {
                    case StoreEndingType.NormalEnding1_SuccessLowStress:
                        _headerBanner.color = new Color(0.10f, 0.56f, 0.40f, 1f);
                        break;
                    case StoreEndingType.NormalEnding2_SuccessHighStress:
                        _headerBanner.color = new Color(0.74f, 0.44f, 0.12f, 1f);
                        break;
                    case StoreEndingType.BadEnding1_TargetShortfall:
                        _headerBanner.color = new Color(0.56f, 0.22f, 0.28f, 1f);
                        break;
                    default:
                        _headerBanner.color = new Color(0.72f, 0.14f, 0.16f, 1f);
                        break;
                }
            }

            if (_headerTitleText != null)
            {
                _headerTitleText.text = report.HeaderTitle;
            }

            if (_conditionSummaryText != null)
            {
                _conditionSummaryText.text = report.ConditionSummary;
            }

            if (_cutscenePlaceholderText != null)
            {
                _cutscenePlaceholderText.text = report.CutscenePlaceholderLabel;
            }

            if (_personaSummaryText != null)
            {
                _personaSummaryText.text = report.PersonaSummaryLine;
            }

            if (_narrativeBodyText != null)
            {
                _narrativeBodyText.text = report.NarrativeBodyText;
            }
        }

        private void HandlePreviewNormal1Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            StoreEndingReport report = StoreEndingRules.BuildReport(
                StoreEndingType.NormalEnding1_SuccessLowStress,
                _currentPersona,
                endingStress: 28f,
                finalBalanceWon: _currentTargetBalanceWon + 12000,
                targetBalanceWon: _currentTargetBalanceWon,
                isSalesTarget: _currentUsesSalesTarget);
            RenderReport(report);
        }

        private void HandlePreviewNormal2Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            StoreEndingReport report = StoreEndingRules.BuildReport(
                StoreEndingType.NormalEnding2_SuccessHighStress,
                _currentPersona,
                endingStress: 84f,
                finalBalanceWon: _currentTargetBalanceWon + 5000,
                targetBalanceWon: _currentTargetBalanceWon,
                isSalesTarget: _currentUsesSalesTarget);
            RenderReport(report);
        }

        private void HandlePreviewBad1Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            StoreEndingReport report = StoreEndingRules.BuildReport(
                StoreEndingType.BadEnding1_TargetShortfall,
                _currentPersona,
                endingStress: 55f,
                finalBalanceWon: Math.Max(0, _currentTargetBalanceWon - 12000),
                targetBalanceWon: _currentTargetBalanceWon,
                isSalesTarget: _currentUsesSalesTarget);
            RenderReport(report);
        }

        private void HandlePreviewBad2Clicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            StoreEndingReport report = StoreEndingRules.BuildReport(
                StoreEndingType.BadEnding2_StressOverflow,
                _currentPersona,
                endingStress: 100f,
                finalBalanceWon: Math.Max(0, _currentTargetBalanceWon - 15000),
                targetBalanceWon: _currentTargetBalanceWon,
                isSalesTarget: _currentUsesSalesTarget);
            RenderReport(report);
        }

        private void HandleRetryClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            RetryClicked?.Invoke();
        }

        private void HandleReturnLobbyClicked()
        {
            _audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            ReturnLobbyClicked?.Invoke();
        }
    }
}
