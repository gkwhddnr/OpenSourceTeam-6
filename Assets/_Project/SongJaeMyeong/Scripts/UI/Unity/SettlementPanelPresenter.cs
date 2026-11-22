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

            if (screenTitleText != null)
            {
                screenTitleText.text = $"[ {result.DayNumber}일차 결과 및 정산 ]";
            }

            if (goalValueText != null)
            {
                goalValueText.text = $"목표 :  {result.TargetBalanceWon:N0}원";
            }

            if (currentValueText != null)
            {
                string sign = result.NetProfitWon >= 0 ? "+" : "";
                currentValueText.text = $"현재 :  {result.FinalBalanceWon:N0}원   (순이익 {sign}{result.NetProfitWon:N0}원)";
            }

            if (extraInfoSummaryText != null)
            {
                string gradeLabel = result.OutcomeGrade == DayOutcomeGrade.StableSuccess
                    ? "목표 달성 (안정)"
                    : (result.OutcomeGrade == DayOutcomeGrade.UnstableAngerSuccess ? "목표 달성 (불안정)" : "목표 미달");
                extraInfoSummaryText.text =
                    $"추가정보 :  {gradeLabel} · 당일 매출 +{result.FinalSalesRevenueWon:N0}원 (판매 {todayUnitsSold}개)\n" +
                    "↳  클릭 시 품목별 판매·재고 정산내역 출력  ▾";
            }

            if (extraInfoDetailBodyText != null)
            {
                int totalCosts = result.CostOfGoodsWon + result.FixedOperatingCostWon;
                string penaltyLine = result.RestockShortagePenaltyApplied ? "적용 (-20%)" : "없음";
                string invBlock = string.IsNullOrWhiteSpace(inventoryDetailReport)
                    ? $"[ 당일 판매 {todayUnitsSold}개 · 물품 매출 {itemSalesRevenueWon:N0}원 · 누적 매출 {cumulativeRevenueWon:N0}원 ]"
                    : inventoryDetailReport;

                extraInfoDetailBodyText.text =
                    $"기초 자금 {result.StartingBalanceWon:N0}원  |  업무 매출 +{(result.CheckoutRevenueWon + result.RestockRevenueWon):N0}원  |  물품 판매 +{itemSalesRevenueWon:N0}원\n" +
                    $"진열 패널티: {penaltyLine}  |  운영 비용(원가+고정비): -{totalCosts:N0}원\n" +
                    $"알바 종료 스트레스: {Mathf.RoundToInt(result.EndingStress)} / 100  ·  분노 거부: {result.AngerRefusalCount}회\n" +
                    "─────────────────────────────────────────\n" +
                    invBlock;
            }

            if (extraInfoDetailBox != null)
            {
                extraInfoDetailBox.SetActive(false);
            }

            if (nextDayButtonLabel != null)
            {
                nextDayButtonLabel.text = proceedsToEnding
                    ? "다음 (엔딩 보기)  →"
                    : $"다음 ({result.DayNumber + 1}일차 영업)  →";
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
            }

            if (extraInfoSummaryText != null)
            {
                extraInfoSummaryText.text = isExtraInfoExpanded
                    ? extraInfoSummaryText.text.Replace("▾", "▴").Replace("출력", "접기")
                    : extraInfoSummaryText.text.Replace("▴", "▾").Replace("접기", "출력");
            }
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
