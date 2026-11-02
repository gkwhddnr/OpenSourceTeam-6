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

        public VoiceCommandFeedback(
            CommandFeedbackKind kind,
            string recognizedText,
            string actionSummary,
            string detailMessage)
        {
            Kind = kind;
            RecognizedText = recognizedText ?? string.Empty;
            ActionSummary = actionSummary ?? string.Empty;
            DetailMessage = detailMessage ?? string.Empty;
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
