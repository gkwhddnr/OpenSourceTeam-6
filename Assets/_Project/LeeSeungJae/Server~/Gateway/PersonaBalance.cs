using System.Globalization;
using System.Text.Json;

namespace EmployeeGateway;

internal sealed record PersonaRequest(string RequestId, string EmployeeName, string Traits)
{
    public static PersonaRequest Read(JsonElement root)
    {
        if (Contract.FindRequestId(root) == null) throw GatewayError.InvalidInput();
        var request = root.Deserialize<PersonaRequest>(Contract.Json);
        if (request == null || !Contract.IsInputText(request.EmployeeName, 12) || !Contract.IsInputText(request.Traits, 50))
            throw GatewayError.InvalidInput();
        return request;
    }
}

internal sealed record PersonaAnalysis(string ClaimStyle, int Diligence, int Laziness, int Loyalty,
    decimal WorkStressMultiplier, decimal WorkFatigueMultiplier, decimal WageSensitivityMultiplier);

internal sealed record PersonaResult(string RequestId, string SchemaVersion, int HourlyWageWon, int Diligence,
    int Laziness, int Loyalty, decimal WorkStressMultiplier, decimal WorkFatigueMultiplier,
    decimal WageSensitivityMultiplier, string SummaryReason);

// Provisional game balance for schema v1. All arithmetic runs on one validated model analysis.
internal static class PersonaBalance
{
    public const int MinWage = 10030, MaxWage = 30000, SummaryLimit = 80;
    public const int MinStat = 0, MaxStat = 100;
    public const decimal MinMultiplier = 0.5m, MaxMultiplier = 2m;
    internal static readonly string[] Styles = ["ORDINARY", "ONE_SIDED", "UNREALISTIC", "CONTRADICTORY"];

    private static bool StatsInRange(int diligence, int laziness, int loyalty) =>
        diligence is >= MinStat and <= MaxStat && laziness is >= MinStat and <= MaxStat && loyalty is >= MinStat and <= MaxStat;

    private static bool MultiplierInRange(decimal value) => value is >= MinMultiplier and <= MaxMultiplier;
    private static decimal RoundMultiplier(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static PersonaResult Evaluate(string json, string requestId)
    {
        PersonaAnalysis? analysis;
        try { analysis = JsonSerializer.Deserialize<PersonaAnalysis>(json, Contract.Json); }
        catch (JsonException) { throw GatewayError.InvalidResponse(); }
        if (analysis == null || !Styles.Contains(analysis.ClaimStyle) ||
            !StatsInRange(analysis.Diligence, analysis.Laziness, analysis.Loyalty) ||
            !MultiplierInRange(analysis.WorkStressMultiplier) || !MultiplierInRange(analysis.WorkFatigueMultiplier) ||
            !MultiplierInRange(analysis.WageSensitivityMultiplier)) throw GatewayError.InvalidResponse();

        int diligence = analysis.Diligence, laziness = analysis.Laziness, loyalty = analysis.Loyalty;
        decimal stress = analysis.WorkStressMultiplier, fatigue = analysis.WorkFatigueMultiplier,
            sensitivity = analysis.WageSensitivityMultiplier;
        if (analysis.ClaimStyle == "CONTRADICTORY")
        {
            // Pull each conflicting assessment halfway toward neutral. Integer midpoints round down.
            diligence = (diligence + 50) / 2;
            laziness = (laziness + 50) / 2;
            loyalty = (loyalty + 50) / 2;
            stress = (stress + 1m) / 2;
            fatigue = (fatigue + 1m) / 2;
            sensitivity = (sensitivity + 1m) / 2;
        }
        bool unrealistic = analysis.ClaimStyle == "UNREALISTIC";
        if (unrealistic)
        {
            diligence = Math.Min(90, diligence);
            laziness = Math.Max(10, laziness);
            loyalty = Math.Min(90, loyalty);
        }
        stress = RoundMultiplier(stress);
        fatigue = RoundMultiplier(fatigue);
        sensitivity = RoundMultiplier(sensitivity);
        bool strong = diligence >= 80 || laziness <= 20 || loyalty >= 80 ||
            Math.Min(stress, Math.Min(fatigue, sensitivity)) <= 0.75m;
        bool premium = strong || unrealistic || analysis.ClaimStyle == "ONE_SIDED";
        if (premium) sensitivity = Math.Max(sensitivity, unrealistic ? 1.25m : 1.10m);

        // Each favorable stat point, or 0.01 below a neutral multiplier, costs 50 won/hour.
        decimal benefits = Math.Max(0, diligence - 50) + Math.Max(0, 50 - laziness) + Math.Max(0, loyalty - 50)
            + 100m * (Math.Max(0, 1 - stress) + Math.Max(0, 1 - fatigue) + Math.Max(0, 1 - sensitivity));
        int floor = unrealistic ? 20000 : premium ? 16000 : MinWage;
        int wage = Math.Clamp(MinWage + (int)(50 * benefits), floor, MaxWage);

        string merit = diligence > 50 ? "성실도가 높고" : laziness < 50 ? "게으름이 적고" :
            loyalty > 50 ? "충성도가 높고" : stress < 1 ? "업무 스트레스가 더디게 쌓이고" :
            fatigue < 1 ? "업무 피로가 더디게 쌓이고" : sensitivity < 1 ? "시급 변화에 둔감하고" : "업무 성향을 종합했고";
        decimal largestCost = Math.Max(stress, Math.Max(fatigue, sensitivity));
        string cost = largestCost <= 1 ? "" : stress == largestCost ? " 업무 스트레스가 빨리 쌓이며" :
            fatigue == largestCost ? " 업무 피로가 빨리 쌓이며" : " 시급 변화에 민감하며";
        string reason = $"{merit}{cost} 시급은 {wage.ToString("N0", CultureInfo.InvariantCulture)}원입니다.";
        if (!StatsInRange(diligence, laziness, loyalty) || !MultiplierInRange(stress) || !MultiplierInRange(fatigue) ||
            !MultiplierInRange(sensitivity) || !Contract.IsInputText(reason, SummaryLimit)) throw GatewayError.InvalidResponse();
        return new PersonaResult(requestId, "1", wage, diligence, laziness, loyalty, stress, fatigue, sensitivity, reason);
    }
}
