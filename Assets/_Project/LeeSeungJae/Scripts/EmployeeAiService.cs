using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.UI.Core;

namespace EmployeeVoice
{
    internal interface IEmployeeAiService
    {
        Task<EmployeeDecision> ClassifyAsync(EmployeeAiRequest request, CancellationToken cancellation);
        Task<string> DialogueAsync(EmployeeAiRequest request, CancellationToken cancellation);
    }

    internal sealed class EmployeeDecision
    {
        public readonly string Intent, Target, Tone, Reaction;

        public EmployeeDecision(string intent, string target, string tone, string reaction)
        {
            Intent = intent;
            Target = target;
            Tone = tone;
            Reaction = reaction;
        }
    }

    // Snapshot the state on the Unity thread. Responses and requests use explicit wire names for player builds.
    internal sealed class EmployeeAiRequest
    {
        public readonly string RequestId;
        public readonly byte[] Body;

        public EmployeeAiRequest(string requestId, string utterance, EmployeePersonality employee,
            StaffWorkRole currentWork, bool isMoving, EmployeeDecision decision = null)
        {
            if (requestId?.Length != 36 || !Guid.TryParseExact(requestId, "D", out Guid id) || id == Guid.Empty || employee == null ||
                !ValidText(utterance, 512) || !InRange(employee.stress) || !InRange(employee.fatigue) ||
                !InRange(employee.loyalty) || !Enum.IsDefined(typeof(StaffWorkRole), currentWork))
                throw new InvalidOperationException("발화는 1~512자여야 합니다. 발화와 직원 상태를 확인해 주세요.");

            string traits = string.IsNullOrWhiteSpace(employee.traits) ? employee.archetype.ToString() : employee.traits;
            if (!ValidText(traits, 256)) throw new InvalidOperationException("직원 성격은 1~256자로 입력해 주세요.");
            if (decision != null && !VoiceMovementIntegration.TryValidateDecision(decision.Intent, decision.Target,
                decision.Tone, decision.Reaction, out _))
                throw new InvalidOperationException("확정된 직원 행동을 확인해 주세요.");
            RequestId = requestId;
            using (var buffer = new MemoryStream())
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("requestId", requestId);
                json.WriteString("utterance", utterance);
                json.WriteStartObject("employee");
                json.WriteString("personality", traits);
                json.WriteNumber("stress", employee.stress);
                json.WriteNumber("fatigue", employee.fatigue);
                json.WriteNumber("loyalty", employee.loyalty);
                json.WriteString("currentWork", currentWork.ToString());
                json.WriteBoolean("isMoving", isMoving);
                json.WriteEndObject();
                if (decision != null)
                {
                    json.WriteStartObject("decision");
                    json.WriteString("intent", decision.Intent);
                    json.WriteString("target", decision.Target);
                    json.WriteString("tone", decision.Tone);
                    json.WriteString("reaction", decision.Reaction);
                    json.WriteEndObject();
                }
                json.WriteEndObject();
                json.Flush();
                Body = buffer.ToArray();
            }
        }

        private static bool InRange(int value) => value >= 0 && value <= 100;
        private static bool ValidText(string value, int limit) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
    }

    internal sealed class GatewayEmployeeAiService : IEmployeeAiService
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:5127";
        private const int MaxResponseBytes = 8192;
        private const string InvalidResponse = "중계 서버 응답 형식이 올바르지 않습니다. 다시 말씀해 주세요.";
        // Reuse sockets across scenes. In particular, never forward a local utterance through a proxy or redirect.
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false
        }) { Timeout = Timeout.InfiniteTimeSpan };
        private readonly Uri baseUri;

        public GatewayEmployeeAiService(string baseUrl)
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) && uri.UserInfo.Length == 0 &&
                uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.Host.Length != 0 &&
                (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp &&
                    (uri.Host == "127.0.0.1" || uri.Host == "localhost" || uri.Host == "[::1]"))))
                baseUri = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        }

        public async Task<EmployeeDecision> ClassifyAsync(EmployeeAiRequest request, CancellationToken cancellation)
        {
            JsonElement body = await SendAsync("classify", request.RequestId, request.Body, 20, cancellation).ConfigureAwait(false);
            if (body.EnumerateObject().Count() != 5) throw new InvalidOperationException(InvalidResponse);
            var decision = new EmployeeDecision(ReadString(body, "intent"), ReadString(body, "target"),
                ReadString(body, "tone"), ReadString(body, "reaction"));
            if (!VoiceMovementIntegration.TryValidateDecision(decision.Intent, decision.Target, decision.Tone, decision.Reaction, out _))
                throw new InvalidOperationException(InvalidResponse);
            return decision;
        }

        public async Task<string> DialogueAsync(EmployeeAiRequest request, CancellationToken cancellation)
        {
            JsonElement body = await SendAsync("dialogue", request.RequestId, request.Body, 10, cancellation).ConfigureAwait(false);
            if (body.EnumerateObject().Count() != 2 || !VoiceMovementIntegration.TryNormalizeDialogue(ReadString(body, "dialogue"), out string line))
                throw new InvalidOperationException(InvalidResponse);
            return line;
        }

        internal async Task<PersonaEvaluationResult> EvaluatePersonaAsync(string employeeName, string traits, CancellationToken cancellation)
        {
            var data = new PersonaEvaluationRequest(employeeName, traits);
            JsonElement body = await SendAsync("evaluate-persona", data.RequestId, data.Body, 20, cancellation).ConfigureAwait(false);
            return PersonaEvaluationResult.FromResponse(body, data);
        }

        private async Task<JsonElement> SendAsync(string operation, string requestId, byte[] requestBody, int seconds, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (baseUri == null)
                throw new InvalidOperationException("Gateway Base Url을 확인해 주세요. 같은 PC는 로컬 HTTP, 외부 서버는 HTTPS 주소를 사용합니다.");

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/v1/employee/" + operation)))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
                request.Content = new ByteArrayContent(requestBody);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                try
                {
                    using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    using (deadline.Token.Register(response.Dispose))
                    {
                        if (!string.Equals(response.Content?.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                            response.Content.Headers.ContentLength > MaxResponseBytes)
                            throw new InvalidOperationException(InvalidResponse);
                        ReadOnlyMemory<byte> json = await ReadBodyAsync(response.Content, deadline.Token).ConfigureAwait(false);
                        deadline.Token.ThrowIfCancellationRequested();
                        using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 }))
                        {
                            JsonElement body = document.RootElement;
                            if (body.ValueKind != JsonValueKind.Object ||
                                !string.Equals(ReadString(body, "requestId"), requestId, StringComparison.Ordinal))
                                throw new InvalidOperationException(InvalidResponse);
                            if (!response.IsSuccessStatusCode)
                            {
                                if (body.EnumerateObject().Count() != 2 || !body.TryGetProperty("error", out var error) ||
                                    error.ValueKind != JsonValueKind.Object || error.EnumerateObject().Count() != 2)
                                    throw new InvalidOperationException(InvalidResponse);
                                ReadString(error, "message");
                                throw new InvalidOperationException(ErrorMessage(ReadString(error, "code")));
                            }
                            return body.Clone();
                        }
                    }
                }
                catch (Exception) when (deadline.IsCancellationRequested)
                {
                    cancellation.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("중계 서버 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.");
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException)
                {
                    throw new InvalidOperationException("중계 서버에 연결할 수 없습니다. 서버 실행 여부와 Gateway Base Url을 확인해 주세요.");
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException(InvalidResponse);
                }
            }
        }

        private static async Task<ReadOnlyMemory<byte>> ReadBodyAsync(HttpContent content, CancellationToken cancellation)
        {
            using (var input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                // Read one extra byte to detect oversized chunked replies without trusting Content-Length.
                var bytes = new byte[MaxResponseBytes + 1];
                int count = 0;
                while (count < bytes.Length)
                {
                    int read = await input.ReadAsync(bytes, count, bytes.Length - count, cancellation).ConfigureAwait(false);
                    if (read == 0) break;
                    count += read;
                }
                if (count > MaxResponseBytes) throw new InvalidOperationException(InvalidResponse);
                return new ReadOnlyMemory<byte>(bytes, 0, count);
            }
        }

        private static string ReadString(JsonElement body, string field)
        {
            if (!body.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException(InvalidResponse);
            try { return value.GetString(); }
            catch (InvalidOperationException) { throw new InvalidOperationException(InvalidResponse); }
        }

        private static string ErrorMessage(string code)
        {
            switch (code)
            {
                case "INVALID_INPUT": return "발화 또는 직원 상태가 올바르지 않습니다. 입력을 확인해 주세요.";
                case "SERVER_CONFIG": return "중계 서버 관리자에게 AI 서비스 설정 확인을 요청해 주세요.";
                case "OPENAI_AUTH": return "AI 서비스 인증에 실패했습니다. 중계 서버 관리자에게 인증 설정 확인을 요청해 주세요.";
                case "MODEL_CONFIG": return "중계 서버의 모델 이름 또는 접근 권한을 확인해 주세요.";
                case "RATE_LIMITED": return "AI 사용량 또는 요청 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.";
                case "UPSTREAM_TIMEOUT": return "AI 서비스 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.";
                case "UPSTREAM_UNAVAILABLE": return "AI 서비스에 일시적인 문제가 있습니다. 잠시 후 다시 시도해 주세요.";
                case "INVALID_RESPONSE": return InvalidResponse;
                case "CANCELLED": return "중계 요청이 취소되었습니다.";
                default: return "중계 서버가 요청을 처리하지 못했습니다. 서버 상태를 확인해 주세요.";
            }
        }
    }
}
