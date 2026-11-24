using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Time = UnityEngine.Time;

    /// <summary>
    /// 일차 정산 화면(와이어프레임 2단계 연출):
    /// 1단계: 검은 화면 위 3D 포스기 중앙 표시 + 화살표 클릭 안내
    /// 2단계: 클릭 시 포스기 영수증 종이 출력 + 우측 [정산 내역] 영수증(목표/현재/추가정보 클릭 펼치기/다음→) 표시
    /// </summary>
    public sealed class SettlementPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text screenTitleText;
        [SerializeField] private Button posClickButton;
        [SerializeField] private GameObject posPromptRoot;
        [SerializeField] private RectTransform posPromptArrowRect;
        [SerializeField] private GameObject dashedArrowRoot;
        [SerializeField] private GameObject receiptSheetRoot;
        [SerializeField] private CanvasGroup receiptCanvasGroup;
        [SerializeField] private Text goalValueText;
        [SerializeField] private Text currentValueText;
        [SerializeField] private Button extraInfoToggleButton;
        [SerializeField] private Text extraInfoSummaryText;
        [SerializeField] private GameObject extraInfoDetailBox;
        [SerializeField] private Text extraInfoDetailBodyText;
        [SerializeField] private Button nextDayButton;
        [SerializeField] private Text nextDayButtonLabel;
        [SerializeField] private Button viewEndingButton;
        [SerializeField] private Button returnLobbyButton;

        private bool isReceiptPrinted;
        private bool isAnimatingReceipt;
        private bool isExtraInfoExpanded;
        private bool proceedsToEnding;
        private float receiptAnimElapsed;
        private Vector2 promptArrowBasePos;
        private bool hasCapturedArrowPos;
        private IStoreAudioPlayer audioPlayer;
        private ScrollRect extraInfoScrollRect;

        public bool IsVisible => (panelRoot != null ? panelRoot : gameObject).activeSelf;

        public event Action PosPrintRequested;
        public event Action NextDayClicked;
        public event Action ViewEndingClicked;
#pragma warning disable CS0067
        public event Action RetryDayClicked;
#pragma warning restore CS0067
        public event Action ReturnLobbyClicked;

        public void BindAudioPlayer(IStoreAudioPlayer player)
        {
            audioPlayer = player;
        }

        private void OnEnable()
        {
            if (posClickButton != null) posClickButton.onClick.AddListener(HandlePosClicked);
            if (extraInfoToggleButton != null) extraInfoToggleButton.onClick.AddListener(HandleExtraInfoToggleClicked);
            if (nextDayButton != null) nextDayButton.onClick.AddListener(HandleNextDayClicked);
            if (viewEndingButton != null) viewEndingButton.onClick.AddListener(HandleViewEndingClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(HandleReturnLobbyClicked);

            if (posPromptArrowRect != null && !hasCapturedArrowPos)
            {
                promptArrowBasePos = posPromptArrowRect.anchoredPosition;
                hasCapturedArrowPos = true;
            }
        }

        private void OnDisable()
        {
            if (posClickButton != null) posClickButton.onClick.RemoveListener(HandlePosClicked);
            if (extraInfoToggleButton != null) extraInfoToggleButton.onClick.RemoveListener(HandleExtraInfoToggleClicked);
            if (nextDayButton != null) nextDayButton.onClick.RemoveListener(HandleNextDayClicked);
            if (viewEndingButton != null) viewEndingButton.onClick.RemoveListener(HandleViewEndingClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.RemoveListener(HandleReturnLobbyClicked);
        }

        private void Update()
        {
            if (!IsVisible) return;

            if (!isReceiptPrinted && posPromptArrowRect != null && hasCapturedArrowPos)
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 4.5f) * 10f;
                posPromptArrowRect.anchoredPosition = promptArrowBasePos + new Vector2(bounce, 0f);
            }

            if (isAnimatingReceipt)
            {
                receiptAnimElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01((receiptAnimElapsed - 0.22f) / 0.45f);
                if (receiptCanvasGroup != null)
                {
                    receiptCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                    receiptCanvasGroup.interactable = t >= 0.85f;
                    receiptCanvasGroup.blocksRaycasts = t >= 0.85f;
                }

                if (t >= 1f)
                {
                    isAnimatingReceipt = false;
                }
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

        public void RenderResult(
            DaySettlementResult result,
            int itemSalesRevenueWon = 0,
            int cumulativeRevenueWon = 0,
            int todayUnitsSold = 0,
            string inventoryDetailReport = null,
            bool isFinalDayOrForcedEnding = false)
        {
            isReceiptPrinted = false;
            isAnimatingReceipt = false;
            isExtraInfoExpanded = false;
            proceedsToEnding = isFinalDayOrForcedEnding;
            receiptAnimElapsed = 0f;

            EnsureScrollableExtraInfoDetailArea();

            if (screenTitleText != null)
            {
                screenTitleText.text = $"[ {result.DayNumber}일차 결과 및 매출 정산 ]";
            }

            if (goalValueText != null)
            {
                goalValueText.text = result.UsesSalesRevenueTarget
                    ? $"목표 매출 :  {result.TargetSalesRevenueWon:N0}원   (당일 매출 +{result.FinalSalesRevenueWon:N0}원)"
                    : $"목표 자산 :  {result.TargetBalanceWon:N0}원   (당일 매출 +{result.FinalSalesRevenueWon:N0}원)";
            }

            if (currentValueText != null)
            {
                string sign = result.NetProfitWon >= 0 ? "+" : "";
                currentValueText.text = $"보유 자산 :  {result.FinalBalanceWon:N0}원   (당일 순익 {sign}{result.NetProfitWon:N0}원)";
            }

            if (extraInfoSummaryText != null)
            {
                string gradeLabel = result.OutcomeGrade == DayOutcomeGrade.StableSuccess
                    ? "목표 달성 (안정)"
                    : (result.OutcomeGrade == DayOutcomeGrade.UnstableAngerSuccess ? "목표 달성 (불안정)" : "목표 미달 (사출 위험)");
                extraInfoSummaryText.text =
                    $"추가정보 :  {gradeLabel} · 당일 매출 +{result.FinalSalesRevenueWon:N0}원 (판매 {todayUnitsSold}개)\n" +
                    "↳  클릭 시 태그(Item_*)별 · 품목별 판매 로그 전체 스크롤 출력  ▾";
            }

            if (extraInfoDetailBodyText != null)
            {
                int totalCosts = result.CostOfGoodsWon + result.FixedOperatingCostWon;
                string penaltyLine = result.RestockShortagePenaltyApplied ? "적용 (-20%)" : "없음";
                string invBlock = string.IsNullOrWhiteSpace(inventoryDetailReport)
                    ? $"[ 당일 판매 {todayUnitsSold}개 · 물품 매출 {itemSalesRevenueWon:N0}원 · 누적 매출 {cumulativeRevenueWon:N0}원 ]"
                    : inventoryDetailReport;
                string profitSign = result.NetProfitWon >= 0 ? "+" : "";

                extraInfoDetailBodyText.text =
                    $"[ 자산 정산 공식 :  당일 매출 - 영업원가 - 알바급여 = 당일 순수익 ]\n" +
                    $"• 당일 총매출 +{result.FinalSalesRevenueWon:N0}원 (업무 +{(result.CheckoutRevenueWon + result.RestockRevenueWon):N0}원 / 물품 +{itemSalesRevenueWon:N0}원)\n" +
                    $"• 차감 비용 -{totalCosts:N0}원 (영업원가 -{result.CostOfGoodsWon:N0}원 / 알바 일일급여 -{result.FixedOperatingCostWon:N0}원 / 진열패널티 {penaltyLine})\n" +
                    $"• 기초 자산 {result.StartingBalanceWon:N0}원  +  순수익 {profitSign}{result.NetProfitWon:N0}원  =  최종 보유 자산 {result.FinalBalanceWon:N0}원\n" +
                    $"• 알바 종료 스트레스: {Mathf.RoundToInt(result.EndingStress)} / 100  ·  분노 거부: {result.AngerRefusalCount}회\n" +
                    "─────────────────────────────────────────\n" +
                    invBlock;

                if (extraInfoScrollRect != null)
                {
                    extraInfoScrollRect.verticalNormalizedPosition = 1f;
                }
            }

            if (extraInfoDetailBox != null)
            {
                extraInfoDetailBox.SetActive(false);
            }

            if (nextDayButtonLabel != null)
            {
                nextDayButtonLabel.text = proceedsToEnding
                    ? (result.OutcomeGrade == DayOutcomeGrade.FailedTargetShortfall
                        ? "다음 (사출 엔딩 보기)  →"
                        : "다음 (엔딩 보기)  →")
                    : "다음 (알바 일급 정산)  →";
            }

            // 1단계: 포스기 클릭 대기 상태로 초기화
            if (posClickButton != null) posClickButton.gameObject.SetActive(true);
            if (posPromptRoot != null) posPromptRoot.SetActive(true);
            if (dashedArrowRoot != null) dashedArrowRoot.SetActive(false);
            if (receiptSheetRoot != null) receiptSheetRoot.SetActive(false);
            if (receiptCanvasGroup != null)
            {
                receiptCanvasGroup.alpha = 0f;
                receiptCanvasGroup.interactable = false;
                receiptCanvasGroup.blocksRaycasts = false;
            }
        }

        public void TriggerPosPrintFromExternalClick()
        {
            HandlePosClicked();
        }

        private void HandlePosClicked()
        {
            if (isReceiptPrinted) return;
            isReceiptPrinted = true;
            isAnimatingReceipt = true;
            receiptAnimElapsed = 0f;

            audioPlayer?.PlaySfx(StoreSfxId.BarcodeCheckout);
            audioPlayer?.PlaySfx(StoreSfxId.PaperFlip);

            if (posClickButton != null) posClickButton.gameObject.SetActive(false);
            if (posPromptRoot != null) posPromptRoot.SetActive(false);
            if (dashedArrowRoot != null) dashedArrowRoot.SetActive(true);
            if (receiptSheetRoot != null) receiptSheetRoot.SetActive(true);

            PosPrintRequested?.Invoke();
        }

        private void HandleExtraInfoToggleClicked()
        {
            isExtraInfoExpanded = !isExtraInfoExpanded;
            audioPlayer?.PlaySfx(StoreSfxId.PaperFlip);

            if (extraInfoDetailBox != null)
            {
                extraInfoDetailBox.SetActive(isExtraInfoExpanded);
                if (isExtraInfoExpanded && extraInfoScrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    extraInfoScrollRect.verticalNormalizedPosition = 1f;
                }
            }

            if (extraInfoSummaryText != null)
            {
                extraInfoSummaryText.text = isExtraInfoExpanded
                    ? extraInfoSummaryText.text.Replace("▾", "▴").Replace("출력", "접기")
                    : extraInfoSummaryText.text.Replace("▴", "▾").Replace("접기", "출력");
            }
        }

        private void EnsureScrollableExtraInfoDetailArea()
        {
            if (extraInfoDetailBox == null || extraInfoDetailBodyText == null) return;
            if (extraInfoScrollRect != null) return;

            extraInfoScrollRect = extraInfoDetailBox.GetComponent<ScrollRect>();
            if (extraInfoScrollRect != null) return;

            if (extraInfoDetailBox.GetComponent<RectMask2D>() == null)
            {
                extraInfoDetailBox.AddComponent<RectMask2D>();
            }

            RectTransform textRt = extraInfoDetailBodyText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = new Vector2(0f, -12f);
            textRt.sizeDelta = new Vector2(-32f, 0f);

            extraInfoDetailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            extraInfoDetailBodyText.verticalOverflow = VerticalWrapMode.Overflow;

            ContentSizeFitter fitter = extraInfoDetailBodyText.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = extraInfoDetailBodyText.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            extraInfoScrollRect = extraInfoDetailBox.AddComponent<ScrollRect>();
            extraInfoScrollRect.horizontal = false;
            extraInfoScrollRect.vertical = true;
            extraInfoScrollRect.movementType = ScrollRect.MovementType.Clamped;
            extraInfoScrollRect.scrollSensitivity = 28f;
            extraInfoScrollRect.viewport = extraInfoDetailBox.GetComponent<RectTransform>();
            extraInfoScrollRect.content = textRt;
        }

        private void HandleNextDayClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            if (proceedsToEnding)
            {
                ViewEndingClicked?.Invoke();
            }
            else
            {
                NextDayClicked?.Invoke();
            }
        }

        private void HandleViewEndingClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            ViewEndingClicked?.Invoke();
        }

        private void HandleReturnLobbyClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            ReturnLobbyClicked?.Invoke();
        }
    }
}
