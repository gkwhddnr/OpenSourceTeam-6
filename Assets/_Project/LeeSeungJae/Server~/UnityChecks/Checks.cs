using System;
using System.Threading;
using System.Threading.Tasks;
using EmployeeVoice;
using OpenSourceTeam6.UI.Core;

internal static class Checks
{
    private static int passed;

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Failed: " + label);
        passed++;
    }

    private static EmployeeAiRequest Request(string utterance, EmployeeDecision decision = null)
    {
        var employee = new EmployeePersonality { traits = "조심스러움", stress = 35 };
        return new EmployeeAiRequest(Guid.NewGuid().ToString("D"), utterance, employee,
            StaffWorkRole.Restock, true, decision);
    }

    private static async Task ExpectRejected(Func<Task> operation, string label, string messageFragment)
    {
        try { await operation(); }
        catch (InvalidOperationException ex)
        {
            Check(ex.Message.Contains(messageFragment) && !ex.Message.Contains("PRIVATE"), label);
            return;
        }
        throw new InvalidOperationException("Expected rejection: " + label);
    }

    public static async Task Main(string[] args)
    {
        var service = new GatewayEmployeeAiService(args[0]);
        var token = CancellationToken.None;
        var proposal = await service.ClassifyAsync(Request("병신아 라면 채워"), token);
        Check(proposal.Intent == "RESTOCK" && proposal.Target == "RAMEN" &&
            proposal.Tone == "INSULT" && proposal.Reaction == "ACCEPT_COMPLAIN", "classification mapping");
        var final = new EmployeeDecision("RESTOCK", "RAMEN", "INSULT", "REFUSE_EMOTION");
        Check(await service.DialogueAsync(Request("병신아 라면 채워", final), token) == "지금은 못 하겠어요.",
            "dialogue carries the chosen action");
        await ExpectRejected(() => service.ClassifyAsync(Request("다른 요청"), token), "correlation", "응답 형식");
        await ExpectRejected(() => service.ClassifyAsync(Request("잘못된 조합"), token), "invalid target", "응답 형식");
        await ExpectRejected(() => service.ClassifyAsync(Request("한도 확인"), token), "redacted 429", "요청 한도");
        await ExpectRejected(() => service.DialogueAsync(Request("빈 대사", final), token),
            "empty line rejected for fallback", "응답 형식");
        var stopped = new GatewayEmployeeAiService(args[1]);
        await ExpectRejected(() => stopped.ClassifyAsync(Request("연결 확인"), token), "server stopped", "연결할 수 없습니다");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool observed = false;
            try { await service.ClassifyAsync(Request("취소된 요청"), cancelled.Token); }
            catch (OperationCanceledException) { observed = true; }
            Check(observed, "cancellation");
        }
        using (var personaClient = new PersonaEvaluationClient(args[0]))
        {
            var persona = await personaClient.EvaluateAsync("김민수", "성실하지만 예민하다", token);
            Check(persona.HourlyWageWon == 16000 && persona.Diligence == 85 && persona.Laziness == 35 &&
                persona.Loyalty == 60 && Math.Abs(persona.WorkStressMultiplier - 1.25f) < 0.001f &&
                Math.Abs(persona.WorkFatigueMultiplier - 1.15f) < 0.001f && persona.SummaryReason.Contains("16,000"),
                "persona evaluation response");
            var originalProfile = EmployeePersonalityProfile.CreatePreset3Timid();
            var baseline = new EmployeePersonaSetupData("김민수", "성실하지만 예민하다", 10030, originalProfile);
            var prepared = persona.BuildPersona(baseline);
            Check(prepared.HourlyWageWon == 16000 && prepared.Profile.Archetype == originalProfile.Archetype &&
                prepared.Profile.Diligence == 85 && prepared.Profile.Laziness == 35 && prepared.Profile.Loyalty == 60 &&
                Math.Abs(prepared.Profile.WorkStressMultiplier - 1.25f) < 0.001f &&
                Math.Abs(prepared.Profile.WageSensitivityMultiplier - 1.1f) < 0.001f &&
                prepared.Profile.RestRecoveryMultiplier == originalProfile.RestRecoveryMultiplier,
                "evaluated profile preserves non-evaluated fields");
            Check(Math.Abs(StressRules.EvaluateContinuousStressDelta(StaffWorkRole.Restock, false, prepared.Profile,
                EmotionStatRates.CreateDemoDefault(), 1f) - 1.5f) < 0.001f, "evaluated work stress multiplier");
            bool staleRejected = false;
            try { persona.BuildPersona(new EmployeePersonaSetupData("김민수", "수정된 성격")); }
            catch (InvalidOperationException) { staleRejected = true; }
            Check(staleRejected, "changed input rejects stale evaluation");
            foreach (string invalid in new[] { "버전 오류", "시급 오류", "배율 오류", "누락 오류", "요청 오류" })
                await ExpectRejected(() => personaClient.EvaluateAsync("김민수", invalid), "invalid persona " + invalid, "응답");
            await ExpectRejected(() => personaClient.EvaluateAsync(" ", "성실함"), "empty name", "이름");
            await ExpectRejected(() => personaClient.EvaluateAsync("김민수", new string('가', 51)), "long traits", "성격");
            var slow = personaClient.EvaluateAsync("김민수", "심사지연");
            Check(personaClient.IsPending, "pending evaluation");
            await ExpectRejected(() => personaClient.EvaluateAsync("김민수", "두 번째"), "duplicate submission", "이미");
            personaClient.Cancel();
            bool wasCancelled = false;
            try { await slow; } catch (OperationCanceledException) { wasCancelled = true; }
            Check(wasCancelled && !personaClient.IsPending, "persona cancellation and cleanup");
        }
        foreach (string rest in new[] { "휴식해", "좀 쉬어", "잠깐 쉬어", "그만하고 쉬어", "저기 가서 휴식", "저기 가서 쉬어", "민수야 쉬어" })
            Check(VoiceMovementIntegration.IsRestCommand(rest), "rest command " + rest);
        foreach (string notRest in new[] { "쉬지 마", "휴식하지 마", "쉬는 건 취소하지 마", "라면 채우고 쉬어", "쉬지 말고 계산해", "청소한 다음에 쉬어" })
            Check(!VoiceMovementIntegration.IsRestCommand(notRest), "unsafe rest command " + notRest);
        Check(!VoiceMovementIntegration.IsMixedCommand("그만하고 쉬어"), "affirmative rest is not mixed");
        Check(VoiceMovementIntegration.IsMixedCommand("라면 채우고 나서 쉬어") ||
              !VoiceMovementIntegration.IsRestCommand("라면 채우고 쉬어"), "work then rest is not immediate");
        var rates = EmotionStatRates.CreateDemoDefault();
        var profile = EmployeePersonalityProfile.CreatePreset1Diligent();
        Check(Math.Abs(VoiceMovementIntegration.EvaluateFatigueDelta(false, rates, profile, 1.25f, 1f) - 1f) < 0.001f,
            "work fatigue multiplier");
        Check(Math.Abs(VoiceMovementIntegration.EvaluateFatigueDelta(true, rates, profile, 1.25f, 1f) -
            VoiceMovementIntegration.EvaluateFatigueDelta(true, rates, profile, 1f, 1f)) < 0.001f,
            "work multiplier does not alter rest recovery");
        Check(VoiceMovementIntegration.IsCancelCommand("민수야 그만해"), "immediate cancellation");
        Check(!VoiceMovementIntegration.IsCancelCommand("취소하지마"), "negated cancellation");
        Check(VoiceMovementIntegration.IsMixedCommand("그만하고 라면 채워"), "mixed command");
        Check(VoiceMovementIntegration.HasNegation("계산하지마"), "negated work");
        Check(VoiceMovementIntegration.IsUnspecifiedRestockCommand("진열해줘", "민수"), "missing category");
        var employee = new EmployeePersonality { stress = 20, archetype = EmployeeArchetype.Standard };
        Check(VoiceMovementIntegration.SelectTeamReaction(employee, EmployeeAction.ACCEPT_COMPLAIN, 0.1)
            == EmployeeAction.ACCEPT_NORMAL, "proposal does not select final action");
        Check(VoiceMovementIntegration.SelectTeamReaction(employee, EmployeeAction.ACCEPT_COMPLAIN, 0.87)
            == EmployeeAction.ACCEPT_COMPLAIN, "proposal weight affects selection");
        Check(VoiceMovementIntegration.SelectTeamReaction(employee, EmployeeAction.ACCEPT_NORMAL, 0.87)
            == EmployeeAction.ACCEPT_NORMAL, "same sample with different proposal");
        bool mistakeSelected = false;
        foreach (EmployeeArchetype archetype in Enum.GetValues(typeof(EmployeeArchetype)))
        foreach (var stress in new[] { 0, 39, 40, 69, 70, 89, 90, 100 })
        {
            employee.archetype = archetype;
            employee.stress = stress;
            for (int sample = 0; sample < 100; sample++)
                mistakeSelected |= VoiceMovementIntegration.SelectTeamReaction(employee,
                    EmployeeAction.ACCEPT_NORMAL, sample / 100.0) == EmployeeAction.MISTAKE;
        }
        Check(!mistakeSelected, "disabled mistake across archetypes and stress bands");
        Console.WriteLine("Unity managed checks passed: " + passed);
    }
}
