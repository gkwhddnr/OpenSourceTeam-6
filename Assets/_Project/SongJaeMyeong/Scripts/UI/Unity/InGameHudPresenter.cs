using System;
using OpenSourceTeam6.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSourceTeam6.UI.Unity
{
    public sealed class InGameHudPresenter : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject hudRoot;

        [Header("Top Store Bar")]
        [SerializeField] private Text dayLabelText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text balanceText;
        [SerializeField] private Button openCommandOverlayButton;

        [Header("Employee Status Card")]
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

        private VoiceProcessStage currentVoiceStage = VoiceProcessStage.Idle;
        private float processingAnimTimer;

        public event Action OpenCommandOverlayClicked;

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
        }

        public void UpdateTopBar(int dayNumber, float remainingSeconds, int displayedBalanceWon, int targetBalanceWon = 0)
        {
            if (dayLabelText != null)
            {
                dayLabelText.text = $"{dayNumber}일차 영업 중";
            }

            if (timerText != null)
            {
                int clampedSec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
                int mins = clampedSec / 60;
                int secs = clampedSec % 60;
                timerText.text = $"남은 시간  {mins:00}:{secs:00}";
            }

            if (balanceText != null)
            {
                if (targetBalanceWon > 0)
                {
                    balanceText.text = $"현재 {displayedBalanceWon:N0}원  /  목표 {targetBalanceWon:N0}원";
                }
                else
                {
                    balanceText.text = $"예상 잔액  {displayedBalanceWon:N0}원";
                }
            }
        }

        public void RenderEmployee(EmployeeHudSnapshot snapshot)
        {
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
                        voiceStageText.text = "● [V] 듣는 중 (종료: V)...";
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
                        voiceStageText.text = "[V] 마이크 대기 중";
                        break;
                }
            }

            if (transcriptText != null && !string.IsNullOrWhiteSpace(recognizedTranscript))
            {
                transcriptText.text = $"인식 문장: \"{recognizedTranscript.Trim()}\"";
            }
            else if (transcriptText != null && stage == VoiceProcessStage.Listening)
            {
                transcriptText.text = "인식 문장: (말씀하시는 내용을 듣고 있습니다... 다시 [V] 키를 누르면 전송)";
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
                feedbackBannerText.text = $"{prefix}{actionPart}  알바: \"{feedback.EmployeeDialogue.Trim()}\"  —  {feedback.DetailMessage}";
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
    }
}
