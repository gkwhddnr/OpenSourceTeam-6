using System.Text.RegularExpressions;

namespace EmployeeGateway;

internal static class RestCommand
{
    // Only an entire, affirmative request can override a model decision with CANCEL.
    private const string RestDirective = @"(?:잠깐만?|조금|좀)?(?:쉬어(?:요|줘)?|쉬세요|휴식(?:해(?:요|줘)?|하세요)?)\z";
    private static readonly Regex Affirmative = new(@"\A(?:그만하고|(?:저기|거기|저쪽)가서)?" + RestDirective);
    private static readonly Regex Negated = new(
        @"(?:쉬지(?:는)?|휴식하지(?:는)?|(?:쉬는(?:건|걸|것은|것을)|휴식(?:을|은)?)(?:취소|중지)하지)(?:말고|(?:마|마요|마세요|말아줘|말아주세요)\z)");
    private static readonly Regex WorkThenRest = new(
        @"(?:채우고|(?:진열|보충|청소|계산|근무|일)(?:하고|한뒤|한다음에?|하고나서)|(?:업무|일|작업)이?끝나면)" + RestDirective);

    public static Decision Apply(string utterance, Decision model)
    {
        string text = string.Concat(utterance.Where(c => !char.IsWhiteSpace(c))).TrimEnd('.', '!', '。', '！');
        string? intent = null;
        if (Affirmative.IsMatch(text)) intent = "CANCEL";
        else if (Negated.IsMatch(text) || WorkThenRest.IsMatch(text)) intent = "CLARIFY";
        return intent == null ? model : new Decision(intent, "NONE", model.Tone, "NONE");
    }
}
