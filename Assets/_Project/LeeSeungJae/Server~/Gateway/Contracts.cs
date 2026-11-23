using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace EmployeeGateway;

internal sealed record Employee(string Personality, int Stress, int Fatigue, int Loyalty, string CurrentWork, bool IsMoving)
{
    public bool IsValid() =>
        Contract.IsInputText(Personality, 256) && Stress is >= 0 and <= 100 && Fatigue is >= 0 and <= 100 &&
        Loyalty is >= 0 and <= 100 && CurrentWork is "Idle" or "Checkout" or "Restock" or "Other";
}

internal sealed record Decision(string Intent, string Target, string Tone, string Reaction)
{
    internal static readonly string[] Intents = ["RESTOCK", "CLEAN", "CASHIER", "MOVE", "CANCEL", "TALK", "CLARIFY", "UNSUPPORTED"];
    internal static readonly string[] Products = ["SNACK", "RAMEN", "DAILY_GOODS", "ICE_CREAM", "WALK_IN", "HOT_FOOD"];
    internal static readonly string[] Tones = ["NEUTRAL", "PRAISE", "APOLOGY", "URGENT", "INSULT"];
    internal static readonly string[] WorkReactions = ["ACCEPT_NORMAL", "ACCEPT_COMPLAIN", "SLACK_OFF", "REFUSE_EMOTION"];

    public bool IsValid() => Tones.Contains(Tone) && (Intent switch
    {
        "RESTOCK" => Products.Contains(Target) && WorkReactions.Contains(Reaction),
        "CLEAN" or "CASHIER" => Target == "DEFAULT" && WorkReactions.Contains(Reaction),
        "MOVE" => Target == "CLICKED" && Reaction == "NONE",
        "CANCEL" or "TALK" or "CLARIFY" or "UNSUPPORTED" => Target == "NONE" && Reaction == "NONE",
        _ => false
    });
}

internal sealed record EmployeeRequest(string RequestId, string Utterance, Employee Employee, Decision? Decision = null);

internal static class Contract
{
    public const int BodyLimit = 16 * 1024;
    public static readonly JsonDocumentOptions Document = new() { AllowDuplicateProperties = false, MaxDepth = 16 };
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // The input becomes text for the model. Preserve Hangul instead of spelling each syllable as a JSON escape.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.HangulSyllables,
            UnicodeRanges.HangulJamo, UnicodeRanges.HangulCompatibilityJamo),
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 16
    };

    public static bool HasText(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit;

    public static bool IsInputText(string value, int limit) => HasText(value, limit) && !value.Any(char.IsControl);

    public static string? FindRequestId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("requestId", out JsonElement id) ||
            id.ValueKind != JsonValueKind.String) return null;
        string? value;
        try { value = id.GetString(); }
        catch (InvalidOperationException) { return null; }
        return value?.Length == 36 && Guid.TryParseExact(value, "D", out Guid parsed) && parsed != Guid.Empty ? value : null;
    }

    public static EmployeeRequest ReadRequest(JsonElement root, bool dialogue)
    {
        // Classification has no client-selected result, model or instructions.
        if (FindRequestId(root) == null || root.TryGetProperty("decision", out _) != dialogue)
            throw GatewayError.InvalidInput();
        try
        {
            EmployeeRequest? request = root.Deserialize<EmployeeRequest>(Json);
            if (request == null || !IsInputText(request.Utterance, 512) || !request.Employee.IsValid() ||
                (dialogue && request.Decision?.IsValid() != true)) throw GatewayError.InvalidInput();
            return request;
        }
        catch (JsonException) { throw GatewayError.InvalidInput(); }
    }

    public static object ReadAnswer(string text, string requestId, bool dialogue)
    {
        if (dialogue)
        {
            if (!HasText(text, 120) || text.Any(char.IsControl) || text.IndexOfAny(['<', '>', '\u2028', '\u2029']) >= 0 ||
                text.Contains("```", StringComparison.Ordinal) || !text.Any(c => c is >= '가' and <= '힣'))
                throw GatewayError.InvalidResponse();
            return new { requestId, dialogue = text.Trim() };
        }

        try
        {
            var decision = JsonSerializer.Deserialize<Decision>(text, Json);
            if (decision?.IsValid() != true) throw GatewayError.InvalidResponse();
            return new { requestId, decision.Intent, decision.Target, decision.Tone, decision.Reaction };
        }
        catch (JsonException) { throw GatewayError.InvalidResponse(); }
    }
}

internal sealed class GatewayError(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static GatewayError InvalidInput(int status = 400) =>
        new(status, "INVALID_INPUT", "요청 형식, 입력 길이 및 직원 상태를 확인해 주세요.");
    public static GatewayError InvalidResponse() =>
        new(502, "INVALID_RESPONSE", "AI 응답 형식이 잘못되었습니다. 다시 말씀해 주세요.");
    public static GatewayError Configuration() =>
        new(503, "SERVER_CONFIG", "서버의 OPENAI_API_KEY 환경 변수와 OpenAI 설정을 확인한 뒤 서버를 다시 시작해 주세요.");
    public static GatewayError Unavailable() =>
        new(503, "UPSTREAM_UNAVAILABLE", "AI 서비스에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.");
    public static GatewayError Timeout() =>
        new(504, "UPSTREAM_TIMEOUT", "AI 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.");
    public static GatewayError Cancelled() => new(499, "CANCELLED", "요청이 취소되었습니다.");
}
