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

        [Test]
        public void StoreAudioRules_VolumeStateAndBgmResolution_FollowsScreenAndStressThresholds()
        {
            StoreAudioVolumeState vol = new StoreAudioVolumeState(0.5f, 0.8f, 0.6f, isBgmMuted: false);
            Assert.AreEqual(0.4f, vol.EffectiveBgmVolume, 0.0001f);
            Assert.AreEqual(0.3f, vol.EffectiveSfxVolume, 0.0001f);

            StoreAudioVolumeState muted = new StoreAudioVolumeState(0.5f, 0.8f, 0.6f, isBgmMuted: true);
            Assert.AreEqual(0f, muted.EffectiveBgmVolume, 0.0001f);
            Assert.AreEqual(0.3f, muted.EffectiveSfxVolume, 0.0001f);

            Assert.AreEqual(StoreBgmTrack.Lobby, StoreAudioRules.ResolveBgmForState(UiScreenMode.Lobby, 20f));
            Assert.AreEqual(StoreBgmTrack.DayNormal, StoreAudioRules.ResolveBgmForState(UiScreenMode.InGame, 79.9f));
            Assert.AreEqual(StoreBgmTrack.DayCrisis, StoreAudioRules.ResolveBgmForState(UiScreenMode.InGame, 80f));
            Assert.AreEqual(StoreBgmTrack.SettlementOrEnding, StoreAudioRules.ResolveBgmForState(UiScreenMode.Settlement, 90f));
        }

        [Test]
        public void StoreAudioRules_VoiceAndFeedbackSfxResolution_MapsCorrectClips()
        {
            Assert.IsTrue(StoreAudioRules.TryResolveVoiceStageSfx(
                VoiceProcessStage.Idle, VoiceProcessStage.Listening, out StoreSfxId startSfx));
            Assert.AreEqual(StoreSfxId.MicRecordStart, startSfx);

            Assert.IsTrue(StoreAudioRules.TryResolveVoiceStageSfx(
                VoiceProcessStage.Listening, VoiceProcessStage.Processing, out StoreSfxId stopSfx));
            Assert.AreEqual(StoreSfxId.MicRecordStop, stopSfx);

            Assert.IsFalse(StoreAudioRules.TryResolveVoiceStageSfx(
                VoiceProcessStage.Processing, VoiceProcessStage.Completed, out _));

            Assert.IsTrue(StoreAudioRules.TryResolveFeedbackSfx(CommandFeedbackKind.Accepted, out StoreSfxId acc));
            Assert.AreEqual(StoreSfxId.ReactionAccept, acc);

            Assert.IsTrue(StoreAudioRules.TryResolveFeedbackSfx(CommandFeedbackKind.RefusedByEmotion, out StoreSfxId refSfx));
            Assert.AreEqual(StoreSfxId.ReactionRefuseOrCrisis, refSfx);

            Assert.AreEqual(StoreSfxId.ReactionComplain, StoreAudioRules.ResolveReactionSfx(EmployeeReaction.AcceptComplain));
        }

        [Test]
        public void EmployeePersonaPresets_Preset1RandomAnd50CharClamp_FollowSpec()
        {
            EmployeePersonaSetupData preset1 = EmployeePersonaPresets.CreatePreset1();
            Assert.AreEqual("김민수", preset1.EmployeeName);
            Assert.LessOrEqual(preset1.PersonalityTraits.Length, EmployeePersonaPresets.MaxTraitLength);
            Assert.AreEqual(10030, preset1.HourlyWageWon);

            Assert.AreEqual("(0/50)", EmployeePersonaPresets.FormatTraitLengthCounter(string.Empty));
            string over50 = new string('가', 65);
            string clamped = EmployeePersonaPresets.ClampTraitInput(over50);
            Assert.AreEqual(50, clamped.Length);
            Assert.AreEqual("(50/50)", EmployeePersonaPresets.FormatTraitLengthCounter(over50));

            for (int i = 0; i < EmployeePersonaPresets.RandomPoolCount; i++)
            {
                EmployeePersonaSetupData candidate = EmployeePersonaPresets.GetRandomCandidate(i);
                Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.EmployeeName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.PersonalityTraits));
                Assert.LessOrEqual(candidate.PersonalityTraits.Length, EmployeePersonaPresets.MaxTraitLength);
            }
        }

        [Test]
        public void PersonaStressAndReactionRules_PresetsAndToneMultipliers_FollowPrototypeSpec()
        {
            EmployeePersonaSetupData p1 = EmployeePersonaPresets.CreatePreset1();
            EmployeePersonaSetupData p2 = EmployeePersonaPresets.CreatePreset2();
            EmployeePersonaSetupData p3 = EmployeePersonaPresets.CreatePreset3();

            Assert.AreEqual(PersonaArchetype.DiligentStandard, p1.Profile.Archetype);
            Assert.AreEqual(PersonaArchetype.LazyComplainer, p2.Profile.Archetype);
            Assert.AreEqual(PersonaArchetype.FragileTimid, p3.Profile.Archetype);

            // 1. 자유 입력 키워드 매핑 검증
            EmployeePersonalityProfile inferredLazy = EmployeePersonaPresets.InferProfileFromTraits("일하기 귀찮아하고 한숨을 자주 쉰다");
            Assert.AreEqual(PersonaArchetype.LazyComplainer, inferredLazy.Archetype);

            EmployeePersonalityProfile inferredTimid = EmployeePersonaPresets.InferProfileFromTraits("소심하고 상처를 잘 받는 유리멘탈");
            Assert.AreEqual(PersonaArchetype.FragileTimid, inferredTimid.Archetype);

            // 2. 김민수(프리셋 1)는 작업 중 업무 강제 전환 시 페널티가 기본(12)보다 1.6배(+19.2) 적용
            float switchDeltaP1 = StressRules.EvaluateCommandStressDelta(
                StoreCommandIntent.Restock,
                UtteranceTone.Neutral,
                StaffWorkRole.Checkout,
                StaffWorkRole.Restock,
                p1.Profile);
            Assert.AreEqual(19.2f, switchDeltaP1, 0.01f);

            // 3. 최소심(프리셋 3)은 강압적 말투(+18 * 1.7 = +30.6) 및 칭찬 말투(-8 * 1.6 = -12.8)에 민감하게 반응
            Assert.AreEqual(UtteranceTone.HarshOrNagging, EmotionReactionRules.ParseKoreanTone("빨리 계산대 안 가?!"));
            float harshDeltaP3 = StressRules.EvaluateCommandStressDelta(
                StoreCommandIntent.Checkout,
                UtteranceTone.HarshOrNagging,
                StaffWorkRole.Idle,
                StaffWorkRole.Checkout,
                p3.Profile);
            Assert.AreEqual(30.6f, harshDeltaP3, 0.01f);

            Assert.AreEqual(UtteranceTone.PoliteOrPraise, EmotionReactionRules.ParseKoreanTone("고마워 계산대 좀 부탁할게"));
            float praiseDeltaP3 = StressRules.EvaluateCommandStressDelta(
                StoreCommandIntent.Checkout,
                UtteranceTone.PoliteOrPraise,
                StaffWorkRole.Idle,
                StaffWorkRole.Checkout,
                p3.Profile);
            Assert.AreEqual(-12.8f, praiseDeltaP3, 0.01f);

            // 4. 박태만(프리셋 2)은 정산 시 시급 인상(-25 * 1.5 = -37.5) / 삭감(+25 * 1.5 = +37.5)에 더 크게 반응
            Assert.AreEqual(-37.5f, StressRules.EvaluateWageAdjustmentStressDelta(WageAdjustmentOption.Raise, p2.Profile), 0.01f);
            Assert.AreEqual(37.5f, StressRules.EvaluateWageAdjustmentStressDelta(WageAdjustmentOption.Cut, p2.Profile), 0.01f);

            // 5. 과부하 구간(70~89)에서 실수(Mistake) 가중치 존재 검증
            EmotionReactionWeights overloadWeights = EmotionReactionRules.ComputeWeights(
                StoreCommandIntent.Checkout, stress: 75f, fatigue: 50f, p3.Profile);
            Assert.Greater(overloadWeights.Mistake, 20f);
        }

        [Test]
        public void StoreEndingRules_FourEndingsAndPersonaNarrative_FollowSpec()
        {
            EmployeePersonaSetupData p1 = EmployeePersonaPresets.CreatePreset1();
            EmployeePersonaSetupData p2 = EmployeePersonaPresets.CreatePreset2();
            EmployeePersonaSetupData p3 = EmployeePersonaPresets.CreatePreset3();

            // 1. 노말엔딩 1: 목표 달성 + 스트레스 완화 (< 60)
            StoreEndingType e1 = StoreEndingRules.EvaluateEnding(
                finalBalanceWon: 85000, targetBalanceWon: 80000, endingStress: 32f, angerRefusalCount: 0);
            Assert.AreEqual(StoreEndingType.NormalEnding1_SuccessLowStress, e1);
            StoreEndingReport r1 = StoreEndingRules.BuildReport(e1, p1, 32f, 85000, 80000);
            StringAssert.Contains("노말엔딩 1", r1.HeaderTitle);
            StringAssert.Contains("김민수", r1.NarrativeBodyText);
            StringAssert.Contains("32/100", r1.NarrativeBodyText);

            // 2. 노말엔딩 2: 목표 달성 + 스트레스 악화 (>= 60)
            StoreEndingType e2 = StoreEndingRules.EvaluateEnding(
                finalBalanceWon: 82000, targetBalanceWon: 80000, endingStress: 84f, angerRefusalCount: 2);
            Assert.AreEqual(StoreEndingType.NormalEnding2_SuccessHighStress, e2);
            StoreEndingReport r2 = StoreEndingRules.BuildReport(e2, p2, 84f, 82000, 80000);
            StringAssert.Contains("노말엔딩 2", r2.HeaderTitle);
            StringAssert.Contains("박태만", r2.NarrativeBodyText);
            StringAssert.Contains("84/100", r2.NarrativeBodyText);

            // 3. 실패 엔딩 1: 일차 목표 금액 미달
            StoreEndingType e3 = StoreEndingRules.EvaluateEnding(
                finalBalanceWon: 64000, targetBalanceWon: 80000, endingStress: 45f, angerRefusalCount: 0);
            Assert.AreEqual(StoreEndingType.BadEnding1_TargetShortfall, e3);
            StoreEndingReport r3 = StoreEndingRules.BuildReport(e3, p3, 45f, 64000, 80000);
            StringAssert.Contains("실패 엔딩 1", r3.HeaderTitle);
            StringAssert.Contains("최소심", r3.NarrativeBodyText);

            // 4. 실패 엔딩 2: 알바 스트레스 100 초과 (최우선 판정)
            StoreEndingType e4 = StoreEndingRules.EvaluateEnding(
                finalBalanceWon: 90000, targetBalanceWon: 80000, endingStress: 100f, angerRefusalCount: 3);
            Assert.AreEqual(StoreEndingType.BadEnding2_StressOverflow, e4);
            StoreEndingReport r4 = StoreEndingRules.BuildReport(e4, p3, 100f, 90000, 80000);
            StringAssert.Contains("실패 엔딩 2", r4.HeaderTitle);
            StringAssert.Contains("100/100", r4.NarrativeBodyText);
        }
    }
}
