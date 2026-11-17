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
    /// 1차 통합 데모 규격서(2.2절) 및 페르소나 행동 확률표 기준 알바 AI 반응 판정 코드.
    /// </summary>
    public enum EmployeeReaction
    {
        AcceptNormal = 0,
        AcceptComplain = 1,
        SlackOff = 2,
        RefuseEmotion = 3,
        Mistake = 4
    }

    /// <summary>
    /// 0일차 알바 채용 이력서 대표 페르소나 아키타입(프리셋 1~3 및 랜덤 추가 2종).
    /// </summary>
    public enum PersonaArchetype
    {
        DiligentStandard = 0,
        LazyComplainer = 1,
        FragileTimid = 2,
        LiteralRigid = 3,
        PassionateHasty = 4
    }

    /// <summary>
    /// 플레이어(점주) 음성/텍스트 지시의 발화 태도 분류.
    /// </summary>
    public enum UtteranceTone
    {
        Neutral = 0,
        PoliteOrPraise = 1,
        HarshOrNagging = 2
    }

    /// <summary>
    /// 일일 마감 정산(8번 알바 상태 정산 화면)에서의 시급 조정 선택지.
    /// </summary>
    public enum WageAdjustmentOption
    {
        Freeze = 0,
        Raise = 1,
        Cut = 2
    }

    public readonly struct EmployeePersonalityProfile
    {
        public PersonaArchetype Archetype { get; }
        public float Diligence { get; }
        public float Laziness { get; }
        public float Loyalty { get; }
        public float InitialStress { get; }
        public float WorkStressMultiplier { get; }
        public float RestRecoveryMultiplier { get; }
        public float TaskSwitchMultiplier { get; }
        public float HarshToneMultiplier { get; }
        public float PraiseRecoveryMultiplier { get; }
        public float AmbiguousCommandPenalty { get; }
        public float WageSensitivityMultiplier { get; }
        public float MoveSpeedMultiplier { get; }

        public EmployeePersonalityProfile(
            float diligence,
            float laziness,
            float loyalty,
            PersonaArchetype archetype = PersonaArchetype.DiligentStandard,
            float initialStress = 25f,
            float workStressMultiplier = 1.0f,
            float restRecoveryMultiplier = 1.0f,
            float taskSwitchMultiplier = 1.0f,
            float harshToneMultiplier = 1.0f,
            float praiseRecoveryMultiplier = 1.0f,
            float ambiguousCommandPenalty = 0f,
            float wageSensitivityMultiplier = 1.0f,
            float moveSpeedMultiplier = 1.0f)
        {
            Archetype = archetype;
            Diligence = StressRules.ClampStat(diligence);
            Laziness = StressRules.ClampStat(laziness);
            Loyalty = StressRules.ClampStat(loyalty);
            InitialStress = StressRules.ClampStat(initialStress);
            WorkStressMultiplier = Math.Max(0.1f, workStressMultiplier);
            RestRecoveryMultiplier = Math.Max(0.1f, restRecoveryMultiplier);
            TaskSwitchMultiplier = Math.Max(0.1f, taskSwitchMultiplier);
            HarshToneMultiplier = Math.Max(0.1f, harshToneMultiplier);
            PraiseRecoveryMultiplier = Math.Max(0.1f, praiseRecoveryMultiplier);
            AmbiguousCommandPenalty = Math.Max(0f, ambiguousCommandPenalty);
            WageSensitivityMultiplier = Math.Max(0.1f, wageSensitivityMultiplier);
            MoveSpeedMultiplier = Math.Max(0.5f, moveSpeedMultiplier);
        }

        public static EmployeePersonalityProfile CreateDefault()
        {
            return new EmployeePersonalityProfile(diligence: 70f, laziness: 20f, loyalty: 70f);
        }

        public static EmployeePersonalityProfile CreatePreset1Diligent()
        {
            return new EmployeePersonalityProfile(
                diligence: 80f,
                laziness: 15f,
                loyalty: 75f,
                archetype: PersonaArchetype.DiligentStandard,
                initialStress: 20f,
                workStressMultiplier: 0.85f,
                restRecoveryMultiplier: 1.0f,
                taskSwitchMultiplier: 1.6f,
                harshToneMultiplier: 1.0f,
                praiseRecoveryMultiplier: 1.0f,
                ambiguousCommandPenalty: 0f,
                wageSensitivityMultiplier: 1.0f,
                moveSpeedMultiplier: 1.0f);
        }

        public static EmployeePersonalityProfile CreatePreset2Lazy()
        {
            return new EmployeePersonalityProfile(
                diligence: 40f,
                laziness: 75f,
                loyalty: 55f,
                archetype: PersonaArchetype.LazyComplainer,
                initialStress: 30f,
                workStressMultiplier: 1.35f,
                restRecoveryMultiplier: 1.5f,
                taskSwitchMultiplier: 1.1f,
                harshToneMultiplier: 1.1f,
                praiseRecoveryMultiplier: 1.0f,
                ambiguousCommandPenalty: 0f,
                wageSensitivityMultiplier: 1.5f,
                moveSpeedMultiplier: 0.9f);
        }

        public static EmployeePersonalityProfile CreatePreset3Timid()
        {
            return new EmployeePersonalityProfile(
                diligence: 65f,
                laziness: 20f,
                loyalty: 65f,
                archetype: PersonaArchetype.FragileTimid,
                initialStress: 25f,
                workStressMultiplier: 1.10f,
                restRecoveryMultiplier: 1.0f,
                taskSwitchMultiplier: 1.2f,
                harshToneMultiplier: 1.7f,
                praiseRecoveryMultiplier: 1.6f,
                ambiguousCommandPenalty: 4f,
                wageSensitivityMultiplier: 1.1f,
                moveSpeedMultiplier: 1.0f);
        }

        public static EmployeePersonalityProfile CreateRandom4Rigid()
        {
            return new EmployeePersonalityProfile(
                diligence: 70f,
                laziness: 15f,
                loyalty: 70f,
                archetype: PersonaArchetype.LiteralRigid,
                initialStress: 20f,
                workStressMultiplier: 0.80f,
                restRecoveryMultiplier: 0.9f,
                taskSwitchMultiplier: 1.3f,
                harshToneMultiplier: 1.0f,
                praiseRecoveryMultiplier: 0.9f,
                ambiguousCommandPenalty: 12f,
                wageSensitivityMultiplier: 1.0f,
                moveSpeedMultiplier: 1.0f);
        }

        public static EmployeePersonalityProfile CreateRandom5Passionate()
        {
            return new EmployeePersonalityProfile(
                diligence: 85f,
                laziness: 10f,
                loyalty: 80f,
                archetype: PersonaArchetype.PassionateHasty,
                initialStress: 15f,
                workStressMultiplier: 1.00f,
                restRecoveryMultiplier: 1.2f,
                taskSwitchMultiplier: 0.9f,
                harshToneMultiplier: 1.1f,
                praiseRecoveryMultiplier: 1.4f,
                ambiguousCommandPenalty: 0f,
                wageSensitivityMultiplier: 1.0f,
                moveSpeedMultiplier: 1.15f);
        }
    }

    /// <summary>
    /// 0일차 알바 채용 이력서 화면에서 설정·확정된 알바 페르소나 데이터.
    /// </summary>
    public readonly struct EmployeePersonaSetupData
    {
        public string EmployeeName { get; }
        public string PersonalityTraits { get; }
        public int HourlyWageWon { get; }
        public EmployeePersonalityProfile Profile { get; }

        public EmployeePersonaSetupData(
            string employeeName,
            string personalityTraits,
            int hourlyWageWon = EmployeePersonaPresets.DefaultHourlyWageWon,
            EmployeePersonalityProfile? profile = null)
        {
            EmployeeName = EmployeePersonaPresets.SanitizeName(employeeName);
            PersonalityTraits = EmployeePersonaPresets.SanitizeTraits(personalityTraits);
            HourlyWageWon = hourlyWageWon > 0 ? hourlyWageWon : EmployeePersonaPresets.DefaultHourlyWageWon;
            Profile = profile ?? EmployeePersonaPresets.InferProfileFromTraits(PersonalityTraits);
        }
    }

    /// <summary>
    /// 0일차 알바 채용 이력서용 기본 프리셋(1·2·3번), 랜덤 생성 및 50자 자유 입력 키워드 매핑 규칙.
    /// </summary>
    public static class EmployeePersonaPresets
    {
        public const int MaxNameLength = 12;
        public const int MaxTraitLength = 50;
        public const int DefaultHourlyWageWon = 10030;

        public const string Preset1Name = "김민수";
        public const string Preset1Traits = "성실하고 예의 바르지만 반복 재촉에는 스트레스를 받는다.";

        public const string Preset2Name = "박태만";
        public const string Preset2Traits = "귀찮은 걸 싫어해 금방 지치지만 쉬게 해주면 금방 풀린다.";

        public const string Preset3Name = "최소심";
        public const string Preset3Traits = "상처를 잘 받아 거친 말투에 취약하지만 칭찬하면 힘을 낸다.";

        private static readonly EmployeePersonaSetupData[] _randomPool =
        {
            new EmployeePersonaSetupData(
                Preset1Name,
                Preset1Traits,
                DefaultHourlyWageWon,
                EmployeePersonalityProfile.CreatePreset1Diligent()),
            new EmployeePersonaSetupData(
                Preset2Name,
                Preset2Traits,
                DefaultHourlyWageWon,
                EmployeePersonalityProfile.CreatePreset2Lazy()),
            new EmployeePersonaSetupData(
                Preset3Name,
                Preset3Traits,
                DefaultHourlyWageWon,
                EmployeePersonalityProfile.CreatePreset3Timid()),
            new EmployeePersonaSetupData(
                "이정석",
                "융통성이 없고 사장님의 말을 문자 그대로 곧이곧대로 믿는다.",
                DefaultHourlyWageWon,
                EmployeePersonalityProfile.CreateRandom4Rigid()),
            new EmployeePersonaSetupData(
                "정열정",
                "의욕만 앞서는 행동파 알바. 칭찬해 주면 신나서 뛰어다닌다.",
                DefaultHourlyWageWon,
                EmployeePersonalityProfile.CreateRandom5Passionate())
        };

        public static int RandomPoolCount => _randomPool.Length;

        public static EmployeePersonaSetupData CreatePreset1() => _randomPool[0];

        public static EmployeePersonaSetupData CreatePreset2() => _randomPool[1];

        public static EmployeePersonaSetupData CreatePreset3() => _randomPool[2];

        public static EmployeePersonaSetupData GetPreset(int presetNumber)
        {
            switch (presetNumber)
            {
                case 2:
                    return CreatePreset2();
                case 3:
                    return CreatePreset3();
                default:
                    return CreatePreset1();
            }
        }

        public static EmployeePersonaSetupData GetRandomCandidate(int index)
        {
            if (_randomPool.Length == 0) return CreatePreset1();
            int normalized = Math.Abs(index) % _randomPool.Length;
            return _randomPool[normalized];
        }

        public static EmployeePersonalityProfile InferProfileFromTraits(string rawTraits)
        {
            if (string.IsNullOrWhiteSpace(rawTraits))
            {
                return EmployeePersonalityProfile.CreatePreset1Diligent();
            }

            string text = rawTraits.Trim();
            if (text.Contains("귀찮") || text.Contains("게으") || text.Contains("태만") ||
                text.Contains("농땡") || text.Contains("한숨") || text.Contains("시급"))
            {
                return EmployeePersonalityProfile.CreatePreset2Lazy();
            }

            if (text.Contains("소심") || text.Contains("상처") || text.Contains("유리") ||
                text.Contains("겁") || text.Contains("예민") || text.Contains("칭찬"))
            {
                if (text.Contains("열정") || text.Contains("의욕") || text.Contains("행동파") || text.Contains("뛰어"))
                {
                    return EmployeePersonalityProfile.CreateRandom5Passionate();
                }

                return EmployeePersonalityProfile.CreatePreset3Timid();
            }

            if (text.Contains("융통성") || text.Contains("원칙") || text.Contains("문자 그대로") ||
                text.Contains("곧이곧대로") || text.Contains("고집"))
            {
                return EmployeePersonalityProfile.CreateRandom4Rigid();
            }

            if (text.Contains("열정") || text.Contains("의욕") || text.Contains("행동파") ||
                text.Contains("활발") || text.Contains("뛰어"))
            {
                return EmployeePersonalityProfile.CreateRandom5Passionate();
            }

            if (text.Contains("성실") || text.Contains("예의") || text.Contains("모범") || text.Contains("재촉"))
            {
                return EmployeePersonalityProfile.CreatePreset1Diligent();
            }

            return EmployeePersonalityProfile.CreateDefault();
        }

        public static string SanitizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return Preset1Name;
            string trimmed = rawName.Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }
            return trimmed;
        }

        public static string SanitizeTraits(string rawTraits)
        {
            if (string.IsNullOrWhiteSpace(rawTraits)) return Preset1Traits;
            string trimmed = rawTraits.Replace("\r", " ").Replace("\n", " ").Trim();
            if (trimmed.Length > MaxTraitLength)
            {
                trimmed = trimmed.Substring(0, MaxTraitLength);
            }
            return trimmed;
        }

        public static string ClampTraitInput(string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput)) return string.Empty;
            string singleLine = rawInput.Replace("\r", string.Empty).Replace("\n", " ");
            return singleLine.Length > MaxTraitLength
                ? singleLine.Substring(0, MaxTraitLength)
                : singleLine;
        }

        public static string FormatTraitLengthCounter(string currentText)
        {
            string clamped = ClampTraitInput(currentText);
            return $"({clamped.Length}/{MaxTraitLength})";
        }
    }

    public readonly struct EmotionReactionWeights
    {
        public float AcceptNormal { get; }
        public float AcceptComplain { get; }
        public float Mistake { get; }
        public float SlackOff { get; }
        public float RefuseEmotion { get; }
        public float Total => AcceptNormal + AcceptComplain + Mistake + SlackOff + RefuseEmotion;

        public EmotionReactionWeights(float acceptNormal, float acceptComplain, float slackOff, float refuseEmotion)
            : this(acceptNormal, acceptComplain, 0f, slackOff, refuseEmotion)
        {
        }

        public EmotionReactionWeights(
            float acceptNormal,
            float acceptComplain,
            float mistake,
            float slackOff,
            float refuseEmotion)
        {
            AcceptNormal = Math.Max(0f, acceptNormal);
            AcceptComplain = Math.Max(0f, acceptComplain);
            Mistake = Math.Max(0f, mistake);
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
        public const float OverloadThreshold = 70f;
        public const float AngerThreshold = 80f;
        public const float FuriousThreshold = 90f;

        public const float StableEfficiency = 1.0f;
        public const float ComplainingEfficiency = 0.8f;
        public const float AngryEfficiency = 0.5f;

        public const float PraiseBaseRecovery = 8f;
        public const float RepeatNagBasePenalty = 10f;
        public const float TaskSwitchBasePenalty = 12f;
        public const float HarshCommandBasePenalty = 18f;
        public const float RestCommandInstantRecovery = 10f;

        public const float WageRaiseStressDelta = -25f;
        public const float WageFreezeStressDelta = -10f;
        public const float WageCutStressDelta = 25f;

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

        /// <summary>
        /// 페르소나별 지속 업무/휴식 스트레스 초당 변화량 계산.
        /// </summary>
        public static float EvaluateContinuousStressDelta(
            StaffWorkRole currentRole,
            bool isStoreCrowdedOrNoisy,
            EmployeePersonalityProfile profile,
            EmotionStatRates baseRates,
            float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return 0f;

            float ratePerSec;
            if (currentRole == StaffWorkRole.Checkout ||
                currentRole == StaffWorkRole.Restock ||
                currentRole == StaffWorkRole.Other)
            {
                ratePerSec = baseRates.WorkStressPerSecond * profile.WorkStressMultiplier;
            }
            else
            {
                ratePerSec = -baseRates.RestStressRecoveryPerSecond * profile.RestRecoveryMultiplier;
            }

            if (isStoreCrowdedOrNoisy)
            {
                ratePerSec += 0.8f * profile.WorkStressMultiplier;
            }

            return ratePerSec * deltaSeconds;
        }

        /// <summary>
        /// 음성/텍스트 지시 1회 발생 시 페르소나·발화 태도·작업 전환 여부에 따른 즉발 스트레스 증감량 계산.
        /// </summary>
        public static float EvaluateCommandStressDelta(
            StoreCommandIntent intent,
            UtteranceTone tone,
            StaffWorkRole currentRole,
            StaffWorkRole targetRole,
            EmployeePersonalityProfile profile)
        {
            if (intent == StoreCommandIntent.Rest)
            {
                return -RestCommandInstantRecovery * profile.RestRecoveryMultiplier;
            }

            if (intent == StoreCommandIntent.Clarify || intent == StoreCommandIntent.Unsupported)
            {
                float tonePenalty = tone == UtteranceTone.HarshOrNagging
                    ? HarshCommandBasePenalty * profile.HarshToneMultiplier
                    : 0f;
                return profile.AmbiguousCommandPenalty + tonePenalty;
            }

            float delta = 0f;

            // 작업 중 업무 강제 전환 vs 동일 업무 반복 재촉
            if (currentRole != StaffWorkRole.Idle && targetRole != StaffWorkRole.Idle)
            {
                if (currentRole != targetRole)
                {
                    delta += TaskSwitchBasePenalty * profile.TaskSwitchMultiplier;
                }
                else
                {
                    delta += RepeatNagBasePenalty * profile.TaskSwitchMultiplier;
                }
            }

            // 발화 태도(칭찬/부탁 vs 강압/독촉) 보정
            switch (tone)
            {
                case UtteranceTone.PoliteOrPraise:
                    delta -= PraiseBaseRecovery * profile.PraiseRecoveryMultiplier;
                    break;
                case UtteranceTone.HarshOrNagging:
                    delta += HarshCommandBasePenalty * profile.HarshToneMultiplier;
                    break;
            }

            return delta;
        }

        /// <summary>
        /// 일일 정산(8번 화면) 시급 조정에 따른 다음 날 스트레스 변화량 계산.
        /// </summary>
        public static float EvaluateWageAdjustmentStressDelta(
            WageAdjustmentOption option,
            EmployeePersonalityProfile profile)
        {
            switch (option)
            {
                case WageAdjustmentOption.Raise:
                    return WageRaiseStressDelta * profile.WageSensitivityMultiplier;
                case WageAdjustmentOption.Cut:
                    return WageCutStressDelta * profile.WageSensitivityMultiplier;
                default:
                    return WageFreezeStressDelta;
            }
        }
    }

    /// <summary>
    /// 기능정의서 SYS-03 / SYS-06 및 페르소나·스트레스 구간별 행동 확률 프로토타입 기준:
    /// 스트레스 구간(0~39 안정 / 40~69 불만 / 70~89 과부하 / 90~100 분노 폭발) 및
    /// 캐릭터 성격·피로도에 따른 5종 반응 가중치(정상 / 투덜 / 실수 / 농땡이 / 거부) 계산과 대사 선택.
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
                return new EmotionReactionWeights(100f, 0f, 0f, 0f, 0f);
            }

            float normal;
            float complain;
            float mistake;
            float slack;
            float refuse;

            if (s < StressRules.ComplainingThreshold)
            {
                // 0 ~ 39 (안정): 정상 85% / 불만 10% / 실수 3% / 농땡이 2% / 거부 0%
                normal = 85f + (personality.Diligence - 50f) * 0.15f;
                complain = 10f;
                mistake = 3f;
                slack = Math.Max(0.5f, 2f + (personality.Laziness - 20f) * 0.08f + f * 0.03f);
                refuse = 0f;
            }
            else if (s < StressRules.OverloadThreshold)
            {
                // 40 ~ 69 (불만): 정상 35% / 불만 40% / 실수 10% / 농땡이 10% / 거부 5%
                normal = Math.Max(5f, 35f + (personality.Diligence - 70f) * 0.15f);
                complain = 40f;
                mistake = 10f;
                slack = Math.Max(2f, 10f + (personality.Laziness - 20f) * 0.15f + f * 0.06f);
                refuse = Math.Max(1f, 5f + (70f - personality.Loyalty) * 0.12f);
            }
            else if (s < StressRules.FuriousThreshold)
            {
                // 70 ~ 89 (과부하): 정상 10% / 불만 25% / 실수 20% / 농땡이 20% / 거부 25%
                normal = Math.Max(2f, 10f + (personality.Diligence - 70f) * 0.10f);
                complain = 25f;
                mistake = 20f;
                slack = Math.Max(5f, 20f + (personality.Laziness - 20f) * 0.15f + f * 0.08f);
                refuse = Math.Max(10f, 25f + (70f - personality.Loyalty) * 0.18f);
            }
            else
            {
                // 90 ~ 100 (분노 폭발): 정상 0% / 불만 10% / 실수 15% / 농땡이 20% / 거부 55%
                normal = 0f;
                complain = 10f;
                mistake = 15f;
                slack = Math.Max(5f, 20f + (personality.Laziness - 20f) * 0.15f + f * 0.10f);
                refuse = Math.Max(25f, 55f + (70f - personality.Loyalty) * 0.20f + (s - 90f) * 0.5f);
            }

            // 페르소나 아키타입별 고유 행동 가중치 보정
            switch (personality.Archetype)
            {
                case PersonaArchetype.LazyComplainer:
                    slack *= 1.8f;
                    complain *= 1.2f;
                    break;
                case PersonaArchetype.FragileTimid:
                    if (s >= StressRules.ComplainingThreshold)
                    {
                        mistake *= 1.5f;
                    }
                    break;
                case PersonaArchetype.PassionateHasty:
                    if (f >= 60f)
                    {
                        mistake *= 1.6f;
                    }
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
                    case EmployeeReaction.Mistake:
                        mistake *= LlmProposalMultiplier;
                        break;
                    case EmployeeReaction.SlackOff:
                        slack *= LlmProposalMultiplier;
                        break;
                    case EmployeeReaction.RefuseEmotion:
                        if (s >= StressRules.ComplainingThreshold) refuse *= LlmProposalMultiplier;
                        break;
                }
            }

            return new EmotionReactionWeights(normal, complain, mistake, slack, refuse);
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
            if ((cursor -= weights.Mistake) < 0f) return EmployeeReaction.Mistake;
            if ((cursor -= weights.SlackOff) < 0f) return EmployeeReaction.SlackOff;
            return EmployeeReaction.RefuseEmotion;
        }

        public static UtteranceTone ParseKoreanTone(string utterance)
        {
            if (string.IsNullOrWhiteSpace(utterance)) return UtteranceTone.Neutral;
            string text = utterance.Trim();

            bool hasHarsh = text.Contains("빨리") || text.Contains("당장") || text.Contains("답답") ||
                            text.Contains("안 해") || text.Contains("똑바로") || text.Contains("장난") ||
                            text.Contains("!");
            if (hasHarsh)
            {
                return UtteranceTone.HarshOrNagging;
            }

            bool hasPraise = text.Contains("고마워") || text.Contains("고맙") || text.Contains("부탁") ||
                             text.Contains("수고") || text.Contains("고생") || text.Contains("잘했") ||
                             text.Contains("최고") || text.Contains("천천히");
            if (hasPraise)
            {
                return UtteranceTone.PoliteOrPraise;
            }

            return UtteranceTone.Neutral;
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

                case EmployeeReaction.Mistake:
                    return intent == StoreCommandIntent.Checkout
                        ? "앗, 손이 미끄러져서 바코드를 잘못 찍었네요... 다시 할게요!"
                        : "악! 손이 떨려서 진열할 물건을 떨어뜨렸어요...";

                case EmployeeReaction.SlackOff:
                    return "아 몰라요, 다리 아파서 그냥 잠깐 쉴래요...";

                case EmployeeReaction.RefuseEmotion:
                    return "지금 너무 힘들어서 못 하겠어요! 시키지 마세요!";

                default:
                    return "네, 확인했습니다.";
            }
        }
    }

    /// <summary>
    /// 4대 엔딩 분기 코드 (성공 엔딩 2종 + 실패 엔딩 2종).
    /// </summary>
    public enum StoreEndingType
    {
        NormalEnding1_SuccessLowStress = 0,
        NormalEnding2_SuccessHighStress = 1,
        BadEnding1_TargetShortfall = 2,
        BadEnding2_StressOverflow = 3
    }

    /// <summary>
    /// 엔딩 화면(EndingPanel)에 표시할 텍스트 데모 및 추후 컷신 슬롯 데이터.
    /// </summary>
    public readonly struct StoreEndingReport
    {
        public StoreEndingType EndingType { get; }
        public string EndingCode { get; }
        public string HeaderTitle { get; }
        public string ConditionSummary { get; }
        public string CutscenePlaceholderLabel { get; }
        public string PersonaSummaryLine { get; }
        public string NarrativeBodyText { get; }

        public StoreEndingReport(
            StoreEndingType endingType,
            string endingCode,
            string headerTitle,
            string conditionSummary,
            string cutscenePlaceholderLabel,
            string personaSummaryLine,
            string narrativeBodyText)
        {
            EndingType = endingType;
            EndingCode = endingCode ?? string.Empty;
            HeaderTitle = headerTitle ?? string.Empty;
            ConditionSummary = conditionSummary ?? string.Empty;
            CutscenePlaceholderLabel = cutscenePlaceholderLabel ?? string.Empty;
            PersonaSummaryLine = personaSummaryLine ?? string.Empty;
            NarrativeBodyText = narrativeBodyText ?? string.Empty;
        }
    }

    /// <summary>
    /// 4대 엔딩(노말엔딩 1·2 / 실패엔딩 1·2) 판정 및
    /// 알바의 성격(Persona) + 최종 스트레스 지수에 따른 엔딩 텍스트 생성 규칙.
    /// </summary>
    public static class StoreEndingRules
    {
        public const float BadStressEndingThreshold = 60f;
        public const float StressOverflowThreshold = 100f;

        public static StoreEndingType EvaluateEnding(
            int finalBalanceWon,
            int targetBalanceWon,
            float endingStress,
            int angerRefusalCount = 0)
        {
            if (endingStress >= StressOverflowThreshold)
            {
                return StoreEndingType.BadEnding2_StressOverflow;
            }

            if (finalBalanceWon < targetBalanceWon)
            {
                return StoreEndingType.BadEnding1_TargetShortfall;
            }

            if (endingStress >= BadStressEndingThreshold || angerRefusalCount >= 3)
            {
                return StoreEndingType.NormalEnding2_SuccessHighStress;
            }

            return StoreEndingType.NormalEnding1_SuccessLowStress;
        }

        public static StoreEndingReport EvaluateAndBuildReport(
            DaySettlementResult settlement,
            EmployeePersonaSetupData persona)
        {
            StoreEndingType endingType = EvaluateEnding(
                settlement.FinalBalanceWon,
                settlement.TargetBalanceWon,
                settlement.EndingStress,
                settlement.AngerRefusalCount);

            return BuildReport(
                endingType,
                persona,
                settlement.EndingStress,
                settlement.FinalBalanceWon,
                settlement.TargetBalanceWon);
        }

        public static StoreEndingReport BuildReport(
            StoreEndingType endingType,
            EmployeePersonaSetupData persona,
            float endingStress,
            int finalBalanceWon,
            int targetBalanceWon)
        {
            string name = string.IsNullOrWhiteSpace(persona.EmployeeName)
                ? EmployeePersonaPresets.Preset1Name
                : persona.EmployeeName.Trim();
            string traits = string.IsNullOrWhiteSpace(persona.PersonalityTraits)
                ? EmployeePersonaPresets.Preset1Traits
                : persona.PersonalityTraits.Trim();

            int roundedStress = (int)Math.Round(StressRules.ClampStat(endingStress), MidpointRounding.AwayFromZero);
            string archetypeLabel = GetArchetypeShortLabel(persona.Profile.Archetype);

            string personaSummaryLine =
                $"알바: {name} ({archetypeLabel})  |  성격: \"{traits}\"\n" +
                $"최종 스트레스 지수: {roundedStress} / 100  |  최종 잔액: {finalBalanceWon:N0}원 (목표: {targetBalanceWon:N0}원)";

            string endingCode;
            string headerTitle;
            string conditionSummary;
            string cutscenePlaceholder;
            string narrativeBody = BuildPersonaEndingNarrative(
                endingType,
                name,
                traits,
                persona.Profile.Archetype,
                roundedStress);

            switch (endingType)
            {
                case StoreEndingType.NormalEnding1_SuccessLowStress:
                    endingCode = "NORMAL_ENDING_01";
                    headerTitle = "노말엔딩 1  ·  성공 엔딩 (7일차 완수 + 알바 스트레스 완화)";
                    conditionSummary = "달성 조건 : 7일차 목표 금액 완수  +  알바 스트레스 지수 완화 (60 미만 유지)";
                    cutscenePlaceholder =
                        "[ 컷신 슬롯 : 노말엔딩 1 (성공 · 스트레스 완화) ]\n" +
                        "추후 2D 애니메이션 또는 3D 동작 녹화 영상이 재생될 영역입니다.";
                    break;

                case StoreEndingType.NormalEnding2_SuccessHighStress:
                    endingCode = "NORMAL_ENDING_02";
                    headerTitle = "노말엔딩 2  ·  성공 엔딩 (7일차 완수 + 알바 스트레스 악화)";
                    conditionSummary = "달성 조건 : 7일차 목표 금액 완수  +  알바 스트레스 지수 악화 (60 이상 또는 거부 누적)";
                    cutscenePlaceholder =
                        "[ 컷신 슬롯 : 노말엔딩 2 (성공 · 스트레스 악화) ]\n" +
                        "추후 2D 애니메이션 또는 3D 동작 녹화 영상이 재생될 영역입니다.";
                    break;

                case StoreEndingType.BadEnding1_TargetShortfall:
                    endingCode = "BAD_ENDING_01";
                    headerTitle = "실패 엔딩 1  ·  일차 목표 금액 달성 실패";
                    conditionSummary = "발생 조건 : 일차 마감 시 최종 잔액이 목표 금액에 도달하지 못함";
                    cutscenePlaceholder =
                        "[ 컷신 슬롯 : 실패 엔딩 1 (목표 금액 미달 · 폐업) ]\n" +
                        "추후 2D 애니메이션 또는 3D 동작 녹화 영상이 재생될 영역입니다.";
                    break;

                default:
                    endingCode = "BAD_ENDING_02";
                    headerTitle = "실패 엔딩 2  ·  알바 스트레스 100 초과 (멘탈 폭발·탈주)";
                    conditionSummary = "발생 조건 : 알바 스트레스 지수가 100에 도달하여 근무 중 이탈 및 영업 강제 종료";
                    cutscenePlaceholder =
                        "[ 컷신 슬롯 : 실패 엔딩 2 (알바 스트레스 100 폭발 · 중도 탈주) ]\n" +
                        "추후 2D 애니메이션 또는 3D 동작 녹화 영상이 재생될 영역입니다.";
                    break;
            }

            return new StoreEndingReport(
                endingType,
                endingCode,
                headerTitle,
                conditionSummary,
                cutscenePlaceholder,
                personaSummaryLine,
                narrativeBody);
        }

        private static string GetArchetypeShortLabel(PersonaArchetype archetype)
        {
            switch (archetype)
            {
                case PersonaArchetype.LazyComplainer:
                    return "귀차니즘형";
                case PersonaArchetype.FragileTimid:
                    return "유리멘탈·소심형";
                case PersonaArchetype.LiteralRigid:
                    return "원리원칙형";
                case PersonaArchetype.PassionateHasty:
                    return "의욕과다·열정형";
                default:
                    return "성실·모범형";
            }
        }

        private static string BuildPersonaEndingNarrative(
            StoreEndingType endingType,
            string name,
            string traits,
            PersonaArchetype archetype,
            int stress)
        {
            switch (endingType)
            {
                case StoreEndingType.NormalEnding1_SuccessLowStress:
                    switch (archetype)
                    {
                        case PersonaArchetype.LazyComplainer:
                            return $"[매장 결말] 7일차까지 목표 금액을 무사히 완수하며 편의점 운영에 성공했습니다!\n" +
                                   $"[알바 후일담] 귀찮은 걸 싫어하던 {name}(스트레스 {stress}/100)은(는) \"사장님이 쉴 땐 확실히 쉬게 해주셔서 생각보다 할 만했네요. 다음 주에도 시급만 챙겨주시면 계속 나올게요\"라며 은근슬쩍 미소를 지었습니다.";
                        case PersonaArchetype.FragileTimid:
                            return $"[매장 결말] 7일차까지 목표 금액을 무사히 완수하며 편의점 운영에 성공했습니다!\n" +
                                   $"[알바 후일담] 상처를 잘 받던 {name}(스트레스 {stress}/100)은(는) \"실수해도 다그치지 않고 다정하게 지시해 주셔서 정말 감사했어요! 저 이제 편의점 일이 안 무서워요!\"라며 감격한 표정으로 인사했습니다.";
                        case PersonaArchetype.LiteralRigid:
                            return $"[매장 결말] 7일차까지 목표 금액을 무사히 완수하며 편의점 운영에 성공했습니다!\n" +
                                   $"[알바 후일담] 원리원칙대로 일하는 {name}(스트레스 {stress}/100)은(는) \"사장님의 명확한 업무 지시 덕분에 매뉴얼대로 완벽히 근무할 수 있었습니다\"라며 깍듯이 고개를 숙였습니다.";
                        case PersonaArchetype.PassionateHasty:
                            return $"[매장 결말] 7일차까지 목표 금액을 무사히 완수하며 편의점 운영에 성공했습니다!\n" +
                                   $"[알바 후일담] 의욕 넘치는 {name}(스트레스 {stress}/100)은(는) \"사장님이 칭찬해 주셔서 7일 내내 날아다녔습니다! 우리 매장 매출 1등 가보자고요!\"라며 주먹을 불끈 쥐었습니다.";
                        default:
                            return $"[매장 결말] 7일차까지 목표 금액을 무사히 완수하며 편의점 운영에 성공했습니다!\n" +
                                   $"[알바 후일담] 성실한 성격의 {name}(스트레스 {stress}/100)은(는) \"사장님이 업무 배분과 휴식을 적절히 챙겨주셔서 편하게 일했습니다. 다음 주부터도 든든하게 매장 지키겠습니다!\"라며 밝게 웃었습니다.";
                    }

                case StoreEndingType.NormalEnding2_SuccessHighStress:
                    switch (archetype)
                    {
                        case PersonaArchetype.LazyComplainer:
                            return $"[매장 결말] 7일차 목표 금액은 달성했지만, 알바의 스트레스가 극에 달한 아슬아슬한 흑자였습니다.\n" +
                                   $"[알바 후일담] 지칠 대로 지친 {name}(스트레스 {stress}/100)은(는) 마지막 정산금이 입금되자마자 \"사람을 이렇게 굴리는데 누가 더 합니까? 전 오늘부로 그만둡니다\"라는 문자를 남기고 연락을 끊었습니다.";
                        case PersonaArchetype.FragileTimid:
                            return $"[매장 결말] 7일차 목표 금액은 달성했지만, 알바의 스트레스가 극에 달한 아슬아슬한 흑자였습니다.\n" +
                                   $"[알바 후일담] 내내 긴장하며 눈치를 보던 {name}(스트레스 {stress}/100)은(는) 눈시울을 붉힌 채 \"사장님 밑에서 일하는 게 너무 무섭고 힘들어요... 죄송하지만 오늘까지만 할게요\"라며 도망치듯 퇴근했습니다.";
                        case PersonaArchetype.LiteralRigid:
                            return $"[매장 결말] 7일차 목표 금액은 달성했지만, 알바의 스트레스가 극에 달한 아슬아슬한 흑자였습니다.\n" +
                                   $"[알바 후일담] {name}(스트레스 {stress}/100)은(는) 무표정한 얼굴로 \"근로계약서상 금일 근무까지만 이행하겠습니다. 중구난방인 지시 방식은 개선하셔야 할 겁니다\"라고 통보한 뒤 매장을 떠났습니다.";
                        case PersonaArchetype.PassionateHasty:
                            return $"[매장 결말] 7일차 목표 금액은 달성했지만, 알바의 스트레스가 극에 달한 아슬아슬한 흑자였습니다.\n" +
                                   $"[알바 후일담] 처음엔 열정이 넘쳤던 {name}(스트레스 {stress}/100)도 완전히 번아웃이 와서 \"아무리 열심히 뛰어도 재촉만 하시네요... 저도 이제 지쳤어요\"라며 앞치마를 카운터에 내려놓았습니다.";
                        default:
                            return $"[매장 결말] 7일차 목표 금액은 달성했지만, 알바의 스트레스가 극에 달한 아슬아슬한 흑자였습니다.\n" +
                                   $"[알바 후일담] 참을성 많던 {name}(스트레스 {stress}/100)조차 차가운 표정으로 \"목표 매출 채우셨으니 제 할 일은 다 끝났네요. 내일부터는 다른 알바 구하세요\"라며 뒤도 돌아보지 않고 퇴근했습니다.";
                    }

                case StoreEndingType.BadEnding1_TargetShortfall:
                    return $"[매장 결말] 일차 목표 금액을 채우지 못해 결국 편의점 운영 자금이 바닥났습니다.\n" +
                           $"[알바 후일담] '{traits}' 성향의 {name}(최종 스트레스 {stress}/100)은(는) 불 꺼진 편의점 간판을 쓸쓸히 올려다보며 \"사장님... 장사가 참 쉽지 않네요. 그동안 고생 많으셨습니다\"라고 인사한 뒤 구인 앱을 켜고 새 일자리를 찾아 떠났습니다.";

                default:
                    return $"[매장 결말] 알바의 스트레스 지수가 100을 초과해 근무 도중 멘탈이 폭발했습니다!\n" +
                           $"[알바 후일담] '{traits}' 성향의 {name}(스트레스 {stress}/100)은(는) \"더 이상은 못 해 먹겠어요! 사장님이 직접 다 하세요!\"라고 소리치며 앞치마를 집어 던지고 매장을 뛰쳐나갔고, 홀로 남겨진 사장님은 그날 영업을 강제로 마감해야 했습니다.";
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
