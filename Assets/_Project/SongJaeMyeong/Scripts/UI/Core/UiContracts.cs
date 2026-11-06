using System;
using System.Collections.Generic;

namespace OpenSourceTeam6.UI.Core
{
    public enum UiScreenMode
    {
        Lobby = 0,
        InGame = 1,
        Settlement = 2
    }

    public enum VoiceProcessStage
    {
        Idle = 0,
        Listening = 1,
        Processing = 2,
        Completed = 3
    }

    public enum CommandFeedbackKind
    {
        Info = 0,
        Accepted = 1,
        RecognitionFailed = 2,
        RefusedByEmotion = 3,
        SystemOrMoveError = 4
    }

    public enum StaffWorkRole
    {
        Idle = 0,
        Checkout = 1,
        Restock = 2,
        Other = 3
    }

    public enum StaffMoveBadge
    {
        Idle = 0,
        Moving = 1,
        Arrived = 2,
        Failed = 3,
        Cancelled = 4
    }

    public enum StressBand
    {
        Stable = 0,
        Complaining = 1,
        Angry = 2
    }

    public enum DayOutcomeGrade
    {
        StableSuccess = 0,
        UnstableAngerSuccess = 1,
        FailedTargetShortfall = 2
    }

    /// <summary>
    /// 1차 통합 데모 규격서(2.1절) 기준 편의점 공통 명령 의도.
    /// </summary>
    public enum StoreCommandIntent
    {
        Checkout = 0,
        Restock = 1,
        Rest = 2,
        Clarify = 3,
        Unsupported = 4
    }

    /// <summary>
    /// 1차 통합 데모 규격서(2.2절) 기준 알바 AI 반응 판정 코드.
    /// (세부 실수 연출 MISTAKE_DROP, MISTAKE_CHECKOUT은 2차 개발 단계에서 확장)
    /// </summary>
    public enum EmployeeReaction
    {
        AcceptNormal = 0,
        AcceptComplain = 1,
        SlackOff = 2,
        RefuseEmotion = 3
    }

    public readonly struct EmployeePersonalityProfile
    {
        public float Diligence { get; }
        public float Laziness { get; }
        public float Loyalty { get; }

        public EmployeePersonalityProfile(float diligence, float laziness, float loyalty)
        {
            Diligence = StressRules.ClampStat(diligence);
            Laziness = StressRules.ClampStat(laziness);
            Loyalty = StressRules.ClampStat(loyalty);
        }

        public static EmployeePersonalityProfile CreateDefault()
        {
            return new EmployeePersonalityProfile(diligence: 70f, laziness: 20f, loyalty: 70f);
        }
    }

    public readonly struct EmotionReactionWeights
    {
        public float AcceptNormal { get; }
        public float AcceptComplain { get; }
        public float SlackOff { get; }
        public float RefuseEmotion { get; }
        public float Total => AcceptNormal + AcceptComplain + SlackOff + RefuseEmotion;

        public EmotionReactionWeights(float acceptNormal, float acceptComplain, float slackOff, float refuseEmotion)
        {
            AcceptNormal = Math.Max(0f, acceptNormal);
            AcceptComplain = Math.Max(0f, acceptComplain);
            SlackOff = Math.Max(0f, slackOff);
            RefuseEmotion = Math.Max(0f, refuseEmotion);
        }
    }

    [Serializable]
    public struct EmotionStatRates
    {
        public float InitialStress;
        public float InitialFatigue;
        public float InitialLoyalty;
        public float WorkStressPerSecond;
        public float WorkFatiguePerSecond;
        public float RestStressRecoveryPerSecond;
        public float RestFatigueRecoveryPerSecond;
        public float TaskSwitchStressPenalty;
        public float RefusalLoyaltyPenalty;

        /// <summary>
        /// 1차 통합 데모 규격서(4.1절) 120초 시연 기준 감정 변화 밸런스.
        /// </summary>
        public static EmotionStatRates CreateDemoDefault()
        {
            return new EmotionStatRates
            {
                InitialStress = 25f,
                InitialFatigue = 15f,
                InitialLoyalty = 70f,
                WorkStressPerSecond = 1.2f,
                WorkFatiguePerSecond = 0.8f,
                RestStressRecoveryPerSecond = 1.8f,
                RestFatigueRecoveryPerSecond = 1.0f,
                TaskSwitchStressPenalty = 12f,
                RefusalLoyaltyPenalty = 5f
            };
        }
    }

    public readonly struct EmployeeHudSnapshot
    {
        public string EmployeeName { get; }
        public string PersonalityLabel { get; }
        public StaffWorkRole CurrentRole { get; }
        public string ActionCodeLabel { get; }
        public StaffMoveBadge MoveBadge { get; }
        public float Stress { get; }
        public float Fatigue { get; }
        public float Loyalty { get; }

        public EmployeeHudSnapshot(
            string employeeName,
            string personalityLabel,
            StaffWorkRole currentRole,
            string actionCodeLabel,
            StaffMoveBadge moveBadge,
            float stress,
            float fatigue,
            float loyalty)
        {
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? "직원" : employeeName.Trim();
            PersonalityLabel = string.IsNullOrWhiteSpace(personalityLabel) ? "표준형" : personalityLabel.Trim();
            CurrentRole = currentRole;
            ActionCodeLabel = string.IsNullOrWhiteSpace(actionCodeLabel) ? "IDLE" : actionCodeLabel.Trim();
            MoveBadge = moveBadge;
            Stress = StressRules.ClampStat(stress);
            Fatigue = StressRules.ClampStat(fatigue);
            Loyalty = StressRules.ClampStat(loyalty);
        }
    }

    public readonly struct VoiceCommandFeedback
    {
        public CommandFeedbackKind Kind { get; }
        public string RecognizedText { get; }
        public string ActionSummary { get; }
        public string DetailMessage { get; }
        public string EmployeeDialogue { get; }

        public VoiceCommandFeedback(
            CommandFeedbackKind kind,
            string recognizedText,
            string actionSummary,
            string detailMessage,
            string employeeDialogue = "")
        {
            Kind = kind;
            RecognizedText = recognizedText ?? string.Empty;
            ActionSummary = actionSummary ?? string.Empty;
            DetailMessage = detailMessage ?? string.Empty;
            EmployeeDialogue = employeeDialogue ?? string.Empty;
        }
    }

    public interface ICursorLockGate
    {
        IDisposable AcquireLock(string reason);
        bool IsLocked { get; }
    }

    public interface IUISystemPresenter
    {
        UiScreenMode CurrentMode { get; }
        bool IsCommandOverlayOpen { get; }

        event Action<string> TextCommandSubmitted;
        event Action CancelWorkRequested;
        event Action VoiceRecordToggleRequested;
        event Action DayStarted;

        void StartDay(int dayNumber, int startingBalanceWon, float durationSeconds);
        void UpdateEmployeeHud(EmployeeHudSnapshot snapshot);
        void PushVoiceStage(VoiceProcessStage stage, string recognizedTranscript);
        void PushCommandFeedback(VoiceCommandFeedback feedback);
        void RecordWorkSample(StaffWorkRole role, float stress, float deltaSeconds);
        void RecordAngerRefusal();
        DaySettlementResult CompleteDayAndShowSettlement(float endingStress);
    }

    public static class StressRules
    {
        public const float ComplainingThreshold = 40f;
        public const float AngerThreshold = 80f;

        public const float StableEfficiency = 1.0f;
        public const float ComplainingEfficiency = 0.8f;
        public const float AngryEfficiency = 0.5f;

        public static float ClampStat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "상태 수치는 유한한 값이어야 합니다.");
            }

            if (value < 0f) return 0f;
            if (value > 100f) return 100f;
            return value;
        }

        public static StressBand Classify(float stress)
        {
            float clamped = ClampStat(stress);
            if (clamped >= AngerThreshold) return StressBand.Angry;
            if (clamped >= ComplainingThreshold) return StressBand.Complaining;
            return StressBand.Stable;
        }

        public static float GetEfficiencyMultiplier(float stress)
        {
            StressBand band = Classify(stress);
            switch (band)
            {
                case StressBand.Angry:
                    return AngryEfficiency;
                case StressBand.Complaining:
                    return ComplainingEfficiency;
                default:
                    return StableEfficiency;
            }
        }
    }

    /// <summary>
    /// 기능정의서 SYS-03 / SYS-06 및 1차 통합 데모 규격서(3장) 기준:
    /// 스트레스 구간 및 캐릭터 성격·피로도에 따른 반응 가중치 계산과 대사 스크립트 선택.
    /// </summary>
    public static class EmotionReactionRules
    {
        public const float LlmProposalMultiplier = 1.25f;

        public static EmotionReactionWeights ComputeWeights(
            StoreCommandIntent intent,
            float stress,
            float fatigue,
            EmployeePersonalityProfile personality,
            EmployeeReaction? llmProposal = null)
        {
            float s = StressRules.ClampStat(stress);
            float f = StressRules.ClampStat(fatigue);

            // 휴식(REST) 지시는 분노 상태여도 항상 즉시 수락하여 스트레스 해소 경로를 보장한다.
            if (intent == StoreCommandIntent.Rest)
            {
                return new EmotionReactionWeights(100f, 0f, 0f, 0f);
            }

            StressBand band = StressRules.Classify(s);
            float normal;
            float complain;
            float slack;
            float refuse;

            switch (band)
            {
                case StressBand.Stable:
                    // 기본 비율: 정상 88% / 불만 10% / 태만 2% / 거부 0%
                    normal = 88f + (personality.Diligence - 50f) * 0.15f;
                    complain = 10f;
                    slack = Math.Max(0.5f, 2f + (personality.Laziness - 20f) * 0.08f + f * 0.03f);
                    refuse = 0f;
                    break;

                case StressBand.Complaining:
                    // 기본 비율: 정상 30% / 불만 50% / 태만 12% / 거부 8%
                    normal = Math.Max(5f, 30f + (personality.Diligence - 50f) * 0.18f);
                    complain = 50f;
                    slack = Math.Max(2f, 12f + (personality.Laziness - 20f) * 0.15f + f * 0.08f);
                    refuse = Math.Max(1f, 8f + (70f - personality.Loyalty) * 0.12f);
                    break;

                default:
                    // 분노·임계(80~100) 기본 비율: 정상 5% / 불만 15% / 태만 25% / 거부 55%
                    normal = Math.Max(1f, 5f + (personality.Diligence - 70f) * 0.08f);
                    complain = 15f;
                    slack = Math.Max(5f, 25f + (personality.Laziness - 20f) * 0.15f + f * 0.10f);
                    refuse = Math.Max(15f, 55f + (70f - personality.Loyalty) * 0.20f + (s - 80f) * 0.4f);
                    break;
            }

            if (llmProposal.HasValue)
            {
                switch (llmProposal.Value)
                {
                    case EmployeeReaction.AcceptNormal:
                        normal *= LlmProposalMultiplier;
                        break;
                    case EmployeeReaction.AcceptComplain:
                        complain *= LlmProposalMultiplier;
                        break;
                    case EmployeeReaction.SlackOff:
                        slack *= LlmProposalMultiplier;
                        break;
                    case EmployeeReaction.RefuseEmotion:
                        if (band != StressBand.Stable) refuse *= LlmProposalMultiplier;
                        break;
                }
            }

            return new EmotionReactionWeights(normal, complain, slack, refuse);
        }

        public static EmployeeReaction SampleReaction(EmotionReactionWeights weights, float normalizedSample01)
        {
            if (float.IsNaN(normalizedSample01) || float.IsInfinity(normalizedSample01) ||
                normalizedSample01 < 0f || normalizedSample01 >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedSample01), "확률 샘플은 [0, 1) 범위여야 합니다.");
            }

            float total = weights.Total;
            if (total <= 0f) return EmployeeReaction.AcceptNormal;

            float cursor = normalizedSample01 * total;
            if ((cursor -= weights.AcceptNormal) < 0f) return EmployeeReaction.AcceptNormal;
            if ((cursor -= weights.AcceptComplain) < 0f) return EmployeeReaction.AcceptComplain;
            if ((cursor -= weights.SlackOff) < 0f) return EmployeeReaction.SlackOff;
            return EmployeeReaction.RefuseEmotion;
        }

        public static StoreCommandIntent ParseKoreanIntent(string utterance)
        {
            if (string.IsNullOrWhiteSpace(utterance)) return StoreCommandIntent.Clarify;
            string text = utterance.Trim();

            if (text.Contains("쉬어") || text.Contains("휴식") || text.Contains("대기") ||
                text.Contains("취소") || text.Contains("그만"))
            {
                return StoreCommandIntent.Rest;
            }

            bool hasCheckout = text.Contains("계산") || text.Contains("결제") || text.Contains("카운터") || text.Contains("손님");
            bool hasRestock = text.Contains("진열") || text.Contains("물건") || text.Contains("채워") ||
                              text.Contains("재고") || text.Contains("정리");

            if (hasCheckout && hasRestock) return StoreCommandIntent.Clarify;
            if (hasCheckout) return StoreCommandIntent.Checkout;
            if (hasRestock) return StoreCommandIntent.Restock;

            if (text.Contains("요리") || text.Contains("배달") || text.Contains("테이블") || text.Contains("서빙"))
            {
                return StoreCommandIntent.Unsupported;
            }

            return StoreCommandIntent.Clarify;
        }

        public static string GetReactionDialogue(StoreCommandIntent intent, EmployeeReaction reaction)
        {
            if (intent == StoreCommandIntent.Rest)
            {
                return "휴, 살았다... 잠깐만 숨 좀 돌릴게요.";
            }

            switch (reaction)
            {
                case EmployeeReaction.AcceptNormal:
                    return intent == StoreCommandIntent.Checkout
                        ? "네! 바로 계산대 보겠습니다."
                        : "알겠습니다, 진열대 채워 둘게요.";

                case EmployeeReaction.AcceptComplain:
                    return intent == StoreCommandIntent.Checkout
                        ? "하... 조금 피곤한데 일단 계산대 가볼게요."
                        : "방금도 일했는데... 알겠어요, 진열 한다고요.";

                case EmployeeReaction.SlackOff:
                    return "아 몰라요, 다리 아파서 그냥 잠깐 쉴래요...";

                case EmployeeReaction.RefuseEmotion:
                    return "지금 너무 힘들어서 못 하겠어요! 시키지 마세요!";

                default:
                    return "네, 확인했습니다.";
            }
        }
    }

    public sealed class NestedLockCounter
    {
        private readonly HashSet<long> activeTokens = new HashSet<long>();
        private long nextTokenId = 1;

        public bool IsLocked => activeTokens.Count > 0;
        public int ActiveCount => activeTokens.Count;

        public event Action<bool> LockStateChanged;

        public IDisposable Acquire(string reason)
        {
            bool wasLocked = IsLocked;
            long id = nextTokenId++;
            activeTokens.Add(id);

            if (!wasLocked && IsLocked)
            {
                LockStateChanged?.Invoke(true);
            }

            return new ReleaseToken(this, id, reason ?? "UI");
        }

        public void ReleaseAll()
        {
            if (activeTokens.Count == 0) return;
            activeTokens.Clear();
            LockStateChanged?.Invoke(false);
        }

        private void ReleaseById(long id)
        {
            if (!activeTokens.Remove(id)) return;
            if (!IsLocked)
            {
                LockStateChanged?.Invoke(false);
            }
        }

        private sealed class ReleaseToken : IDisposable
        {
            private NestedLockCounter owner;
            private readonly long tokenId;
            public string Reason { get; }

            public ReleaseToken(NestedLockCounter owner, long tokenId, string reason)
            {
                this.owner = owner;
                this.tokenId = tokenId;
                Reason = reason;
            }

            public void Dispose()
            {
                NestedLockCounter target = owner;
                if (target == null) return;
                owner = null;
                target.ReleaseById(tokenId);
            }
        }
    }
}
