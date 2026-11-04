using System;
using NUnit.Framework;
using OpenSourceTeam6.UI.Core;

namespace OpenSourceTeam6.UI.EditorTests
{
    public sealed class EconomyAndUiRulesTests
    {
        [Test]
        public void StressRules_ClassifyAndEfficiency_FollowsThresholds()
        {
            Assert.AreEqual(StressBand.Stable, StressRules.Classify(0f));
            Assert.AreEqual(StressBand.Stable, StressRules.Classify(39.9f));
            Assert.AreEqual(1.0f, StressRules.GetEfficiencyMultiplier(39.9f), 0.0001f);

            Assert.AreEqual(StressBand.Complaining, StressRules.Classify(40f));
            Assert.AreEqual(StressBand.Complaining, StressRules.Classify(79.9f));
            Assert.AreEqual(0.8f, StressRules.GetEfficiencyMultiplier(60f), 0.0001f);

            Assert.AreEqual(StressBand.Angry, StressRules.Classify(80f));
            Assert.AreEqual(StressBand.Angry, StressRules.Classify(100f));
            Assert.AreEqual(0.5f, StressRules.GetEfficiencyMultiplier(95f), 0.0001f);
        }

        [Test]
        public void SettlementCalculator_BalancedWork_AchievesStableSuccessWithoutPenalty()
        {
            DaySettlementSettings settings = DaySettlementSettings.CreateDefault();
            // 유효 계산 90초(72,000) + 유효 진열 30초(15,000) = 87,000원 매출
            // 원가 30%(26,100) + 고정비(25,000) = 51,100원 비용 -> 순이익 +35,900 -> 최종 잔액 85,900원
            DaySettlementInput input = new DaySettlementInput(
                dayNumber: 1,
                startingBalanceWon: 50000,
                rawCheckoutSeconds: 90f,
                rawRestockSeconds: 30f,
                effectiveCheckoutSeconds: 90f,
                effectiveRestockSeconds: 30f,
                endingStress: 52f,
                angerRefusalCount: 0);

            DaySettlementResult result = DaySettlementCalculator.Calculate(settings, input);

            Assert.IsFalse(result.RestockShortagePenaltyApplied);
            Assert.AreEqual(87000, result.FinalSalesRevenueWon);
            Assert.AreEqual(26100, result.CostOfGoodsWon);
            Assert.AreEqual(85900, result.FinalBalanceWon);
            Assert.AreEqual(DayOutcomeGrade.StableSuccess, result.OutcomeGrade);
        }

        [Test]
        public void SettlementCalculator_InsufficientRestock_AppliesShortagePenalty()
        {
            DaySettlementSettings settings = DaySettlementSettings.CreateDefault();
            // 유효 진열 10초(< 25초 기준) -> 기본 매출 85,000원에 0.8x 패널티 = 68,000원
            DaySettlementInput input = new DaySettlementInput(
                dayNumber: 1,
                startingBalanceWon: 50000,
                rawCheckoutSeconds: 100f,
                rawRestockSeconds: 10f,
                effectiveCheckoutSeconds: 100f,
                effectiveRestockSeconds: 10f,
                endingStress: 40f,
                angerRefusalCount: 0);

            DaySettlementResult result = DaySettlementCalculator.Calculate(settings, input);

            Assert.IsTrue(result.RestockShortagePenaltyApplied);
            Assert.AreEqual(85000, result.GrossBaseRevenueWon);
            Assert.AreEqual(68000, result.FinalSalesRevenueWon);
            Assert.AreEqual(DayOutcomeGrade.FailedTargetShortfall, result.OutcomeGrade);
        }

        [Test]
        public void SettlementCalculator_TargetReachedWithHighAnger_ReturnsUnstableAngerSuccess()
        {
            DaySettlementSettings settings = DaySettlementSettings.CreateDefault();
            DaySettlementInput input = new DaySettlementInput(
                dayNumber: 1,
                startingBalanceWon: 50000,
                rawCheckoutSeconds: 100f,
                rawRestockSeconds: 30f,
                effectiveCheckoutSeconds: 95f,
                effectiveRestockSeconds: 30f,
                endingStress: 84f,
                angerRefusalCount: 3);

            DaySettlementResult result = DaySettlementCalculator.Calculate(settings, input);

            Assert.GreaterOrEqual(result.FinalBalanceWon, settings.TargetBalanceWon);
            Assert.AreEqual(DayOutcomeGrade.UnstableAngerSuccess, result.OutcomeGrade);
        }

        [Test]
        public void DaySettlementSession_SettleOnce_PreventsDuplicatePayoutAndPostSettleAccumulation()
        {
            DaySettlementSettings settings = DaySettlementSettings.CreateDefault();
            DaySettlementSession session = new DaySettlementSession(settings);

            session.RecordWorkSample(StaffWorkRole.Checkout, currentStress: 20f, deltaSeconds: 90f);
            session.RecordWorkSample(StaffWorkRole.Restock, currentStress: 20f, deltaSeconds: 30f);

            DaySettlementResult first = session.SettleOnce(endingStress: 35f);
            session.RecordWorkSample(StaffWorkRole.Checkout, currentStress: 20f, deltaSeconds: 50f);
            DaySettlementResult second = session.SettleOnce(endingStress: 10f);

            Assert.IsTrue(session.IsSettled);
            Assert.AreEqual(first.FinalBalanceWon, second.FinalBalanceWon);
            Assert.AreEqual(90f, session.EffectiveCheckoutSeconds, 0.0001f);
        }

        [Test]
        public void DaySettlementSession_NegativeOrNaNDelta_ThrowsArgumentOutOfRange()
        {
            DaySettlementSession session = new DaySettlementSession(DaySettlementSettings.CreateDefault());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.RecordWorkSample(StaffWorkRole.Checkout, 20f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.RecordWorkSample(StaffWorkRole.Checkout, float.NaN, 1f));
        }

        [Test]
        public void NestedLockCounter_PartialAndDuplicateDispose_KeepsLockUntilAllReleased()
        {
            NestedLockCounter counter = new NestedLockCounter();

            IDisposable tokenA = counter.Acquire("CommandOverlay");
            IDisposable tokenB = counter.Acquire("PausePopup");

            Assert.IsTrue(counter.IsLocked);
            tokenA.Dispose();
            tokenA.Dispose(); // 중복 해제 시에도 다른 토큰에 영향 없음
            Assert.IsTrue(counter.IsLocked);

            tokenB.Dispose();
            Assert.IsFalse(counter.IsLocked);
        }
    }
}
