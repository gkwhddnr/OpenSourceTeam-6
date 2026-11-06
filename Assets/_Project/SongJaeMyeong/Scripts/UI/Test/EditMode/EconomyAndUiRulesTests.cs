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
        public void SettlementCalculator_DemoPreset120s_AchievesStableSuccessAndReflects15sThreshold()
        {
            DaySettlementSettings demoSettings = DaySettlementSettings.CreateDemoPreset();
            // 120초 데모 중 계산 60초(60,000) + 진열 20초(13,000) = 73,000원 매출
            // 원가 30%(21,900) + 고정비(20,000) = 41,900원 비용 -> 순이익 +31,100 -> 최종 잔액 81,100원 (>= 80,000)
            DaySettlementInput input = new DaySettlementInput(
                dayNumber: 1,
                startingBalanceWon: 50000,
                rawCheckoutSeconds: 60f,
                rawRestockSeconds: 20f,
                effectiveCheckoutSeconds: 60f,
                effectiveRestockSeconds: 20f,
                endingStress: 45f,
                angerRefusalCount: 0);

            DaySettlementResult result = DaySettlementCalculator.Calculate(demoSettings, input);

            Assert.IsFalse(result.RestockShortagePenaltyApplied);
            Assert.AreEqual(15f, result.MinEffectiveRestockSeconds, 0.0001f);
            Assert.AreEqual(81100, result.FinalBalanceWon);
            Assert.AreEqual(DayOutcomeGrade.StableSuccess, result.OutcomeGrade);
        }

        [Test]
        public void SettlementCalculator_InsufficientRestock_AppliesShortagePenalty()
        {
            DaySettlementSettings settings = DaySettlementSettings.CreateDefault();
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
        public void EmotionReactionRules_StressBandsAndRestIntent_FollowSpecWeights()
        {
            EmployeePersonalityProfile profile = EmployeePersonalityProfile.CreateDefault();

            EmotionReactionWeights stable = EmotionReactionRules.ComputeWeights(
                StoreCommandIntent.Checkout, stress: 25f, fatigue: 15f, profile);
            Assert.AreEqual(0f, stable.RefuseEmotion, 0.0001f);
            Assert.Greater(stable.AcceptNormal, stable.AcceptComplain);

            EmotionReactionWeights complaining = EmotionReactionRules.ComputeWeights(
                StoreCommandIntent.Checkout, stress: 60f, fatigue: 40f, profile);
            Assert.Greater(complaining.AcceptComplain, complaining.AcceptNormal);
            Assert.Greater(complaining.RefuseEmotion, 0f);

            EmotionReactionWeights angry = EmotionReactionRules.ComputeWeights(
                StoreCommandIntent.Restock, stress: 90f, fatigue: 60f, profile);
            Assert.Greater(angry.RefuseEmotion, angry.AcceptNormal);
            Assert.AreEqual(EmployeeReaction.RefuseEmotion, EmotionReactionRules.SampleReaction(angry, 0.95f));

            // 분노 상태여도 휴식(REST) 지시는 100% 정상 수락 보장
            EmotionReactionWeights restWhileAngry = EmotionReactionRules.ComputeWeights(
                StoreCommandIntent.Rest, stress: 98f, fatigue: 90f, profile);
            Assert.AreEqual(EmployeeReaction.AcceptNormal, EmotionReactionRules.SampleReaction(restWhileAngry, 0.99f));
        }

        [Test]
        public void EmotionReactionRules_ParseKoreanIntent_ClassifiesStoreCommands()
        {
            Assert.AreEqual(StoreCommandIntent.Checkout, EmotionReactionRules.ParseKoreanIntent("민수야 계산대 좀 봐줘"));
            Assert.AreEqual(StoreCommandIntent.Restock, EmotionReactionRules.ParseKoreanIntent("진열대에 물건 채워줘"));
            Assert.AreEqual(StoreCommandIntent.Rest, EmotionReactionRules.ParseKoreanIntent("힘들지 잠깐 쉬어"));
            Assert.AreEqual(StoreCommandIntent.Unsupported, EmotionReactionRules.ParseKoreanIntent("3번 테이블 서빙해줘"));
            Assert.AreEqual(StoreCommandIntent.Clarify, EmotionReactionRules.ParseKoreanIntent("저기 그거 좀 해봐"));
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
            tokenA.Dispose();
            Assert.IsTrue(counter.IsLocked);

            tokenB.Dispose();
            Assert.IsFalse(counter.IsLocked);
        }
    }
}
