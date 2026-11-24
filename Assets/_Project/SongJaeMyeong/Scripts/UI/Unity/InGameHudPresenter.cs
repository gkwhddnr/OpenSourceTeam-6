using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    using Time = UnityEngine.Time;
    public sealed class InGameHudPresenter : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject hudRoot;

        [Header("Top Store Bar")]
        [SerializeField] private Text dayLabelText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text balanceText;
        [SerializeField] private Button openCommandOverlayButton;

        [Header("3D Counter Monitor Screen (World Space)")]
        [SerializeField] private Text counterMonitorDayText;
        [SerializeField] private Text counterMonitorTimerText;
        [SerializeField] private Text counterMonitorBalanceText;

        [Header("Employee Status Card")]
        [SerializeField] private GameObject employeeStatusCardRoot;
        [SerializeField] private Sprite foldedNoteSprite;
        [SerializeField] private Text employeeHeaderText;
        [SerializeField] private Text currentRoleText;
        [SerializeField] private Image moveBadgeBackground;
        [SerializeField] private Text moveBadgeText;
        [SerializeField] private Image stressFillImage;
        [SerializeField] private Text stressValueText;
        [SerializeField] private Image fatigueFillImage;
        [SerializeField] private Text fatigueValueText;
        [SerializeField] private Image loyaltyFillImage;
        [SerializeField] private Text loyaltyValueText;

        [Header("Voice & Feedback Dock")]
        [SerializeField] private Image voiceStageBadgeBg;
        [SerializeField] private Text voiceStageText;
        [SerializeField] private Text transcriptText;
        [SerializeField] private Image feedbackBannerBg;
        [SerializeField] private Text feedbackBannerText;

        private const string FoldedNoteResourcePath = "UI/Spr_FoldedNote";

        private VoiceProcessStage currentVoiceStage = VoiceProcessStage.Idle;
        private float processingAnimTimer;
        private bool isEmployeeCardCollapsed;
        private GameObject expandedCollapseHintRoot;
        private GameObject collapsedNoteRoot;
        private Sprite runtimeCreatedFoldedSprite;

        public bool IsEmployeeCardCollapsed => isEmployeeCardCollapsed;

        public event Action OpenCommandOverlayClicked;
        public event Action EmployeeCardFoldToggleClicked;

        private static readonly Color StressStableColor = new Color(0.20f, 0.78f, 0.52f, 1f);
        private static readonly Color StressComplainColor = new Color(0.95f, 0.72f, 0.18f, 1f);
        private static readonly Color StressAngryColor = new Color(0.92f, 0.27f, 0.27f, 1f);

        private static readonly Color StageIdleColor = new Color(0.24f, 0.29f, 0.36f, 0.95f);
        private static readonly Color StageListeningColor = new Color(0.06f, 0.65f, 0.56f, 0.95f);
        private static readonly Color StageProcessingColor = new Color(0.16f, 0.48f, 0.85f, 0.95f);
        private static readonly Color StageDoneColor = new Color(0.22f, 0.62f, 0.38f, 0.95f);

        private static readonly Color BannerInfoColor = new Color(0.15f, 0.20f, 0.27f, 0.92f);
        private static readonly Color BannerAcceptedColor = new Color(0.11f, 0.42f, 0.30f, 0.95f);
        private static readonly Color BannerRecognitionFailColor = new Color(0.58f, 0.43f, 0.08f, 0.96f);
        private static readonly Color BannerEmotionRefusedColor = new Color(0.62f, 0.15f, 0.17f, 0.96f);
        private static readonly Color BannerSystemErrorColor = new Color(0.48f, 0.26f, 0.12f, 0.96f);

        private void OnEnable()
        {
            if (openCommandOverlayButton != null)
            {
                openCommandOverlayButton.onClick.AddListener(HandleOpenOverlayClicked);
            }
        }

        private void OnDisable()
        {
            if (openCommandOverlayButton != null)
            {
                openCommandOverlayButton.onClick.RemoveListener(HandleOpenOverlayClicked);
            }
        }

        private void Update()
        {
            if (currentVoiceStage != VoiceProcessStage.Processing || voiceStageText == null) return;

            processingAnimTimer += Time.unscaledDeltaTime;
            int dots = ((int)(processingAnimTimer * 3f) % 3) + 1;
            string dotSuffix = dots == 1 ? "." : (dots == 2 ? ".." : "...");
            voiceStageText.text = "음성 인식 및 AI 판단 중" + dotSuffix;
        }

        public void SetVisible(bool visible)
        {
            GameObject target = hudRoot != null ? hudRoot : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }

            if (visible)
            {
                EnsureCollapsibleNoteWidgets();
                ApplyEmployeeCardFoldState();
            }
        }

        public void SetEmployeeCardCollapsed(bool collapsed)
        {
            isEmployeeCardCollapsed = collapsed;
            EnsureCollapsibleNoteWidgets();
            ApplyEmployeeCardFoldState();
        }

        public void ToggleEmployeeCardCollapsed()
        {
            SetEmployeeCardCollapsed(!isEmployeeCardCollapsed);
        }

        private void OnDestroy()
        {
            if (runtimeCreatedFoldedSprite != null)
            {
                Destroy(runtimeCreatedFoldedSprite);
                runtimeCreatedFoldedSprite = null;
            }
        }

        public void BindCounterMonitorTexts(Text monitorDayText, Text monitorTimerText, Text monitorBalanceText)
        {
            counterMonitorDayText = monitorDayText;
            counterMonitorTimerText = monitorTimerText;
            counterMonitorBalanceText = monitorBalanceText;
        }

        private void EnsureEmployeeCardBound()
        {
            if (stressFillImage != null && stressValueText != null &&
                fatigueFillImage != null && fatigueValueText != null &&
                loyaltyFillImage != null && loyaltyValueText != null)
            {
                return;
            }

            Transform root = hudRoot != null ? hudRoot.transform : transform;
            Transform cardContent = root.Find("EmployeeStatusCard/PostItContent");
            if (cardContent == null) return;

            if (stressFillImage == null)
            {
                Transform tr = cardContent.Find("스트레스_Track/스트레스_Fill");
                if (tr != null) stressFillImage = tr.GetComponent<Image>();
            }
            if (stressValueText == null)
            {
                Transform tr = cardContent.Find("스트레스_Value");
                if (tr != null) stressValueText = tr.GetComponent<Text>();
            }
            if (fatigueFillImage == null)
            {
                Transform tr = cardContent.Find("피로도_Track/피로도_Fill");
                if (tr != null) fatigueFillImage = tr.GetComponent<Image>();
            }
            if (fatigueValueText == null)
            {
                Transform tr = cardContent.Find("피로도_Value");
                if (tr != null) fatigueValueText = tr.GetComponent<Text>();
            }
            if (loyaltyFillImage == null)
            {
                Transform tr = cardContent.Find("충성도_Track/충성도_Fill");
                if (tr != null) loyaltyFillImage = tr.GetComponent<Image>();
            }
            if (loyaltyValueText == null)
            {
                Transform tr = cardContent.Find("충성도_Value");
                if (tr != null) loyaltyValueText = tr.GetComponent<Text>();
            }
        }

        private void EnsureCounterMonitorBound()
        {
            if (counterMonitorTimerText != null && counterMonitorBalanceText != null)
            {
                SyncCounterMonitorTimeCaption();
                return;
            }

            GameObject canvasObj = GameObject.Find("CounterMonitorScreenCanvas");
            if (canvasObj == null)
            {
                return;
            }

            Transform root = canvasObj.transform;
            if (counterMonitorDayText == null)
            {
                Transform dayTr = root.Find("MonitorScreenBg/MonitorHeaderBar/MonitorDayLabelText");
                if (dayTr != null) counterMonitorDayText = dayTr.GetComponent<Text>();
            }

            if (counterMonitorTimerText == null)
            {
                Transform timerTr = root.Find("MonitorScreenBg/MonitorTimeCard/MonitorTimerText");
                if (timerTr != null) counterMonitorTimerText = timerTr.GetComponent<Text>();
            }

            if (counterMonitorBalanceText == null)
            {
                Transform balTr = root.Find("MonitorScreenBg/MonitorRevenueCard/MonitorBalanceText");
                if (balTr != null) counterMonitorBalanceText = balTr.GetComponent<Text>();
            }

            SyncCounterMonitorTimeCaption();
        }

        private void SyncCounterMonitorTimeCaption()
        {
            if (counterMonitorTimerText == null) return;
            Transform parent = counterMonitorTimerText.transform.parent;
            if (parent == null) return;
            Transform captionTr = parent.Find("TimeCardCaption");
            if (captionTr != null)
            {
                Text captionText = captionTr.GetComponent<Text>();
                if (captionText != null && captionText.text != "매장 영업 시각")
                {
                    captionText.text = "매장 영업 시각";
                }
            }
        }

        public void UpdateTopBar(
            int dayNumber,
            float remainingSeconds,
            int displayedBalanceWon,
            int targetBalanceWon = 0,
            float totalDaySeconds = 0f,
            float? explicitGameHour = null,
            int todaySalesWon = -1,
            int todayUnitsSold = 0)
        {
            EnsureCounterMonitorBound();

            string dayLabel = $"{dayNumber}일차 영업 중";
            string clockLabel = StoreDayTimeRules.FormatMonitorClock(
                remainingSeconds,
                totalDaySeconds,
                explicitGameHour,
                minuteStep: 10);
            string balanceLabel = targetBalanceWon > 0
                ? $"현재 {displayedBalanceWon:N0}원  /  목표 {targetBalanceWon:N0}원"
                : $"예상 잔액  {displayedBalanceWon:N0}원";

            if (dayLabelText != null)
            {
                dayLabelText.text = dayLabel;
            }

            if (timerText != null)
            {
                timerText.text = clockLabel;
            }

            if (balanceText != null)
            {
                balanceText.text = balanceLabel;
            }

            if (counterMonitorDayText != null)
            {
                counterMonitorDayText.text = dayLabel;
            }

            if (counterMonitorTimerText != null)
            {
                counterMonitorTimerText.text = clockLabel;
            }

            if (counterMonitorBalanceText != null)
            {
                if (todaySalesWon >= 0)
                {
                    Transform revParent = counterMonitorBalanceText.transform.parent;
                    Transform revCapTr = revParent != null ? revParent.Find("RevenueCardCaption") : null;
                    Text revCapText = revCapTr != null ? revCapTr.GetComponent<Text>() : null;
                    if (revCapText != null)
                    {
                        revCapText.text = targetBalanceWon > 0
                            ? $"보유 자산 {displayedBalanceWon:N0}원  /  목표 자산 {targetBalanceWon:N0}원"
                            : $"예상 보유 자산 {displayedBalanceWon:N0}원";
                    }

                    counterMonitorBalanceText.text = $"오늘 매출 : {todaySalesWon:N0}원  ({todayUnitsSold}개 결제)";
                }
                else
                {
                    counterMonitorBalanceText.text = balanceLabel;
                }

                counterMonitorBalanceText.color = (targetBalanceWon > 0 && displayedBalanceWon >= targetBalanceWon)
                    ? new Color(0.28f, 0.98f, 0.68f, 1f)
                    : new Color(1f, 0.86f, 0.32f, 1f);
            }
        }

        public void RenderEmployee(EmployeeHudSnapshot snapshot)
        {
            EnsureEmployeeCardBound();

            if (employeeHeaderText != null)
            {
                employeeHeaderText.text = $"{snapshot.EmployeeName}  ·  성격: {snapshot.PersonalityLabel}";
            }

            if (currentRoleText != null)
            {
                currentRoleText.text = $"현재 업무: {FormatRoleLabel(snapshot.CurrentRole)} ({snapshot.ActionCodeLabel})";
            }

            RenderMoveBadge(snapshot.MoveBadge);

            StressBand band = StressRules.Classify(snapshot.Stress);
            string bandText = band == StressBand.Angry
                ? "분노·임계"
                : (band == StressBand.Complaining ? "피로·불만" : "안정");

            if (stressFillImage != null)
            {
                stressFillImage.fillAmount = snapshot.Stress / 100f;
                stressFillImage.color = band == StressBand.Angry
                    ? StressAngryColor
                    : (band == StressBand.Complaining ? StressComplainColor : StressStableColor);
            }

            if (stressValueText != null)
            {
                stressValueText.text = $"{Mathf.RoundToInt(snapshot.Stress)} ({bandText})";
            }

            if (fatigueFillImage != null)
            {
                fatigueFillImage.fillAmount = snapshot.Fatigue / 100f;
            }

            if (fatigueValueText != null)
            {
                fatigueValueText.text = $"{Mathf.RoundToInt(snapshot.Fatigue)}";
            }

            if (loyaltyFillImage != null)
            {
                loyaltyFillImage.fillAmount = snapshot.Loyalty / 100f;
            }

            if (loyaltyValueText != null)
            {
                loyaltyValueText.text = $"{Mathf.RoundToInt(snapshot.Loyalty)}";
            }
        }

        public void RenderVoiceStage(VoiceProcessStage stage, string recognizedTranscript)
        {
            currentVoiceStage = stage;
            processingAnimTimer = 0f;

            if (voiceStageBadgeBg != null && voiceStageText != null)
            {
                switch (stage)
                {
                    case VoiceProcessStage.Listening:
                        voiceStageBadgeBg.color = StageListeningColor;
                        voiceStageText.text = "● [R/V] 듣는 중...";
                        break;
                    case VoiceProcessStage.Processing:
                        voiceStageBadgeBg.color = StageProcessingColor;
                        voiceStageText.text = "음성 인식 및 AI 판단 중.";
                        break;
                    case VoiceProcessStage.Completed:
                        voiceStageBadgeBg.color = StageDoneColor;
                        voiceStageText.text = "✓ 명령 판정 완료";
                        break;
                    default:
                        voiceStageBadgeBg.color = StageIdleColor;
                        voiceStageText.text = "[R/V] 마이크 대기 중";
                        break;
                }
            }

            if (transcriptText != null && !string.IsNullOrWhiteSpace(recognizedTranscript))
            {
                transcriptText.text = $"인식 문장: \"{recognizedTranscript.Trim()}\"";
            }
            else if (transcriptText != null && stage == VoiceProcessStage.Listening)
            {
                transcriptText.text = "인식 문장: (말씀하시는 내용을 듣고 있습니다... [R]을 떼거나 [V]를 누르면 전송)";
            }
        }

        public void RenderFeedback(VoiceCommandFeedback feedback)
        {
            if (transcriptText != null && !string.IsNullOrWhiteSpace(feedback.RecognizedText))
            {
                transcriptText.text = $"인식 문장: \"{feedback.RecognizedText.Trim()}\"";
            }

            if (feedbackBannerBg == null || feedbackBannerText == null) return;

            string prefix;
            switch (feedback.Kind)
            {
                case CommandFeedbackKind.Accepted:
                    feedbackBannerBg.color = BannerAcceptedColor;
                    prefix = "[명령 수락]";
                    break;
                case CommandFeedbackKind.RecognitionFailed:
                    feedbackBannerBg.color = BannerRecognitionFailColor;
                    prefix = "[① 단순 인식 실패 / 미지원]";
                    break;
                case CommandFeedbackKind.RefusedByEmotion:
                    feedbackBannerBg.color = BannerEmotionRefusedColor;
                    prefix = "[② 감정 악화 · 거부/태만]";
                    break;
                case CommandFeedbackKind.SystemOrMoveError:
                    feedbackBannerBg.color = BannerSystemErrorColor;
                    prefix = "[③ 시스템 · 이동 오류]";
                    break;
                default:
                    feedbackBannerBg.color = BannerInfoColor;
                    prefix = "[안내]";
                    break;
            }

            string actionPart = string.IsNullOrWhiteSpace(feedback.ActionSummary)
                ? string.Empty
                : $" ({feedback.ActionSummary})";

            if (!string.IsNullOrWhiteSpace(feedback.EmployeeDialogue))
            {
                string trimmedDialogue = feedback.EmployeeDialogue.Trim();
                bool detailAlreadyHasDialogue =
                    !string.IsNullOrWhiteSpace(feedback.DetailMessage) &&
                    feedback.DetailMessage.IndexOf(trimmedDialogue, StringComparison.Ordinal) >= 0;

                feedbackBannerText.text = detailAlreadyHasDialogue
                    ? $"{prefix}{actionPart}  {feedback.DetailMessage}"
                    : $"{prefix}{actionPart}  알바: \"{trimmedDialogue}\"  —  {feedback.DetailMessage}";
            }
            else
            {
                feedbackBannerText.text = $"{prefix}{actionPart}  {feedback.DetailMessage}";
            }
        }

        private void RenderMoveBadge(StaffMoveBadge badge)
        {
            if (moveBadgeText == null || moveBadgeBackground == null) return;

            switch (badge)
            {
                case StaffMoveBadge.Moving:
                    moveBadgeText.text = "이동 중";
                    moveBadgeBackground.color = new Color(0.18f, 0.50f, 0.86f, 1f);
                    break;
                case StaffMoveBadge.Arrived:
                    moveBadgeText.text = "목적지 도착";
                    moveBadgeBackground.color = new Color(0.16f, 0.66f, 0.45f, 1f);
                    break;
                case StaffMoveBadge.Failed:
                    moveBadgeText.text = "이동 실패";
                    moveBadgeBackground.color = new Color(0.85f, 0.24f, 0.24f, 1f);
                    break;
                case StaffMoveBadge.Cancelled:
                    moveBadgeText.text = "이동 취소";
                    moveBadgeBackground.color = new Color(0.55f, 0.45f, 0.22f, 1f);
                    break;
                default:
                    moveBadgeText.text = "위치 대기";
                    moveBadgeBackground.color = new Color(0.30f, 0.35f, 0.42f, 1f);
                    break;
            }
        }

        private static string FormatRoleLabel(StaffWorkRole role)
        {
            switch (role)
            {
                case StaffWorkRole.Checkout:
                    return "계산";
                case StaffWorkRole.Restock:
                    return "진열";
                case StaffWorkRole.Other:
                    return "기타 행동";
                default:
                    return "기본 대기";
            }
        }

        private void HandleOpenOverlayClicked()
        {
            OpenCommandOverlayClicked?.Invoke();
        }

        private void HandleFoldToggleButtonClicked()
        {
            if (EmployeeCardFoldToggleClicked != null)
            {
                EmployeeCardFoldToggleClicked.Invoke();
            }
            else
            {
                ToggleEmployeeCardCollapsed();
            }
        }

        private void EnsureCollapsibleNoteWidgets()
        {
            Transform root = hudRoot != null ? hudRoot.transform : transform;
            if (employeeStatusCardRoot == null)
            {
                Transform cardTr = root.Find("EmployeeStatusCard");
                if (cardTr != null)
                {
                    employeeStatusCardRoot = cardTr.gameObject;
                }
            }

            if (employeeStatusCardRoot == null)
            {
                return;
            }

            Font uiFont = ResolveUiFont();
            Transform postItContent = employeeStatusCardRoot.transform.Find("PostItContent");

            // 1. 펼친 상태(포스트잇 최하단 · 충성도 숫자 바로 아래) '접기(G)' 텍스트
            if (expandedCollapseHintRoot == null)
            {
                Transform hintParent = postItContent != null ? postItContent : employeeStatusCardRoot.transform;
                Transform existingHint = hintParent.Find("EmployeeCardCollapseHint");
                if (existingHint != null)
                {
                    expandedCollapseHintRoot = existingHint.gameObject;
                }
                else
                {
                    expandedCollapseHintRoot = new GameObject("EmployeeCardCollapseHint", typeof(RectTransform), typeof(Text), typeof(Button));
                    expandedCollapseHintRoot.transform.SetParent(hintParent, false);

                    RectTransform hintRt = expandedCollapseHintRoot.GetComponent<RectTransform>();
                    hintRt.anchorMin = new Vector2(1f, 0f);
                    hintRt.anchorMax = new Vector2(1f, 0f);
                    hintRt.pivot = new Vector2(1f, 0f);
                    hintRt.anchoredPosition = postItContent != null ? new Vector2(0f, 12f) : new Vector2(-24f, 14f);
                    hintRt.sizeDelta = postItContent != null ? new Vector2(240f, 54f) : new Vector2(120f, 28f);

                    Text hintText = expandedCollapseHintRoot.GetComponent<Text>();
                    hintText.font = uiFont;
                    hintText.fontSize = postItContent != null ? 34 : 17;
                    hintText.fontStyle = FontStyle.Bold;
                    hintText.color = new Color(0.24f, 0.20f, 0.16f, 0.88f);
                    hintText.alignment = TextAnchor.MiddleRight;
                    hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    hintText.verticalOverflow = VerticalWrapMode.Overflow;
                    hintText.raycastTarget = true;
                    hintText.text = "접기(G)";

                    Button hintBtn = expandedCollapseHintRoot.GetComponent<Button>();
                    hintBtn.targetGraphic = hintText;
                    hintBtn.onClick.AddListener(HandleFoldToggleButtonClicked);
                }
            }

            // 2. 접은 상태(우상단 쪽지 아이콘 + 박스 없는 흰색 글씨·검은 테두리 '펼치기(G)')
            if (collapsedNoteRoot == null)
            {
                Transform existingCollapsed = root.Find("CollapsedEmployeeNoteWidget");
                if (existingCollapsed != null)
                {
                    collapsedNoteRoot = existingCollapsed.gameObject;
                }
                else
                {
                    collapsedNoteRoot = new GameObject("CollapsedEmployeeNoteWidget", typeof(RectTransform));
                    collapsedNoteRoot.transform.SetParent(root, false);

                    RectTransform containerRt = collapsedNoteRoot.GetComponent<RectTransform>();
                    containerRt.anchorMin = new Vector2(1f, 1f);
                    containerRt.anchorMax = new Vector2(1f, 1f);
                    containerRt.pivot = new Vector2(1f, 1f);
                    containerRt.anchoredPosition = new Vector2(-28f, -18f);
                    containerRt.sizeDelta = new Vector2(124f, 142f);

                    // 접힌 쪽지 이미지 버튼
                    GameObject iconObj = new GameObject("FoldedNoteIcon", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
                    iconObj.transform.SetParent(collapsedNoteRoot.transform, false);
                    RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.5f, 1f);
                    iconRt.anchorMax = new Vector2(0.5f, 1f);
                    iconRt.pivot = new Vector2(0.5f, 1f);
                    iconRt.anchoredPosition = new Vector2(0f, 0f);
                    iconRt.sizeDelta = new Vector2(106f, 106f);

                    Image iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = ResolveFoldedNoteSprite();
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                    iconImg.raycastTarget = true;

                    Shadow iconShadow = iconObj.GetComponent<Shadow>();
                    iconShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
                    iconShadow.effectDistance = new Vector2(3f, -4f);

                    Button iconBtn = iconObj.GetComponent<Button>();
                    iconBtn.targetGraphic = iconImg;
                    iconBtn.onClick.AddListener(HandleFoldToggleButtonClicked);

                    // 쪽지 아래 '펼치기(G)' (배경 박스 없이 2x 슈퍼샘플링 흰색 글씨 + 검은색 테두리)
                    GameObject expandObj = new GameObject("ExpandHintLabel", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow), typeof(Button));
                    expandObj.transform.SetParent(collapsedNoteRoot.transform, false);

                    RectTransform expandRt = expandObj.GetComponent<RectTransform>();
                    expandRt.anchorMin = new Vector2(0.5f, 0f);
                    expandRt.anchorMax = new Vector2(0.5f, 0f);
                    expandRt.pivot = new Vector2(0.5f, 0f);
                    expandRt.anchoredPosition = new Vector2(0f, 2f);
                    expandRt.sizeDelta = new Vector2(260f, 56f);
                    expandRt.localScale = new Vector3(0.5f, 0.5f, 1f);

                    Text expandText = expandObj.GetComponent<Text>();
                    expandText.font = uiFont;
                    expandText.fontSize = 36;
                    expandText.fontStyle = FontStyle.Bold;
                    expandText.color = Color.white;
                    expandText.alignment = TextAnchor.MiddleCenter;
                    expandText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    expandText.verticalOverflow = VerticalWrapMode.Overflow;
                    expandText.raycastTarget = true;
                    expandText.text = "펼치기(G)";

                    Outline expandOutline = expandObj.GetComponent<Outline>();
                    expandOutline.effectColor = new Color(0f, 0f, 0f, 0.98f);
                    expandOutline.effectDistance = new Vector2(2.6f, -2.6f);

                    Shadow expandShadow = expandObj.GetComponent<Shadow>();
                    expandShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                    expandShadow.effectDistance = new Vector2(0f, -3f);

                    Button expandBtn = expandObj.GetComponent<Button>();
                    expandBtn.targetGraphic = expandText;
                    expandBtn.onClick.AddListener(HandleFoldToggleButtonClicked);
                }
            }
        }

        private void ApplyEmployeeCardFoldState()
        {
            if (employeeStatusCardRoot != null && employeeStatusCardRoot.activeSelf == isEmployeeCardCollapsed)
            {
                employeeStatusCardRoot.SetActive(!isEmployeeCardCollapsed);
            }

            if (expandedCollapseHintRoot != null && expandedCollapseHintRoot.activeSelf == isEmployeeCardCollapsed)
            {
                expandedCollapseHintRoot.SetActive(!isEmployeeCardCollapsed);
            }

            if (collapsedNoteRoot != null && collapsedNoteRoot.activeSelf != isEmployeeCardCollapsed)
            {
                collapsedNoteRoot.SetActive(isEmployeeCardCollapsed);
            }
        }

        private Sprite ResolveFoldedNoteSprite()
        {
            if (foldedNoteSprite != null)
            {
                return foldedNoteSprite;
            }

            Sprite loadedSprite = Resources.Load<Sprite>(FoldedNoteResourcePath);
            if (loadedSprite != null)
            {
                foldedNoteSprite = loadedSprite;
                return foldedNoteSprite;
            }

            if (runtimeCreatedFoldedSprite != null)
            {
                return runtimeCreatedFoldedSprite;
            }

            Texture2D tex = Resources.Load<Texture2D>(FoldedNoteResourcePath);
            if (tex != null)
            {
                runtimeCreatedFoldedSprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                return runtimeCreatedFoldedSprite;
            }

            return null;
        }

        private Font ResolveUiFont()
        {
            if (employeeHeaderText != null && employeeHeaderText.font != null)
            {
                return employeeHeaderText.font;
            }

            if (feedbackBannerText != null && feedbackBannerText.font != null)
            {
                return feedbackBannerText.font;
            }

            Text anyText = GetComponentInChildren<Text>(true);
            if (anyText != null && anyText.font != null)
            {
                return anyText.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
