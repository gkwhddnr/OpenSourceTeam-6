using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    /// <summary>
    /// 일차 매출 정산(영수증 확인) 직후 열리는 [알바 정산 화면] 프리젠터.
    /// 0일차 이력서 종이(클립보드) 양식 위에 플레이어가 입력했던 이름·성격·AI 심사평과
    /// 당일 근무 후 스트레스·피로도·충성도·보유 자산을 출력하고,
    /// 하단에서 [시급 내리기] / [시급 유지] / [시급 올리기]를 선택해 다음 일차에 반영한다.
    /// </summary>
    public sealed class EmployeeSettlementPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text headerTitleText;
        [SerializeField] private Text resumeInfoBodyText;
        [SerializeField] private Text statusAndAssetBodyText;
        [SerializeField] private Text conditionAdviceText;
        [SerializeField] private Button wageCutButton;
        [SerializeField] private Button wageFreezeButton;
        [SerializeField] private Button wageRaiseButton;
        [SerializeField] private Text wagePreviewText;
        [SerializeField] private Button confirmNextDayButton;
        [SerializeField] private Text confirmNextDayButtonLabel;

        private IStoreAudioPlayer audioPlayer;
        private EmployeePersonaSetupData currentPersona;
        private DaySettlementResult currentDayResult;
        private EmployeeHudSnapshot currentEmployeeSnapshot;
        private WageAdjustmentOption selectedOption = WageAdjustmentOption.Freeze;

        public bool IsVisible => (panelRoot != null ? panelRoot : gameObject).activeSelf;
        public WageAdjustmentOption SelectedOption => selectedOption;

        public event Action<WageAdjustmentOption, EmployeePersonaSetupData> WageSettlementConfirmed;

        public void BindAudioPlayer(IStoreAudioPlayer player)
        {
            audioPlayer = player;
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void BindButtons()
        {
            UnbindButtons();
            if (wageCutButton != null) wageCutButton.onClick.AddListener(HandleWageCutClicked);
            if (wageFreezeButton != null) wageFreezeButton.onClick.AddListener(HandleWageFreezeClicked);
            if (wageRaiseButton != null) wageRaiseButton.onClick.AddListener(HandleWageRaiseClicked);
            if (confirmNextDayButton != null) confirmNextDayButton.onClick.AddListener(HandleConfirmClicked);
        }

        private void UnbindButtons()
        {
            if (wageCutButton != null) wageCutButton.onClick.RemoveListener(HandleWageCutClicked);
            if (wageFreezeButton != null) wageFreezeButton.onClick.RemoveListener(HandleWageFreezeClicked);
            if (wageRaiseButton != null) wageRaiseButton.onClick.RemoveListener(HandleWageRaiseClicked);
            if (confirmNextDayButton != null) confirmNextDayButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        public void SetVisible(bool visible)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        public void RenderEmployeeSettlement(
            DaySettlementResult dayResult,
            EmployeePersonaSetupData persona,
            EmployeeHudSnapshot employeeSnapshot,
            Font fallbackFont = null,
            Sprite clipboardSprite = null)
        {
            EnsureRuntimeUiBuilt(fallbackFont, clipboardSprite);
            BindButtons();

            currentDayResult = dayResult;
            currentPersona = persona;
            currentEmployeeSnapshot = employeeSnapshot;
            selectedOption = WageAdjustmentOption.Freeze;

            if (headerTitleText != null)
            {
                headerTitleText.text = $"[ {dayResult.DayNumber}일차 마감  ·  알바 이력서 및 일급 정산 ]";
            }

            int baseDailyWage = EmployeePersonaPresets.ComputeDailyOperatingCostWon(
                persona.HourlyWageWon > 0 ? persona.HourlyWageWon : EmployeePersonaPresets.DefaultHourlyWageWon);
            string evalSummary = !string.IsNullOrWhiteSpace(persona.EvaluationSummary)
                ? persona.EvaluationSummary
                : $"표준 채용 프로필 적용 · 확정 일급 {baseDailyWage:N0}원";

            if (resumeInfoBodyText != null)
            {
                resumeInfoBodyText.text =
                    $"[ 채용 이력서 기재 사항 ]\n" +
                    $"• 성  명 :  {persona.EmployeeName}\n" +
                    $"• 입력 성격 :  \"{persona.PersonalityTraits}\"\n" +
                    $"• 면접 심사평 :  {evalSummary}";
            }

            int currentWage = persona.HourlyWageWon > 0 ? persona.HourlyWageWon : EmployeePersonaPresets.DefaultHourlyWageWon;
            int dailyWageCost = EmployeePersonaPresets.ComputeDailyOperatingCostWon(currentWage);
            int stressInt = Mathf.RoundToInt(employeeSnapshot.Stress);
            int fatigueInt = Mathf.RoundToInt(employeeSnapshot.Fatigue);
            int loyaltyInt = Mathf.RoundToInt(employeeSnapshot.Loyalty);
            int diligenceInt = Mathf.RoundToInt(persona.Profile.Diligence);
            int lazinessInt = Mathf.RoundToInt(persona.Profile.Laziness);
            string profitSign = dayResult.NetProfitWon >= 0 ? "+" : "";

            if (statusAndAssetBodyText != null)
            {
                statusAndAssetBodyText.text =
                    $"[ 당일 정산 자산 및 알바 상태 수치 ]\n" +
                    $"• 당일 매출 +{dayResult.FinalSalesRevenueWon:N0}원  -  영업원가 {dayResult.CostOfGoodsWon:N0}원  -  알바 일급 {dayResult.FixedOperatingCostWon:N0}원  =  순수익 {profitSign}{dayResult.NetProfitWon:N0}원\n" +
                    $"• 플레이어 보유 자산 (누적 재화) :  {dayResult.FinalBalanceWon:N0}원   (현재 일급 {dailyWageCost:N0}원)\n" +
                    $"• 현재 감정 수치 :  스트레스 {stressInt}/100   |   피로도 {fatigueInt}/100   |   충성도 {loyaltyInt}/100\n" +
                    $"• 세부 능력 배율 :  성실도 {diligenceInt}  ·  게으름 {lazinessInt}  ·  스트레스×{persona.Profile.WorkStressMultiplier:F2}  ·  피로×{persona.Profile.WorkFatigueMultiplier:F2}  ·  급여민감도×{persona.Profile.WageSensitivityMultiplier:F2}";
            }

            if (conditionAdviceText != null)
            {
                string advice;
                if (stressInt >= 65)
                {
                    advice = "※ [스트레스 경고] 스트레스가 높아 매출 효율이 떨어지고 있습니다! 일급을 올려 스트레스를 낮추거나 내일 바른말·칭찬을 건네세요.";
                    conditionAdviceText.color = new Color(0.68f, 0.16f, 0.14f, 1f);
                }
                else if (fatigueInt >= 45)
                {
                    advice = "※ [피로도 누적 권고] 피로도가 쌓여 실수·농땡이 확률이 증가합니다. 내일 영업 중 '잠깐 쉬어' 휴식 명령을 권장합니다.";
                    conditionAdviceText.color = new Color(0.58f, 0.32f, 0.08f, 1f);
                }
                else if (loyaltyInt >= 80)
                {
                    advice = "※ [충성도 우수] 높은 충성도로 업무 거부 확률이 낮습니다. 일급 인상으로 충성도를 유지하면 긍정 엔딩 분기에 유리합니다.";
                    conditionAdviceText.color = new Color(0.12f, 0.42f, 0.34f, 1f);
                }
                else
                {
                    advice = "※ [안정 상태] 보유 자산과 알바의 급여 민감도를 고려하여 내일 적용할 일급을 아래에서 선택해 주세요.";
                    conditionAdviceText.color = new Color(0.18f, 0.34f, 0.30f, 1f);
                }
                conditionAdviceText.text = advice;
            }

            if (confirmNextDayButtonLabel != null)
            {
                confirmNextDayButtonLabel.text = $"일급 정산 확정  ·  {dayResult.DayNumber + 1}일차 영업 시작  →";
            }

            RefreshSelectionPreview();
        }

        private void HandleWageCutClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            selectedOption = WageAdjustmentOption.Cut;
            RefreshSelectionPreview();
        }

        private void HandleWageFreezeClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            selectedOption = WageAdjustmentOption.Freeze;
            RefreshSelectionPreview();
        }

        private void HandleWageRaiseClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.UiClick);
            selectedOption = WageAdjustmentOption.Raise;
            RefreshSelectionPreview();
        }

        private void HandleConfirmClicked()
        {
            audioPlayer?.PlaySfx(StoreSfxId.PencilWrite);
            EmployeePersonaSetupData updatedPersona = BuildAdjustedPersonaForNextDay(
                currentPersona,
                currentEmployeeSnapshot,
                selectedOption);
            WageSettlementConfirmed?.Invoke(selectedOption, updatedPersona);
        }

        public static EmployeePersonaSetupData BuildAdjustedPersonaForNextDay(
            EmployeePersonaSetupData persona,
            EmployeeHudSnapshot snapshot,
            WageAdjustmentOption option)
        {
            ComputeNextDayPreview(
                persona,
                snapshot,
                option,
                out int nextHourlyWage,
                out _,
                out float nextStress,
                out float nextLoyalty,
                out _,
                out _);

            EmployeePersonalityProfile p = persona.Profile;
            EmployeePersonalityProfile updatedProfile = new EmployeePersonalityProfile(
                p.Diligence,
                p.Laziness,
                nextLoyalty,
                p.Archetype,
                nextStress,
                p.WorkStressMultiplier,
                p.RestRecoveryMultiplier,
                p.TaskSwitchMultiplier,
                p.HarshToneMultiplier,
                p.PraiseRecoveryMultiplier,
                p.AmbiguousCommandPenalty,
                p.WageSensitivityMultiplier,
                p.MoveSpeedMultiplier,
                p.WorkFatigueMultiplier);

            string optionKor = option == WageAdjustmentOption.Raise
                ? "일급 인상"
                : (option == WageAdjustmentOption.Cut ? "일급 삭감" : "일급 동결");
            int nextDailyWage = EmployeePersonaPresets.ComputeDailyOperatingCostWon(nextHourlyWage);
            string summary = $"{optionKor} 적용 → 일급 {nextDailyWage:N0}원 (초기 스트레스 {Mathf.RoundToInt(nextStress)} · 충성도 {Mathf.RoundToInt(nextLoyalty)})";

            return new EmployeePersonaSetupData(
                persona.EmployeeName,
                persona.PersonalityTraits,
                nextHourlyWage,
                updatedProfile,
                summary);
        }

        private static void ComputeNextDayPreview(
            EmployeePersonaSetupData persona,
            EmployeeHudSnapshot snapshot,
            WageAdjustmentOption option,
            out int nextHourlyWageWon,
            out int nextDailyOperatingCostWon,
            out float nextInitialStress,
            out float nextLoyalty,
            out int stressDeltaInt,
            out int loyaltyDeltaInt)
        {
            int curWage = persona.HourlyWageWon > 0 ? persona.HourlyWageWon : EmployeePersonaPresets.DefaultHourlyWageWon;
            float sens = Mathf.Max(0.5f, persona.Profile.WageSensitivityMultiplier);

            switch (option)
            {
                case WageAdjustmentOption.Cut:
                    nextHourlyWageWon = Mathf.Max(8000, Mathf.RoundToInt((curWage * 0.90f) / 100f) * 100);
                    break;
                case WageAdjustmentOption.Raise:
                    nextHourlyWageWon = Mathf.Min(30000, Mathf.RoundToInt((curWage * 1.10f) / 100f) * 100);
                    break;
                default:
                    nextHourlyWageWon = curWage;
                    break;
            }

            nextDailyOperatingCostWon = EmployeePersonaPresets.ComputeDailyOperatingCostWon(nextHourlyWageWon);

            float rawStressDelta = StressRules.EvaluateWageAdjustmentStressDelta(option, persona.Profile);
            float rawLoyaltyDelta = option == WageAdjustmentOption.Raise
                ? 10f * sens
                : (option == WageAdjustmentOption.Cut ? -12f * sens : 2f);

            nextInitialStress = StressRules.ClampStat(snapshot.Stress + rawStressDelta);
            nextLoyalty = StressRules.ClampStat(snapshot.Loyalty + rawLoyaltyDelta);
            stressDeltaInt = Mathf.RoundToInt(nextInitialStress - snapshot.Stress);
            loyaltyDeltaInt = Mathf.RoundToInt(nextLoyalty - snapshot.Loyalty);
        }

        private void RefreshSelectionPreview()
        {
            ComputeNextDayPreview(
                currentPersona,
                currentEmployeeSnapshot,
                selectedOption,
                out int nextWage,
                out int nextDailyCost,
                out float nextStress,
                out float nextLoyalty,
                out int stressDelta,
                out int loyaltyDelta);

            HighlightOptionButton(wageCutButton, selectedOption == WageAdjustmentOption.Cut, new Color(0.86f, 0.42f, 0.36f, 1f));
            HighlightOptionButton(wageFreezeButton, selectedOption == WageAdjustmentOption.Freeze, new Color(0.42f, 0.56f, 0.68f, 1f));
            HighlightOptionButton(wageRaiseButton, selectedOption == WageAdjustmentOption.Raise, new Color(0.24f, 0.68f, 0.52f, 1f));

            if (wagePreviewText != null)
            {
                string optLabel = selectedOption == WageAdjustmentOption.Cut
                    ? "[일급 내리기 (-10%) 선택]"
                    : (selectedOption == WageAdjustmentOption.Raise ? "[일급 올리기 (+10%) 선택]" : "[일급 유지 (동결) 선택]");
                string sSign = stressDelta >= 0 ? "+" : "";
                string lSign = loyaltyDelta >= 0 ? "+" : "";

                wagePreviewText.text =
                    $"▶ {optLabel}  내일 일급 {nextDailyCost:N0}원\n" +
                    $"   예상 내일 시작 스트레스: {Mathf.RoundToInt(nextStress)} ({sSign}{stressDelta})   |   예상 충성도: {Mathf.RoundToInt(nextLoyalty)} ({lSign}{loyaltyDelta})";
            }
        }

        private static void HighlightOptionButton(Button btn, bool isSelected, Color activeColor)
        {
            if (btn == null) return;
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? activeColor : new Color(0.88f, 0.85f, 0.78f, 0.95f);
            }

            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.color = isSelected ? Color.white : new Color(0.16f, 0.15f, 0.13f, 1f);
                txt.fontStyle = FontStyle.Normal;
            }
        }

        public void EnsureRuntimeUiBuilt(Font fallbackFont = null, Sprite clipboardSprite = null)
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            Transform upgradedContent = panelRoot.transform.Find("EmployeeSettlementPaper/PaperContentArea/PaperContent2x");
            if (upgradedContent != null && headerTitleText != null && resumeInfoBodyText != null && wageFreezeButton != null)
            {
                return;
            }

            // 구버전 레이아웃(클립보드 금속 집게·가죽 테두리 침범 버전)이 남아 있으면 정리 후 정확한 종이 규격으로 재구성
            for (int i = panelRoot.transform.childCount - 1; i >= 0; i--)
            {
                GameObject oldChild = panelRoot.transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(oldChild);
                }
                else
                {
                    DestroyImmediate(oldChild);
                }
            }

            Font font = fallbackFont != null ? fallbackFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform rootRt = panelRoot.GetComponent<RectTransform>();
            if (rootRt == null) rootRt = panelRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;

            Image dimImg = panelRoot.GetComponent<Image>();
            if (dimImg == null) dimImg = panelRoot.AddComponent<Image>();
            dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.66f);

            // 0일차 채용 이력서(PersonaSetupPanel)와 100% 동일한 클립보드 규격 (906x1326 비율 -> 697x1020)
            GameObject sheetObj = new GameObject("EmployeeSettlementPaper", typeof(RectTransform), typeof(Image));
            sheetObj.transform.SetParent(panelRoot.transform, false);
            RectTransform sheetRt = sheetObj.GetComponent<RectTransform>();
            sheetRt.anchorMin = new Vector2(0.5f, 0.5f);
            sheetRt.anchorMax = new Vector2(0.5f, 0.5f);
            sheetRt.pivot = new Vector2(0.5f, 0.5f);
            sheetRt.anchoredPosition = Vector2.zero;
            sheetRt.sizeDelta = new Vector2(697f, 1020f);

            Image sheetImg = sheetObj.GetComponent<Image>();
            if (clipboardSprite != null)
            {
                sheetImg.sprite = clipboardSprite;
                sheetImg.type = Image.Type.Simple;
                sheetImg.preserveAspect = true;
                sheetImg.color = Color.white;
            }
            else
            {
                sheetImg.color = new Color(0.96f, 0.94f, 0.88f, 0.99f);
            }

            // 클립보드 내부 실제 흰 종이 영역 (좌우 84px, 상단 금속 클립 아래 172px, 하단 88px -> 529 x 760)
            GameObject contentArea = new GameObject("PaperContentArea", typeof(RectTransform));
            contentArea.transform.SetParent(sheetObj.transform, false);
            RectTransform contentRt = contentArea.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.offsetMin = new Vector2(84f, 88f);
            contentRt.offsetMax = new Vector2(-84f, -172f);

            // 2배 슈퍼샘플링 컨테이너 (1058 x 1520 -> 0.5배 축소로 작은 창에서도 글자 선명도 유지)
            GameObject content2x = new GameObject("PaperContent2x", typeof(RectTransform));
            content2x.transform.SetParent(contentArea.transform, false);
            RectTransform c2xRt = content2x.GetComponent<RectTransform>();
            c2xRt.anchorMin = new Vector2(0.5f, 0.5f);
            c2xRt.anchorMax = new Vector2(0.5f, 0.5f);
            c2xRt.pivot = new Vector2(0.5f, 0.5f);
            c2xRt.anchoredPosition = Vector2.zero;
            c2xRt.sizeDelta = new Vector2(1058f, 1520f);
            c2xRt.localScale = new Vector3(0.5f, 0.5f, 1f);

            Color inkPrimary = new Color(0.14f, 0.13f, 0.12f, 1f);
            Color inkSecondary = new Color(0.36f, 0.34f, 0.30f, 1f);
            Color inkAccent = new Color(0.12f, 0.42f, 0.36f, 1f);
            Color inkRule = new Color(0.24f, 0.22f, 0.20f, 0.46f);
            Color boxBg = new Color(0.99f, 0.98f, 0.94f, 0.78f);

            Text subHeader = CreateRuntimeText(
                "SettlementSubHeader",
                content2x.transform,
                font,
                "일일 마감  ·  스태프 근무 평가 및 급여 정산표",
                26,
                FontStyle.Normal,
                inkSecondary,
                TextAnchor.MiddleCenter);
            SetRect(subHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, 38f));

            headerTitleText = CreateRuntimeText(
                "HeaderTitleText",
                content2x.transform,
                font,
                "[ 1일차 마감  ·  알바 이력서 및 일급 정산 ]",
                42,
                FontStyle.Normal,
                inkPrimary,
                TextAnchor.MiddleCenter);
            SetRect(headerTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(0f, 68f));

            CreateRuntimeRule("HeaderRuleBold", content2x.transform, -120f, 4.5f, new Color(0.18f, 0.16f, 0.15f, 0.78f));
            CreateRuntimeRule("HeaderRuleThin", content2x.transform, -128f, 2f, inkRule);

            // 1. 이력서 기재 내용 박스
            GameObject resumeBox = CreateRuntimeBox("ResumeInfoBox", content2x.transform, boxBg);
            SetRect(resumeBox.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(0f, 296f));
            resumeInfoBodyText = CreateRuntimeText("ResumeInfoBodyText", resumeBox.transform, font, "", 30, FontStyle.Normal, inkPrimary, TextAnchor.UpperLeft);
            resumeInfoBodyText.lineSpacing = 1.22f;
            SetRect(resumeInfoBodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-44f, -32f));

            // 2. 당일 정산 자산 & 알바 감정/능력 수치 박스
            GameObject statusBox = CreateRuntimeBox("StatusAndAssetBox", content2x.transform, boxBg);
            SetRect(statusBox.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(0f, 368f));
            statusAndAssetBodyText = CreateRuntimeText("StatusAndAssetBodyText", statusBox.transform, font, "", 29, FontStyle.Normal, inkPrimary, TextAnchor.UpperLeft);
            statusAndAssetBodyText.lineSpacing = 1.24f;
            SetRect(statusAndAssetBodyText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-44f, -32f));

            // 3. 컨디션 권고 문구
            conditionAdviceText = CreateRuntimeText("ConditionAdviceText", content2x.transform, font, "", 28, FontStyle.Normal, inkAccent, TextAnchor.MiddleLeft);
            conditionAdviceText.lineSpacing = 1.18f;
            SetRect(conditionAdviceText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -844f), new Vector2(-12f, 96f));

            CreateRuntimeRule("MidSectionRule", content2x.transform, -952f, 2f, inkRule);

            // 4. 하단 일급 조정 선택 타이틀 & 3버튼 (일급 내리기 / 일급 유지 / 일급 올리기)
            Text wageSectionTitle = CreateRuntimeText(
                "WageSectionTitle",
                content2x.transform,
                font,
                "내일 적용할 알바 일급 선택  (스트레스 · 충성도 · 하루 인건비 연동)",
                30,
                FontStyle.Normal,
                inkPrimary,
                TextAnchor.MiddleLeft);
            SetRect(wageSectionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -968f), new Vector2(0f, 48f));

            wageCutButton = CreateRuntimeButton("WageCutButton", content2x.transform, font, "일급 내리기 (-10%)", 31);
            SetRect(wageCutButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -1030f), new Vector2(338f, 104f));

            wageFreezeButton = CreateRuntimeButton("WageFreezeButton", content2x.transform, font, "일급 유지 (동결)", 31);
            SetRect(wageFreezeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1030f), new Vector2(338f, 104f));

            wageRaiseButton = CreateRuntimeButton("WageRaiseButton", content2x.transform, font, "일급 올리기 (+10%)", 31);
            SetRect(wageRaiseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1030f), new Vector2(338f, 104f));

            // 5. 선택 결과 미리보기 박스
            GameObject previewBox = CreateRuntimeBox("WagePreviewBox", content2x.transform, new Color(0.92f, 0.95f, 0.92f, 0.92f));
            SetRect(previewBox.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1152f), new Vector2(0f, 156f));
            wagePreviewText = CreateRuntimeText("WagePreviewText", previewBox.transform, font, "", 29, FontStyle.Normal, inkAccent, TextAnchor.MiddleCenter);
            wagePreviewText.lineSpacing = 1.24f;
            SetRect(wagePreviewText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32f, -20f));

            // 6. 최하단 정산 확정 및 다음 일차 시작 버튼
            confirmNextDayButton = CreateRuntimeButton("ConfirmWageNextDayButton", content2x.transform, font, "일급 정산 확정  ·  2일차 영업 시작  →", 37);
            Image confirmImg = confirmNextDayButton.GetComponent<Image>();
            if (confirmImg != null) confirmImg.color = new Color(0.14f, 0.42f, 0.37f, 0.98f);
            confirmNextDayButtonLabel = confirmNextDayButton.GetComponentInChildren<Text>(true);
            if (confirmNextDayButtonLabel != null) confirmNextDayButtonLabel.color = Color.white;
            SetRect(confirmNextDayButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(0f, 124f));
        }

        private static void CreateRuntimeRule(string name, Transform parent, float topOffsetY, float thickness, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            SetRect(go.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, topOffsetY), new Vector2(0f, thickness));
        }

        private static GameObject CreateRuntimeBox(string name, Transform parent, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = bgColor;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.22f, 0.18f, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);
            return go;
        }

        private static Text CreateRuntimeText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Normal;
            txt.color = color;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateRuntimeButton(string name, Transform parent, Font font, string label, int fontSize)
        {
            GameObject go = CreateRuntimeBox(name, parent, new Color(0.90f, 0.87f, 0.80f, 0.96f));
            Button btn = go.AddComponent<Button>();
            Text txt = CreateRuntimeText("Label", go.transform, font, label, fontSize, FontStyle.Bold, new Color(0.15f, 0.14f, 0.12f, 1f), TextAnchor.MiddleCenter);
            SetRect(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return btn;
        }

        private static void SetRect(
            RectTransform rt,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }
    }
}
