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
