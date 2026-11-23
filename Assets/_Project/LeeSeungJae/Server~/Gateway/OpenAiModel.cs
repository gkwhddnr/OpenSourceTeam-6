using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;

namespace EmployeeGateway;

internal interface IEmployeeModel
{
    Task<string> GenerateAsync(EmployeeRequest request, bool dialogue, Stopwatch networkTime, CancellationToken cancellation);
}

internal sealed class OpenAiModel(IHttpClientFactory clients, IConfiguration config) : IEmployeeModel
{
    private const string Endpoint = "https://api.openai.com/v1/responses";
    private const int MaxResponseBytes = 64 * 1024;

    public async Task<string> GenerateAsync(EmployeeRequest request, bool dialogue, Stopwatch networkTime, CancellationToken cancellation)
    {
        string operation = dialogue ? "Dialogue" : "Classification";
        string? model = config["OpenAI:" + operation + "Model"];
        if (!Contract.HasText(model, 128) ||
            !int.TryParse(config["OpenAI:" + operation + "MaxOutputTokens"], out int limit) || limit is < 16 or > 2048 ||
            !int.TryParse(config["OpenAI:" + operation + "TimeoutSeconds"], out int seconds) || seconds is < 1 or > 120)
            throw GatewayError.Configuration();

        string? key;
        try { key = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim(); }
        catch (System.Security.SecurityException) { throw GatewayError.Configuration(); }
        if (!Contract.HasText(key, 1024) || key!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '+' or '/' or '=')))
            throw GatewayError.Configuration();

        // Keep server instructions and schema outside the user-provided JSON input.
        // The trace ID is only needed between the game and this relay.
        object gameState = dialogue ? new { request.Utterance, request.Employee, request.Decision }
                                    : new { request.Utterance, request.Employee };
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["instructions"] = dialogue ? EmployeePrompts.Dialogue : EmployeePrompts.Classify,
            ["input"] = JsonSerializer.Serialize(gameState, Contract.Json),
            ["store"] = false,
            ["max_output_tokens"] = limit
        };
        if (model == "gpt-6-luna") body["reasoning"] = new { effort = "none" };
        if (!dialogue) body["text"] = EmployeePrompts.DecisionFormat;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        using var http = clients.CreateClient("OpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        message.Content = JsonContent.Create(body, options: Contract.Json);
        try
        {
            networkTime.Start();
            using HttpResponseMessage response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw MapStatus(response.StatusCode);
            if (response.Content.Headers.ContentLength > MaxResponseBytes) throw GatewayError.InvalidResponse();
            await response.Content.LoadIntoBufferAsync(MaxResponseBytes, deadline.Token);
            networkTime.Stop();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline.Token), Contract.Document);
            return ExtractText(doc.RootElement);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw GatewayError.Timeout(); }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        { throw GatewayError.InvalidResponse(); }
        catch (HttpRequestException) { throw GatewayError.Unavailable(); }
        catch (IOException) { throw GatewayError.Unavailable(); }
        catch (JsonException) { throw GatewayError.InvalidResponse(); }
        catch (InvalidOperationException) { throw GatewayError.InvalidResponse(); }
        finally { networkTime.Stop(); }
    }

    private static GatewayError MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => new(502, "OPENAI_AUTH", "OpenAI 인증에 실패했습니다. 서버 관리자가 키 설정을 확인해 주세요."),
        HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity =>
            new(502, "MODEL_CONFIG", "서버의 OpenAI 모델 이름, 요청 설정 및 모델 접근 권한을 확인해 주세요."),
        HttpStatusCode.TooManyRequests => new(429, "RATE_LIMITED", "AI 사용량 또는 요청 한도에 도달했습니다. 잠시 후 다시 시도해 주세요."),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => GatewayError.Timeout(),
        _ => GatewayError.Unavailable()
    };

    private static string ExtractText(JsonElement root)
    {
        if (StringProperty(root, "status") != "completed" || !root.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array) throw GatewayError.InvalidResponse();
        string? text = null;
        foreach (var item in output.EnumerateArray())
        {
            string? type = StringProperty(item, "type");
            if (type == "reasoning") continue;
            if (type != "message" || !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                throw GatewayError.InvalidResponse();
            foreach (var part in content.EnumerateArray())
            {
                if (text != null || StringProperty(part, "type") != "output_text") throw GatewayError.InvalidResponse();
                text = StringProperty(part, "text");
                if (!Contract.HasText(text, 4096)) throw GatewayError.InvalidResponse();
            }
        }
        return text ?? throw GatewayError.InvalidResponse();
    }

    private static string? StringProperty(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var child) &&
        child.ValueKind == JsonValueKind.String ? child.GetString() : null;
}
