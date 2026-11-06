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

        public DaySettlementResult SettleOnce(float endingStress)
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

            cachedResult = DaySettlementCalculator.Calculate(settings, input);
            IsSettled = true;
            return cachedResult;
        }
    }
}
