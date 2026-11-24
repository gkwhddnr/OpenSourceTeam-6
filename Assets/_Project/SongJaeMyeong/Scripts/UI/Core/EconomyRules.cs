using System;

namespace OpenSourceTeam6.UI.Core
{
    [Serializable]
    public struct DaySettlementSettings
    {
        public float DayDurationSeconds;
        public int StartingBalanceWon;
        public int TargetBalanceWon;
        public float CheckoutWonPerEffectiveSecond;
        public float RestockWonPerEffectiveSecond;
        public float MinEffectiveRestockSeconds;
        public float RestockShortageMultiplier;
        public float CostOfGoodsRatio;
        public int FixedOperatingCostWon;
        public float AngerStressThreshold;
        public int MaxAllowedAngerRefusals;

        public static DaySettlementSettings CreateDefault()
        {
            return new DaySettlementSettings
            {
                DayDurationSeconds = 180f,
                StartingBalanceWon = 50000,
                TargetBalanceWon = 80000,
                CheckoutWonPerEffectiveSecond = 800f,
                RestockWonPerEffectiveSecond = 500f,
                MinEffectiveRestockSeconds = 25f,
                RestockShortageMultiplier = 0.8f,
                CostOfGoodsRatio = 0.3f,
                FixedOperatingCostWon = 25000,
                AngerStressThreshold = 80f,
                MaxAllowedAngerRefusals = 2
            };
        }

        /// <summary>
        /// 1차 통합 데모 규격서(4.2절) 기준 120초 시연용 정산 밸런스 프리셋.
        /// </summary>
        public static DaySettlementSettings CreateDemoPreset()
        {
            return new DaySettlementSettings
            {
                DayDurationSeconds = 120f,
                StartingBalanceWon = 50000,
                TargetBalanceWon = 80000,
                CheckoutWonPerEffectiveSecond = 1000f,
                RestockWonPerEffectiveSecond = 650f,
                MinEffectiveRestockSeconds = 15f,
                RestockShortageMultiplier = 0.8f,
                CostOfGoodsRatio = 0.3f,
                FixedOperatingCostWon = 20000,
                AngerStressThreshold = 80f,
                MaxAllowedAngerRefusals = 2
            };
        }

        public void Validate()
        {
            if (!IsFinitePositive(DayDurationSeconds))
                throw new ArgumentOutOfRangeException(nameof(DayDurationSeconds), "영업 시간은 0보다 큰 유한 값이어야 합니다.");
            if (!IsFiniteNonNegative(CheckoutWonPerEffectiveSecond))
                throw new ArgumentOutOfRangeException(nameof(CheckoutWonPerEffectiveSecond));
            if (!IsFiniteNonNegative(RestockWonPerEffectiveSecond))
                throw new ArgumentOutOfRangeException(nameof(RestockWonPerEffectiveSecond));
            if (!IsFiniteNonNegative(MinEffectiveRestockSeconds))
                throw new ArgumentOutOfRangeException(nameof(MinEffectiveRestockSeconds));
            if (float.IsNaN(RestockShortageMultiplier) || float.IsInfinity(RestockShortageMultiplier) ||
                RestockShortageMultiplier <= 0f || RestockShortageMultiplier > 1f)
                throw new ArgumentOutOfRangeException(nameof(RestockShortageMultiplier));
            if (float.IsNaN(CostOfGoodsRatio) || float.IsInfinity(CostOfGoodsRatio) ||
                CostOfGoodsRatio < 0f || CostOfGoodsRatio >= 1f)
                throw new ArgumentOutOfRangeException(nameof(CostOfGoodsRatio));
            if (FixedOperatingCostWon < 0)
                throw new ArgumentOutOfRangeException(nameof(FixedOperatingCostWon));
        }

        private static bool IsFinitePositive(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        private static bool IsFiniteNonNegative(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f;
    }

    public readonly struct DaySettlementInput
    {
        public int DayNumber { get; }
        public int StartingBalanceWon { get; }
        public float RawCheckoutSeconds { get; }
        public float RawRestockSeconds { get; }
        public float EffectiveCheckoutSeconds { get; }
        public float EffectiveRestockSeconds { get; }
        public float EndingStress { get; }
        public int AngerRefusalCount { get; }

        public DaySettlementInput(
            int dayNumber,
            int startingBalanceWon,
            float rawCheckoutSeconds,
            float rawRestockSeconds,
            float effectiveCheckoutSeconds,
            float effectiveRestockSeconds,
            float endingStress,
            int angerRefusalCount)
        {
            if (dayNumber < 1) throw new ArgumentOutOfRangeException(nameof(dayNumber));
            ValidateSeconds(rawCheckoutSeconds, nameof(rawCheckoutSeconds));
            ValidateSeconds(rawRestockSeconds, nameof(rawRestockSeconds));
            ValidateSeconds(effectiveCheckoutSeconds, nameof(effectiveCheckoutSeconds));
            ValidateSeconds(effectiveRestockSeconds, nameof(effectiveRestockSeconds));
            if (angerRefusalCount < 0) throw new ArgumentOutOfRangeException(nameof(angerRefusalCount));

            DayNumber = dayNumber;
            StartingBalanceWon = startingBalanceWon;
            RawCheckoutSeconds = rawCheckoutSeconds;
            RawRestockSeconds = rawRestockSeconds;
            EffectiveCheckoutSeconds = effectiveCheckoutSeconds;
            EffectiveRestockSeconds = effectiveRestockSeconds;
            EndingStress = StressRules.ClampStat(endingStress);
            AngerRefusalCount = angerRefusalCount;
        }

        private static void ValidateSeconds(float seconds, string paramName)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, "작업 시간은 0 이상의 유한 값이어야 합니다.");
            }
        }
    }

    public readonly struct DaySettlementResult
    {
        public int DayNumber { get; }
        public int StartingBalanceWon { get; }
        public int TargetBalanceWon { get; }
        public float EffectiveCheckoutSeconds { get; }
        public float EffectiveRestockSeconds { get; }
        public float MinEffectiveRestockSeconds { get; }
        public int CheckoutRevenueWon { get; }
        public int RestockRevenueWon { get; }
        public int GrossBaseRevenueWon { get; }
        public bool RestockShortagePenaltyApplied { get; }
        public int FinalSalesRevenueWon { get; }
        public int CostOfGoodsWon { get; }
        public int FixedOperatingCostWon { get; }
        public int NetProfitWon { get; }
        public int FinalBalanceWon { get; }
        public float EndingStress { get; }
        public int AngerRefusalCount { get; }
        public DayOutcomeGrade OutcomeGrade { get; }

        public DaySettlementResult(
            int dayNumber,
            int startingBalanceWon,
            int targetBalanceWon,
            float effectiveCheckoutSeconds,
            float effectiveRestockSeconds,
            int checkoutRevenueWon,
            int restockRevenueWon,
            int grossBaseRevenueWon,
            bool restockShortagePenaltyApplied,
            int finalSalesRevenueWon,
            int costOfGoodsWon,
            int fixedOperatingCostWon,
            int netProfitWon,
            int finalBalanceWon,
            float endingStress,
            int angerRefusalCount,
            DayOutcomeGrade outcomeGrade,
            float minEffectiveRestockSeconds = 25f)
        {
            DayNumber = dayNumber;
            StartingBalanceWon = startingBalanceWon;
            TargetBalanceWon = targetBalanceWon;
            EffectiveCheckoutSeconds = effectiveCheckoutSeconds;
            EffectiveRestockSeconds = effectiveRestockSeconds;
            MinEffectiveRestockSeconds = minEffectiveRestockSeconds;
            CheckoutRevenueWon = checkoutRevenueWon;
            RestockRevenueWon = restockRevenueWon;
            GrossBaseRevenueWon = grossBaseRevenueWon;
            RestockShortagePenaltyApplied = restockShortagePenaltyApplied;
            FinalSalesRevenueWon = finalSalesRevenueWon;
            CostOfGoodsWon = costOfGoodsWon;
            FixedOperatingCostWon = fixedOperatingCostWon;
            NetProfitWon = netProfitWon;
            FinalBalanceWon = finalBalanceWon;
            EndingStress = endingStress;
            AngerRefusalCount = angerRefusalCount;
            OutcomeGrade = outcomeGrade;
        }
    }

    public static class DaySettlementCalculator
    {
        public static DaySettlementResult Calculate(DaySettlementSettings settings, DaySettlementInput input)
        {
            settings.Validate();

            int checkoutWon = RoundToWon(input.EffectiveCheckoutSeconds * settings.CheckoutWonPerEffectiveSecond);
            int restockWon = RoundToWon(input.EffectiveRestockSeconds * settings.RestockWonPerEffectiveSecond);
            int baseRevenueWon = checkoutWon + restockWon;

            bool shortagePenalty = input.EffectiveRestockSeconds < settings.MinEffectiveRestockSeconds;
            int finalSalesWon = shortagePenalty
                ? RoundToWon(baseRevenueWon * settings.RestockShortageMultiplier)
                : baseRevenueWon;

            int cogsWon = RoundToWon(finalSalesWon * settings.CostOfGoodsRatio);
            int fixedCostWon = settings.FixedOperatingCostWon;
            int netProfitWon = finalSalesWon - (cogsWon + fixedCostWon);
            int finalBalanceWon = input.StartingBalanceWon + netProfitWon;

            DayOutcomeGrade grade;
            if (finalBalanceWon < settings.TargetBalanceWon)
            {
                grade = DayOutcomeGrade.FailedTargetShortfall;
            }
            else if (input.EndingStress >= settings.AngerStressThreshold ||
                     input.AngerRefusalCount > settings.MaxAllowedAngerRefusals)
            {
                grade = DayOutcomeGrade.UnstableAngerSuccess;
            }
            else
            {
                grade = DayOutcomeGrade.StableSuccess;
            }

            return new DaySettlementResult(
                input.DayNumber,
                input.StartingBalanceWon,
                settings.TargetBalanceWon,
                input.EffectiveCheckoutSeconds,
                input.EffectiveRestockSeconds,
                checkoutWon,
                restockWon,
                baseRevenueWon,
                shortagePenalty,
                finalSalesWon,
                cogsWon,
                fixedCostWon,
                netProfitWon,
                finalBalanceWon,
                input.EndingStress,
                input.AngerRefusalCount,
                grade,
                settings.MinEffectiveRestockSeconds);
        }

        private static int RoundToWon(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class DaySettlementSession
    {
        private DaySettlementSettings settings;
        private DaySettlementResult cachedResult;

        public int DayNumber { get; private set; }
        public int StartingBalanceWon { get; private set; }
        public int TargetBalanceWon => settings.TargetBalanceWon;
        public int FixedOperatingCostWon => settings.FixedOperatingCostWon;
        public float RawCheckoutSeconds { get; private set; }
        public float RawRestockSeconds { get; private set; }
        public float EffectiveCheckoutSeconds { get; private set; }
        public float EffectiveRestockSeconds { get; private set; }
        public int AngerRefusalCount { get; private set; }
        public bool IsSettled { get; private set; }

        public DaySettlementSession(DaySettlementSettings initialSettings)
        {
            initialSettings.Validate();
            settings = initialSettings;
            Reset(1, initialSettings.StartingBalanceWon);
        }

        public void Reset(int dayNumber, int startingBalanceWon)
        {
            if (dayNumber < 1) throw new ArgumentOutOfRangeException(nameof(dayNumber));
            DayNumber = dayNumber;
            StartingBalanceWon = startingBalanceWon;
            RawCheckoutSeconds = 0f;
            RawRestockSeconds = 0f;
            EffectiveCheckoutSeconds = 0f;
            EffectiveRestockSeconds = 0f;
            AngerRefusalCount = 0;
            IsSettled = false;
            cachedResult = default;
        }

        public void RecordWorkSample(StaffWorkRole role, float currentStress, float deltaSeconds)
        {
            if (IsSettled) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "누적 시간 증분은 0 이상의 유한 값이어야 합니다.");
            }
            if (deltaSeconds == 0f) return;

            float efficiency = StressRules.GetEfficiencyMultiplier(currentStress);
            float effectiveDelta = deltaSeconds * efficiency;

            if (role == StaffWorkRole.Checkout)
            {
                RawCheckoutSeconds += deltaSeconds;
                EffectiveCheckoutSeconds += effectiveDelta;
            }
            else if (role == StaffWorkRole.Restock)
            {
                RawRestockSeconds += deltaSeconds;
                EffectiveRestockSeconds += effectiveDelta;
            }
        }

        public void RecordAngerRefusal()
        {
            if (IsSettled) return;
            AngerRefusalCount++;
        }

        public int EstimateLiveGrossSalesWon()
        {
            int checkoutWon = (int)Math.Round(EffectiveCheckoutSeconds * settings.CheckoutWonPerEffectiveSecond);
            int restockWon = (int)Math.Round(EffectiveRestockSeconds * settings.RestockWonPerEffectiveSecond);
            return checkoutWon + restockWon;
        }

        public void SetTargetBalanceWon(int targetBalanceWon)
        {
            settings.TargetBalanceWon = Math.Max(0, targetBalanceWon);
        }

        public void SetFixedOperatingCostWon(int fixedOperatingCostWon)
        {
            settings.FixedOperatingCostWon = Math.Max(0, fixedOperatingCostWon);
        }

        public DaySettlementResult SettleOnce(float endingStress, int extraItemSalesRevenueWon = 0)
        {
            if (IsSettled)
            {
                return cachedResult;
            }

            DaySettlementInput input = new DaySettlementInput(
                DayNumber,
                StartingBalanceWon,
                RawCheckoutSeconds,
                RawRestockSeconds,
                EffectiveCheckoutSeconds,
                EffectiveRestockSeconds,
                endingStress,
                AngerRefusalCount);

            DaySettlementResult baseResult = DaySettlementCalculator.Calculate(settings, input);
            int extraWon = Math.Max(0, extraItemSalesRevenueWon);
            if (extraWon > 0)
            {
                int finalSalesWon = baseResult.FinalSalesRevenueWon + extraWon;
                int cogsWon = (int)Math.Round(finalSalesWon * settings.CostOfGoodsRatio, MidpointRounding.AwayFromZero);
                int netProfitWon = finalSalesWon - cogsWon - settings.FixedOperatingCostWon;
                int finalBalanceWon = input.StartingBalanceWon + netProfitWon;

                DayOutcomeGrade grade;
                if (finalBalanceWon < settings.TargetBalanceWon)
                {
                    grade = DayOutcomeGrade.FailedTargetShortfall;
                }
                else if (input.EndingStress >= settings.AngerStressThreshold || input.AngerRefusalCount > settings.MaxAllowedAngerRefusals)
                {
                    grade = DayOutcomeGrade.UnstableAngerSuccess;
                }
                else
                {
                    grade = DayOutcomeGrade.StableSuccess;
                }

                baseResult = new DaySettlementResult(
                    baseResult.DayNumber,
                    baseResult.StartingBalanceWon,
                    baseResult.TargetBalanceWon,
                    baseResult.EffectiveCheckoutSeconds,
                    baseResult.EffectiveRestockSeconds,
                    baseResult.CheckoutRevenueWon,
                    baseResult.RestockRevenueWon,
                    baseResult.GrossBaseRevenueWon + extraWon,
                    baseResult.RestockShortagePenaltyApplied,
                    finalSalesWon,
                    cogsWon,
                    baseResult.FixedOperatingCostWon,
                    netProfitWon,
                    finalBalanceWon,
                    baseResult.EndingStress,
                    baseResult.AngerRefusalCount,
                    grade,
                    baseResult.MinEffectiveRestockSeconds);
            }

            cachedResult = baseResult;
            IsSettled = true;
            return cachedResult;
        }
    }

    /// <summary>
    /// 하루 영업 시간 진행에 따른 낮·노을·밤·새벽 구간 구분.
    /// </summary>
    public enum StoreTimeOfDayPhase
    {
        Day = 0,
        Sunset = 1,
        Night = 2,
        Dawn = 3
    }

    /// <summary>
    /// 시간 시스템(BusinessTimeController · DayNightPresenter 9월 기본값)과 동일한 기준으로
    /// UI 영업 타이머 진행률(0~1)을 게임 시각(08:00 ~ 익일 08:00) 및 낮·밤 상태로 변환하는 순수 규칙.
    /// </summary>
    public static class StoreDayTimeRules
    {
        public const float DefaultOpeningHour = 8f;
        public const float DefaultClosingHour = 32f;
        public const float DefaultSunriseHour = 6f;
        public const float DefaultSunsetHour = 18.5f;

        public static float ComputeDayProgress(float remainingSeconds, float totalSeconds)
        {
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds) ||
                float.IsNaN(totalSeconds) || float.IsInfinity(totalSeconds) ||
                totalSeconds <= 0f)
            {
                return 0f;
            }

            float clampedRemaining = Math.Max(0f, Math.Min(totalSeconds, remainingSeconds));
            float progress = 1f - (clampedRemaining / totalSeconds);
            if (progress < 0f) return 0f;
            if (progress > 1f) return 1f;
            return progress;
        }

        public static float ComputeGameHour(
            float progress,
            float openingHour = DefaultOpeningHour,
            float closingHour = DefaultClosingHour)
        {
            float clampedProgress = float.IsNaN(progress) || float.IsInfinity(progress)
                ? 0f
                : Math.Max(0f, Math.Min(1f, progress));
            float safeOpening = float.IsNaN(openingHour) || float.IsInfinity(openingHour)
                ? DefaultOpeningHour
                : openingHour;
            float safeClosing = float.IsNaN(closingHour) || float.IsInfinity(closingHour) || closingHour <= safeOpening
                ? DefaultClosingHour
                : closingHour;

            float rawHour = safeOpening + (safeClosing - safeOpening) * clampedProgress;
            return WrapHour24(rawHour);
        }

        public static StoreTimeOfDayPhase ClassifyPhase(
            float gameHour,
            float sunriseHour = DefaultSunriseHour,
            float sunsetHour = DefaultSunsetHour)
        {
            float wrapped = WrapHour24(gameHour);
            float dawnStart = Math.Max(0f, sunriseHour - 0.5f);
            float dawnEnd = Math.Min(24f, sunriseHour + 0.5f);
            float sunsetStart = Math.Max(dawnEnd, sunsetHour - 1.0f);
            float sunsetEnd = Math.Min(24f, sunsetHour + 0.5f);

            if (wrapped >= dawnStart && wrapped < dawnEnd)
            {
                return StoreTimeOfDayPhase.Dawn;
            }

            if (wrapped >= dawnEnd && wrapped < sunsetStart)
            {
                return StoreTimeOfDayPhase.Day;
            }

            if (wrapped >= sunsetStart && wrapped < sunsetEnd)
            {
                return StoreTimeOfDayPhase.Sunset;
            }

            return StoreTimeOfDayPhase.Night;
        }

        public static bool IsNightTime(
            float gameHour,
            float sunriseHour = DefaultSunriseHour,
            float sunsetHour = DefaultSunsetHour)
        {
            float wrapped = WrapHour24(gameHour);
            return wrapped < sunriseHour || wrapped >= sunsetHour;
        }

        public static string GetPhaseLabel(StoreTimeOfDayPhase phase)
        {
            switch (phase)
            {
                case StoreTimeOfDayPhase.Dawn:
                    return "새벽";
                case StoreTimeOfDayPhase.Sunset:
                    return "노을";
                case StoreTimeOfDayPhase.Night:
                    return "밤";
                default:
                    return "낮";
            }
        }

        public static string FormatGameClock(float gameHour)
        {
            float wrapped = WrapHour24(gameHour);
            int totalMinutes = (int)Math.Floor(wrapped * 60f + 0.0001f);
            if (totalMinutes < 0) totalMinutes = 0;
            int hours = (totalMinutes / 60) % 24;
            int minutes = totalMinutes % 60;
            return $"{hours:00}:{minutes:00}";
        }

        public static string FormatGameClockStepped(float gameHour, int minuteStep = 10)
        {
            float wrapped = WrapHour24(gameHour);
            int totalMinutes = (int)Math.Floor(wrapped * 60f + 0.0001f);
            if (totalMinutes < 0) totalMinutes = 0;
            int safeStep = Math.Max(1, minuteStep);
            int steppedMinutes = (totalMinutes / safeStep) * safeStep;
            int hours = (steppedMinutes / 60) % 24;
            int minutes = steppedMinutes % 60;
            return $"{hours:00}:{minutes:00}";
        }

        public static string FormatMonitorClock(
            float remainingSeconds,
            float totalSeconds,
            float? explicitGameHour = null,
            int minuteStep = 10,
            float openingHour = DefaultOpeningHour,
            float closingHour = DefaultClosingHour)
        {
            float safeRemaining = float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds)
                ? 0f
                : Math.Max(0f, remainingSeconds);
            bool hasExplicitHour = explicitGameHour.HasValue &&
                                   !float.IsNaN(explicitGameHour.Value) &&
                                   !float.IsInfinity(explicitGameHour.Value);
            bool hasValidTotal = !float.IsNaN(totalSeconds) && !float.IsInfinity(totalSeconds) && totalSeconds > 0f;

            float gameHour = hasExplicitHour
                ? WrapHour24(explicitGameHour.Value)
                : (hasValidTotal
                    ? ComputeGameHour(ComputeDayProgress(safeRemaining, totalSeconds), openingHour, closingHour)
                    : WrapHour24(openingHour));

            string clockText = FormatGameClockStepped(gameHour, minuteStep);
            return $"현재 시간 : {clockText}";
        }

        public static string FormatHudClockAndTimer(
            float remainingSeconds,
            float totalSeconds,
            float? explicitGameHour = null,
            float openingHour = DefaultOpeningHour,
            float closingHour = DefaultClosingHour)
        {
            float safeRemaining = float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds)
                ? 0f
                : Math.Max(0f, remainingSeconds);
            int clampedSec = Math.Max(0, (int)Math.Ceiling(safeRemaining));
            int mins = clampedSec / 60;
            int secs = clampedSec % 60;

            bool hasValidTotal = !float.IsNaN(totalSeconds) && !float.IsInfinity(totalSeconds) && totalSeconds > 0f;
            bool hasExplicitHour = explicitGameHour.HasValue &&
                                   !float.IsNaN(explicitGameHour.Value) &&
                                   !float.IsInfinity(explicitGameHour.Value);

            if (!hasValidTotal && !hasExplicitHour)
            {
                return $"남은 시간  {mins:00}:{secs:00}";
            }

            float gameHour = hasExplicitHour
                ? WrapHour24(explicitGameHour.Value)
                : ComputeGameHour(ComputeDayProgress(safeRemaining, totalSeconds), openingHour, closingHour);
            StoreTimeOfDayPhase phase = ClassifyPhase(gameHour);
            string phaseLabel = GetPhaseLabel(phase);
            string clockText = FormatGameClock(gameHour);
            return $"{clockText} ({phaseLabel})  ·  남은 {mins:00}:{secs:00}";
        }

        /// <summary>
        /// 일차별 기본 날씨(비) 적용 규칙. 1일차는 항상 맑은 낮과 밤으로 진행하며,
        /// 추후 2일차 또는 3일차 날씨 이벤트 활성화 시 비 날씨를 적용한다.
        /// </summary>
        public static bool ShouldRainForDay(int dayNumber, bool enableDay2Or3RainEvent = false)
        {
            if (dayNumber <= 1)
            {
                return false;
            }

            if ((dayNumber == 2 || dayNumber == 3) && enableDay2Or3RainEvent)
            {
                return true;
            }

            return false;
        }

        private static float WrapHour24(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour))
            {
                return DefaultOpeningHour;
            }

            float wrapped = hour % 24f;
            return wrapped < 0f ? wrapped + 24f : wrapped;
        }
    }
}
