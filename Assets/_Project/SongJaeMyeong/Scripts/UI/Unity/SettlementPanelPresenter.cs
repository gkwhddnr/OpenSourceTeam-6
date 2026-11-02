using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    public sealed class SettlementPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image gradeHeaderBanner;
        [SerializeField] private Text gradeTitleText;
        [SerializeField] private Text gradeSummaryText;
        [SerializeField] private Text breakdownBodyText;
        [SerializeField] private Text finalBalanceHighlightText;
        [SerializeField] private Text staffSummaryText;
        [SerializeField] private Button retryDayButton;
        [SerializeField] private Button returnLobbyButton;

        public event Action RetryDayClicked;
        public event Action ReturnLobbyClicked;

        private void OnEnable()
        {
            if (retryDayButton != null) retryDayButton.onClick.AddListener(HandleRetryClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.AddListener(HandleReturnLobbyClicked);
        }

        private void OnDisable()
        {
            if (retryDayButton != null) retryDayButton.onClick.RemoveListener(HandleRetryClicked);
            if (returnLobbyButton != null) returnLobbyButton.onClick.RemoveListener(HandleReturnLobbyClicked);
        }

        public void SetVisible(bool visible)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        public void RenderResult(DaySettlementResult result)
        {
            if (gradeHeaderBanner != null && gradeTitleText != null && gradeSummaryText != null)
            {
                switch (result.OutcomeGrade)
                {
                    case DayOutcomeGrade.StableSuccess:
                        gradeHeaderBanner.color = new Color(0.12f, 0.56f, 0.38f, 1f);
                        gradeTitleText.text = $"{result.DayNumber}일차 정산 결과  ·  안정 운영 성공";
                        gradeSummaryText.text = "목표 잔액을 달성했으며 직원의 감정 상태도 안정적으로 유지했습니다.";
                        break;
                    case DayOutcomeGrade.UnstableAngerSuccess:
                        gradeHeaderBanner.color = new Color(0.72f, 0.44f, 0.10f, 1f);
                        gradeTitleText.text = $"{result.DayNumber}일차 정산 결과  ·  불안정 성공 (직원 분노 누적)";
                        gradeSummaryText.text = "목표 잔액은 달성했으나 무리한 지시로 직원의 분노·거부 이력이 결말에 기록되었습니다.";
                        break;
                    default:
                        gradeHeaderBanner.color = new Color(0.68f, 0.18f, 0.20f, 1f);
                        gradeTitleText.text = $"{result.DayNumber}일차 정산 결과  ·  목표 잔액 미달 실패";
                        gradeSummaryText.text = "당일 목표 잔액에 도달하지 못했습니다. 업무 분배와 감정 효율을 조정해 재도전하세요.";
                        break;
                }
            }

            if (breakdownBodyText != null)
            {
                string penaltyLine = result.RestockShortagePenaltyApplied
                    ? "-20% 감산 적용 (유효 진열 25초 미만)"
                    : "적용 없음 (진열 기준 충족)";

                int totalCosts = result.CostOfGoodsWon + result.FixedOperatingCostWon;

                breakdownBodyText.text =
                    $"기초 자금 :  {result.StartingBalanceWon:N0}원\n" +
                    $"계산 기여 매출 (유효 {result.EffectiveCheckoutSeconds:F1}초) :  +{result.CheckoutRevenueWon:N0}원\n" +
                    $"진열 기여 매출 (유효 {result.EffectiveRestockSeconds:F1}초) :  +{result.RestockRevenueWon:N0}원\n" +
                    $"진열 부족 패널티 :  {penaltyLine}\n" +
                    $"당일 최종 매출 :  +{result.FinalSalesRevenueWon:N0}원\n" +
                    $"운영 비용 (원가 {result.CostOfGoodsWon:N0}원 + 고정비 {result.FixedOperatingCostWon:N0}원) :  -{totalCosts:N0}원";
            }

            if (finalBalanceHighlightText != null)
            {
                string sign = result.NetProfitWon >= 0 ? "+" : "";
                finalBalanceHighlightText.text =
                    $"당일 최종 잔액 :  {result.FinalBalanceWon:N0}원   (당일 순이익 {sign}{result.NetProfitWon:N0}원 / 목표 {result.TargetBalanceWon:N0}원)";
            }

            if (staffSummaryText != null)
            {
                staffSummaryText.text =
                    $"직원 상태 기록  —  종료 스트레스: {Mathf.RoundToInt(result.EndingStress)} / 100   ·   분노 거부 누적: {result.AngerRefusalCount}회";
            }
        }

        private void HandleRetryClicked()
        {
            RetryDayClicked?.Invoke();
        }

        private void HandleReturnLobbyClicked()
        {
            ReturnLobbyClicked?.Invoke();
        }
    }
}
