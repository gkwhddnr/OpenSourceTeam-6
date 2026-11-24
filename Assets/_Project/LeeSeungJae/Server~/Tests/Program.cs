using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using EmployeeGateway;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GatewayChecks;

internal sealed partial class TestSuite
{
    internal const string KeyMarker = "offline-only-gateway-key-marker";
    private const string UtteranceMarker = "PRIVATE_UTTERANCE_FOR_TEST";
    private const string UpstreamMarker = "PRIVATE_UPSTREAM_DETAILS_FOR_TEST";
    private const string ExampleDialogue = "잠깐만요, 지금은 쉬고 싶어요.";
    private const string Classify = "/api/v1/employee/classify";
    private const string Dialogue = "/api/v1/employee/dialogue";
    private readonly FakeOpenAi upstream = new();
    private readonly CapturedLogs logs = new();
    private string? dialogueId;
    private string? cancelledId;
    private int passed;

    public static async Task<int> Main()
    {
        var suite = new TestSuite();
        try
        {
            // The standalone test process has its own environment. Both hosts use
            // the in-memory transport even if the developer normally has a real key.
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            await suite.RunHostAsync(suite.CheckNoKeyAsync);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", KeyMarker);
            await suite.RunHostAsync(suite.CheckGatewayAsync);
            suite.CheckLogs();
            Console.WriteLine($"PASS {suite.passed} offline gateway checks.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL after {suite.passed} checks. {ex.Message} ({ex.GetBaseException().Message})");
            return 1;
        }
    }

    private async Task RunHostAsync(Func<HttpClient, Task> checks)
    {
        string[] args = [
            "--Relay:Port=0",
            "--OpenAI:ClassificationModel=test-classifier",
            "--OpenAI:DialogueModel=test-dialogue",
            "--OpenAI:ClassificationMaxOutputTokens=150",
            "--OpenAI:DialogueMaxOutputTokens=90",
            "--OpenAI:PersonaModel=test-persona",
            "--OpenAI:PersonaMaxOutputTokens=220",
            "--OpenAI:PersonaTimeoutSeconds=2",
            "--OpenAI:ClassificationTimeoutSeconds=15",
            "--OpenAI:DialogueTimeoutSeconds=1"
        ];
        await using var app = GatewayHost.Build(args, builder =>
        {
            builder.Logging.ClearProviders().AddProvider(logs);
            builder.Services.AddHttpClient("OpenAI").ConfigurePrimaryHttpMessageHandler(() => upstream);
        });
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()!.Addresses;
            var address = new Uri(addresses.Single());
            Require(address.Host == "127.0.0.1", "Test host must listen on IPv4 loopback.");
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false })
                { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
            await checks(client);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private async Task CheckNoKeyAsync(HttpClient client)
    {
        JsonNode health = JsonNode.Parse(await client.GetStringAsync("/health"))!;
        Require(health.AsObject().Count == 1 && (string?)health["status"] == "ok", "Health must only expose liveness.");
        Require(upstream.Calls == 0, "Health contacted OpenAI.");
        Pass("health without a key");

        JsonObject request = Request();
        var response = await PostAsync(client, Classify, request.ToJsonString());
        ExpectError(response, 503, "SERVER_CONFIG", (string?)request["requestId"]);
        Require(upstream.Calls == 0, "Missing key contacted OpenAI.");
        Pass("missing server key");
        var persona = Resume();
        ExpectError(await PostAsync(client, EvaluatePersona, persona.ToJsonString()), 503, "SERVER_CONFIG", (string?)persona["requestId"]);
        Require(upstream.Calls == 0, "Missing key in persona evaluation contacted OpenAI.");
        Pass("persona missing server key");
    }

    private async Task CheckGatewayAsync(HttpClient client)
    {
        // Fixtures specify model outputs. This checks transport and policy, while
        // real-model Korean interpretation is covered by the manual gameplay matrix.
        (string utterance, string intent, string target, string tone, string reaction)[] cases = [
            ("과자 좀", "RESTOCK", "SNACK", "NEUTRAL", "ACCEPT_NORMAL"),
            ("라면 채워", "RESTOCK", "RAMEN", "NEUTRAL", "REFUSE_EMOTION"),
            ("병신아 라면 채워", "RESTOCK", "RAMEN", "INSULT", "ACCEPT_COMPLAIN"),
            ("병신아", "TALK", "NONE", "INSULT", "NONE"),
            ("쉬지 않고 일하면 힘들지?", "TALK", "NONE", "NEUTRAL", "NONE"),
            ("휴식이 끝나면 기분이 어때?", "TALK", "NONE", "NEUTRAL", "NONE"),
            ("고생했어", "TALK", "NONE", "PRAISE", "NONE"),
            ("아까는 미안해", "TALK", "NONE", "APOLOGY", "NONE"),
            ("빨리 계산해", "CASHIER", "DEFAULT", "URGENT", "SLACK_OFF"),
            ("청소해", "CLEAN", "DEFAULT", "NEUTRAL", "ACCEPT_NORMAL"),
            ("진열해줘", "CLARIFY", "NONE", "NEUTRAL", "NONE"),
            ("과자와 라면을 채워 줘", "CLARIFY", "NONE", "NEUTRAL", "NONE"),
            ("과자 버려", "UNSUPPORTED", "NONE", "NEUTRAL", "NONE"),
            ("저기로 가", "MOVE", "CLICKED", "NEUTRAL", "NONE"),
            ("그만해", "CANCEL", "NONE", "NEUTRAL", "NONE")
        ];
        foreach (var (utterance, intent, target, tone, reaction) in cases)
        {
            JsonObject request = Request(utterance);
            JsonObject expected = Decision(intent, target, tone, reaction);
            upstream.Output(expected.ToJsonString());
            int before = upstream.Calls;
            var response = await PostAsync(client, Classify, request.ToJsonString());
            expected["requestId"] = request["requestId"]!.DeepClone();
            Require(response.Status == 200 && JsonNode.DeepEquals(response.Body, expected),
                $"Wrong response for {intent}/{target}/{tone}/{reaction}.");
            Require(upstream.Calls == before + 1 && upstream.ReceivedTestKey, "Unexpected credentials or call count.");
            Require((string?)upstream.LastBody?["model"] == "test-classifier" &&
                    (int?)upstream.LastBody?["max_output_tokens"] == 150 &&
                    (bool?)upstream.LastBody?["store"] == false &&
                    (bool?)upstream.LastBody?["text"]?["format"]?["strict"] == true,
                "Classification model, storage or schema setting differs from contract.");
            JsonNode? input = JsonNode.Parse((string)upstream.LastBody!["input"]!);
            Require((string?)input?["utterance"] == utterance && JsonNode.DeepEquals(input?["employee"], request["employee"]),
                "Classification lost the original utterance or employee snapshot.");
            Pass($"classification {intent}/{target}/{tone}/{reaction}");
        }

        await CheckInvalidInputsAsync(client);
        await CheckInvalidOutputsAsync(client);
        await CheckDialogueAsync(client);
        await CheckUpstreamErrorsAsync(client);
        await CheckClientDisconnectAsync(client);
        await CheckRestCommandsAsync(client);
        await CheckPersonaAsync(client);
    }

    private async Task CheckInvalidInputsAsync(HttpClient client)
    {
        (string name, Action<JsonObject> mutate)[] invalid = [
            ("missing utterance", r => r.Remove("utterance")),
            ("blank utterance", r => r["utterance"] = "  "),
            ("C0 control in utterance", r => r["utterance"] = "라면\u001b채워"),
            ("long utterance", r => r["utterance"] = new string('가', 513)),
            ("missing id", r => r.Remove("requestId")),
            ("malformed id", r => r["requestId"] = "not-a-guid"),
            ("missing employee", r => r.Remove("employee")),
            ("null employee", r => r["employee"] = null),
            ("missing personality", r => r["employee"]!.AsObject().Remove("personality")),
            ("long personality", r => r["employee"]!["personality"] = new string('나', 257)),
            ("C0 control in personality", r => r["employee"]!["personality"] = "조심\u0000스러움"),
            ("missing stress", r => r["employee"]!.AsObject().Remove("stress")),
            ("negative stress", r => r["employee"]!["stress"] = -1),
            ("excess fatigue", r => r["employee"]!["fatigue"] = 101),
            ("fractional loyalty", r => r["employee"]!["loyalty"] = 35.5),
            ("invalid work role", r => r["employee"]!["currentWork"] = "MANAGER"),
            ("wrong moving type", r => r["employee"]!["isMoving"] = "false"),
            ("missing moving flag", r => r["employee"]!.AsObject().Remove("isMoving")),
            ("client model override", r => r["model"] = "client-selected"),
            ("client supplied instructions", r => r["instructions"] = "test override")
        ];
        int before = upstream.Calls;
        foreach (var (name, mutate) in invalid)
        {
            JsonObject request = Request();
            mutate(request);
            ExpectError(await PostAsync(client, Classify, request.ToJsonString()), 400, "INVALID_INPUT");
            Pass(name);
        }
        ExpectError(await PostAsync(client, Classify, "{invalid"), 400, "INVALID_INPUT");
        Pass("malformed JSON");
        string duplicate = Request().ToJsonString().Insert(1, "\"utterance\":\"override\",");
        ExpectError(await PostAsync(client, Classify, duplicate), 400, "INVALID_INPUT");
        Pass("duplicate property");
        ExpectError(await PostAsync(client, Classify, Request().ToJsonString(), "text/plain"), 415, "INVALID_INPUT");
        Pass("incorrect content type");
        ExpectError(await PostAsync(client, Classify, Request(new string('a', 100_000)).ToJsonString()), 413, "INVALID_INPUT");
        Pass("body size limit");
        ExpectError(await PostAsync(client, Dialogue, Request().ToJsonString()), 400, "INVALID_INPUT");
        Pass("missing final decision");
        JsonObject badDecision = Request();
        badDecision["decision"] = Decision("TALK", "NONE", "NEUTRAL", "ACCEPT_NORMAL");
        ExpectError(await PostAsync(client, Dialogue, badDecision.ToJsonString()), 400, "INVALID_INPUT");
        Pass("invalid final decision");
        Require(upstream.Calls == before, "Invalid client data reached the OpenAI transport.");
    }

    private async Task CheckInvalidOutputsAsync(HttpClient client)
    {
        JsonObject request = Request();
        string requestId = (string)request["requestId"]!;
        string json = request.ToJsonString();
        (string name, string output)[] invalid = [
            ("empty output", ""),
            ("non-JSON output", "RESTOCK|RAMEN|NEUTRAL|ACCEPT_NORMAL"),
            ("null output", "null"),
            ("missing decision fields", "{}"),
            ("unknown intent", Decision("FREE", "NONE", "NEUTRAL", "NONE").ToJsonString()),
            ("unknown target", Decision("RESTOCK", "MEDICINE", "NEUTRAL", "ACCEPT_NORMAL").ToJsonString()),
            ("unknown tone", Decision("RESTOCK", "RAMEN", "ANGRY", "SLACK_OFF").ToJsonString()),
            ("disabled mistake reaction", Decision("RESTOCK", "SNACK", "NEUTRAL", "MISTAKE").ToJsonString()),
            ("missing work reaction", Decision("CLEAN", "DEFAULT", "NEUTRAL", "NONE").ToJsonString()),
            ("clean with product target", Decision("CLEAN", "RAMEN", "NEUTRAL", "ACCEPT_NORMAL").ToJsonString()),
            ("move with work reaction", Decision("MOVE", "CLICKED", "URGENT", "SLACK_OFF").ToJsonString()),
            ("talk with product target", Decision("TALK", "RAMEN", "NEUTRAL", "NONE").ToJsonString())
        ];
        foreach (var (name, output) in invalid)
        {
            upstream.Output(output);
            ExpectError(await PostAsync(client, Classify, json), 502, "INVALID_RESPONSE", requestId);
            Pass(name);
        }
        foreach (var invalidEnvelope in new[] {
            "{not-json",
            """{"status":"incomplete","output":[]}""",
            """{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"test"}]}]}"""
        })
        {
            upstream.Reply(HttpStatusCode.OK, invalidEnvelope);
            ExpectError(await PostAsync(client, Classify, json), 502, "INVALID_RESPONSE", requestId);
            Pass("invalid Responses API envelope");
        }
    }

    private async Task CheckDialogueAsync(HttpClient client)
    {
        JsonObject request = Request(UtteranceMarker);
        request["decision"] = Decision("RESTOCK", "RAMEN", "INSULT", "REFUSE_EMOTION");
        string requestId = (string)request["requestId"]!;
        string json = request.ToJsonString();
        upstream.Output(ExampleDialogue);
        var response = await PostAsync(client, Dialogue, json);
        var expected = new JsonObject { ["requestId"] = requestId, ["dialogue"] = ExampleDialogue };
        Require(response.Status == 200 && JsonNode.DeepEquals(response.Body, expected), "Dialogue or request id changed.");
        JsonNode body = upstream.LastBody!;
        Require((string?)body["model"] == "test-dialogue" && (int?)body["max_output_tokens"] == 90 &&
                (bool?)body["store"] == false, "Incorrect dialogue settings.");
        string input = body["input"]!.ToString();
        Require(input.Contains(UtteranceMarker) && input.Contains("REFUSE_EMOTION"), "Dialogue input lost the final action or utterance.");
        Require(!body["instructions"]!.ToString().Contains(UtteranceMarker), "Player data was used as server instructions.");
        dialogueId = requestId;
        Pass("dialogue uses final action and correlates response");
        foreach (string bad in new[] { "", new string('가', 121), "hello", "네.\n아니요.", "<b>안녕</b>" })
        {
            upstream.Output(bad);
            ExpectError(await PostAsync(client, Dialogue, json), 502, "INVALID_RESPONSE", requestId);
            Pass("invalid dialogue");
        }
        upstream.WaitForCancellation();
        ExpectError(await PostAsync(client, Dialogue, json), 504, "UPSTREAM_TIMEOUT", requestId);
        Pass("OpenAI request deadline");
    }

    private async Task CheckUpstreamErrorsAsync(HttpClient client)
    {
        JsonObject request = Request(UtteranceMarker);
        string requestId = (string)request["requestId"]!;
        (int upstreamStatus, int expectedStatus, string expectedCode)[] errors = [
            (401, 502, "OPENAI_AUTH"), (403, 502, "MODEL_CONFIG"), (404, 502, "MODEL_CONFIG"),
            (429, 429, "RATE_LIMITED"), (503, 503, "UPSTREAM_UNAVAILABLE")
        ];
        foreach (var (upstreamStatus, expectedStatus, expectedCode) in errors)
        {
            var details = new JsonObject
            {
                ["error"] = new JsonObject { ["message"] = KeyMarker + " " + UpstreamMarker }
            };
            upstream.Reply((HttpStatusCode)upstreamStatus, details.ToJsonString());
            int before = upstream.Calls;
            ExpectError(await PostAsync(client, Classify, request.ToJsonString()), expectedStatus, expectedCode, requestId);
            Require(upstream.Calls == before + 1, "An upstream error caused a duplicate request.");
            Pass($"OpenAI HTTP {upstreamStatus}");
        }
    }

    private async Task CheckClientDisconnectAsync(HttpClient client)
    {
        var (started, cancelled) = upstream.WaitForCancellation();
        using var abort = new CancellationTokenSource();
        JsonObject request = Request();
        cancelledId = (string?)request["requestId"];
        Task<(int Status, JsonNode Body)> call = PostAsync(client, Classify, request.ToJsonString(), token: abort.Token);
        await started.WaitAsync(TimeSpan.FromSeconds(5));
        abort.Cancel();
        try
        {
            await call;
            throw new InvalidOperationException("Cancelled HTTP call completed.");
        }
        catch (OperationCanceledException) when (abort.IsCancellationRequested) { }
        // Shorter than the 15-second classification deadline, so a passing check
        // specifically establishes cancellation propagation from the HTTP client.
        await cancelled.WaitAsync(TimeSpan.FromSeconds(5));
        Pass("client disconnect cancels in-flight OpenAI work");
    }

    private void CheckLogs()
    {
        // Endpoints finish writing and logging asynchronously. Inspect after the
        // host has drained so even the disconnected request has its final entry.
        string capture = logs.Text;
        Require(!ContainsPrivateData(capture), "A sensitive marker was written to a server log.");
        string[] lines = capture.Split('\n');
        Require(lines.Any(line => line.Contains(dialogueId!) && line.Contains("code=OK") &&
                                  line.Contains("validation_ms") && line.Contains("openai_ms") && line.Contains("total_ms")),
            "Missing successful dialogue log, trace id or stage durations.");
        Require(lines.Any(line => line.Contains(cancelledId!) && line.Contains("code=CANCELLED")),
            "Missing cancellation log or trace id.");
        Pass("server logs contain timings and exclude private data");
        Require(lines.Any(line => line.Contains(personaId!) && line.Contains("operation=evaluate-persona") &&
            line.Contains("code=OK") && line.Contains("total_ms=")), "Missing persona trace.");
        Require(lines.Any(line => line.Contains(personaCancelledId!) && line.Contains("code=CANCELLED")), "Missing cancelled persona trace.");
        Pass("persona timings, correlation, cancellation and privacy");
    }

    private static JsonObject Request(string utterance = "라면 채워") => new()
    {
        ["requestId"] = Guid.NewGuid().ToString("D"),
        ["utterance"] = utterance,
        ["employee"] = new JsonObject
        {
            ["personality"] = "조심스러운 직원", ["stress"] = 25, ["fatigue"] = 10,
            ["loyalty"] = 70, ["currentWork"] = "Idle", ["isMoving"] = false
        }
    };

    private static JsonObject Decision(string intent, string target, string tone, string reaction) => new()
    {
        ["intent"] = intent, ["target"] = target, ["tone"] = tone, ["reaction"] = reaction
    };

    private static async Task<(int Status, JsonNode Body)> PostAsync(HttpClient client, string route,
        string body, string mediaType = "application/json", CancellationToken token = default)
    {
        using var content = new StringContent(body, Encoding.UTF8, mediaType);
        using var response = await client.PostAsync(route, content, token);
        string wire = await response.Content.ReadAsStringAsync(token);
        Require(response.Content.Headers.ContentType?.MediaType == "application/json",
            $"Expected JSON from {route} (HTTP {(int)response.StatusCode}).");
        return ((int)response.StatusCode, JsonNode.Parse(wire)!);
    }

    private static void ExpectError((int Status, JsonNode Body) response, int status, string code, string? requestId = null)
    {
        Require(response.Status == status, $"Expected HTTP {status}, received HTTP {response.Status} ({code}).");
        string? id = (string?)response.Body["requestId"];
        Require(Guid.TryParseExact(id, "D", out _) && (requestId == null || requestId == id), "Missing or mismatched error id.");
        JsonNode? error = response.Body["error"];
        Require((string?)error?["code"] == code && !string.IsNullOrWhiteSpace((string?)error?["message"]),
            $"Missing safe error message or incorrect error code (expected {code}).");
        Require(!ContainsPrivateData(response.Body.ToString()), "Private marker escaped in an error response.");
    }

    private static bool ContainsPrivateData(string value) =>
        value.Contains(KeyMarker) || value.Contains(UtteranceMarker) || value.Contains(UpstreamMarker) ||
        value.Contains(ExampleDialogue) || value.Contains(ResumeNameMarker) || value.Contains(ResumeTraitsMarker);

    private async Task CheckRestCommandsAsync(HttpClient client)
    {
        (string intent, string[] utterances)[] groups = [
            ("CANCEL", ["휴식해", "좀 쉬어", "잠깐 쉬어!", "그만하고 쉬어", "저기 가서 쉬어", "저기 가서 휴식"]),
            ("CLARIFY", ["쉬지 마.", "휴식하지 마.", "쉬는 건 취소하지 마.", "라면 채우고 쉬어.", "쉬지 말고 계산해.", "청소한 다음에 쉬어."])
        ];
        foreach (var (intent, utterances) in groups)
        foreach (string utterance in utterances)
        {
            var request = Request(utterance);
            // Supply a valid but wrong model decision so the test exercises the server's rule.
            var wrong = intent == "CANCEL" ? Decision("MOVE", "CLICKED", "URGENT", "NONE") :
                Decision("CANCEL", "NONE", "URGENT", "NONE");
            upstream.Output(wrong.ToJsonString());
            var response = await PostAsync(client, Classify, request.ToJsonString());
            var expected = Decision(intent, "NONE", "URGENT", "NONE");
            expected["requestId"] = request["requestId"]!.DeepClone();
            Require(response.Status == 200 && JsonNode.DeepEquals(response.Body, expected), "Rest command policy failed.");
            Pass($"rest command {utterance} => {intent}");
        }
        foreach (var (utterance, line) in new[] { ("그만해", "네, 멈출게요."), ("저기 가서 쉬어", "네, 잠깐 쉴게요.") })
        {
            var request = Request(utterance);
            request["decision"] = Decision("CANCEL", "NONE", "NEUTRAL", "NONE");
            upstream.Output(line);
            var response = await PostAsync(client, Dialogue, request.ToJsonString());
            string instructions = (string)upstream.LastBody!["instructions"]!;
            Require(response.Status == 200 && (string?)response.Body["dialogue"] == line &&
                instructions.Contains("잠깐 쉴게요") && instructions.Contains("멈출게요") &&
                instructions.Contains("현재 위치"), "Cancel dialogue lost rest/stop context.");
            Pass("confirmed cancel dialogue: " + utterance);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Pass(string name)
    {
        passed++;
        Console.WriteLine($"PASS {name}");
    }
}
