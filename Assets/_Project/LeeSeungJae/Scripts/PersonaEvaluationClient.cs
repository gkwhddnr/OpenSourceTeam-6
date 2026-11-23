using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceTeam6.UI.Core;

namespace EmployeeVoice
{
    internal sealed class PersonaEvaluationRequest
    {
        public string RequestId { get; }
        public string EmployeeName { get; }
        public string Traits { get; }
        public byte[] Body { get; }

        public PersonaEvaluationRequest(string employeeName, string traits)
        {
            EmployeeName = ValidateInput(employeeName, 12, "직원 이름은 1~12자로 입력해 주세요.");
            Traits = ValidateInput(traits, 50, "직원 성격은 1~50자로 입력해 주세요.");
            RequestId = Guid.NewGuid().ToString("D");
            using (var buffer = new MemoryStream())
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("requestId", RequestId);
                json.WriteString("employeeName", EmployeeName);
                json.WriteString("traits", Traits);
                json.WriteEndObject();
                json.Flush();
                Body = buffer.ToArray();
            }
        }

        private static string ValidateInput(string value, int max, string message)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
                throw new InvalidOperationException(message);
            return value.Trim();
        }
    }

    // Immutable result bound to the exact validated input used for this request.
    public sealed class PersonaEvaluationResult
    {
        public string RequestId { get; private set; }
        public string EmployeeName { get; private set; }
        public string Traits { get; private set; }
        public int HourlyWageWon { get; private set; }
        public int Diligence { get; private set; }
        public int Laziness { get; private set; }
        public int Loyalty { get; private set; }
        public float WorkStressMultiplier { get; private set; }
        public float WorkFatigueMultiplier { get; private set; }
        public float WageSensitivityMultiplier { get; private set; }
        public string SummaryReason { get; private set; }

        private PersonaEvaluationResult() { }

        internal static PersonaEvaluationResult FromResponse(JsonElement json, PersonaEvaluationRequest request)
        {
            const string error = "이력서 심사 응답이 올바르지 않습니다. 다시 시도해 주세요.";
            if (json.ValueKind != JsonValueKind.Object) throw new InvalidOperationException(error);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty item in json.EnumerateObject())
                if (!names.Add(item.Name)) throw new InvalidOperationException(error);
            if (names.Count != 10 || ReadString(json, "requestId", error) != request.RequestId ||
                ReadString(json, "schemaVersion", error) != "1") throw new InvalidOperationException(error);
            string reason = ReadString(json, "summaryReason", error);
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 80 || reason.Any(char.IsControl))
                throw new InvalidOperationException(error);
            return new PersonaEvaluationResult
            {
                RequestId = request.RequestId, EmployeeName = request.EmployeeName, Traits = request.Traits,
                HourlyWageWon = ReadInt(json, "hourlyWageWon", 10030, 30000, error),
                Diligence = ReadInt(json, "diligence", 0, 100, error),
                Laziness = ReadInt(json, "laziness", 0, 100, error),
                Loyalty = ReadInt(json, "loyalty", 0, 100, error),
                WorkStressMultiplier = ReadMultiplier(json, "workStressMultiplier", error),
                WorkFatigueMultiplier = ReadMultiplier(json, "workFatigueMultiplier", error),
                WageSensitivityMultiplier = ReadMultiplier(json, "wageSensitivityMultiplier", error),
                SummaryReason = reason.Trim()
            };
        }

        private static string ReadString(JsonElement body, string field, string error)
        {
            if (!body.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException(error);
            return value.GetString();
        }

        private static int ReadInt(JsonElement body, string field, int min, int max, string error)
        {
            if (!body.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt32(out int number) || number < min || number > max)
                throw new InvalidOperationException(error);
            return number;
        }

        private static float ReadMultiplier(JsonElement body, string field, string error)
        {
            if (!body.TryGetProperty(field, out JsonElement value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDecimal(out decimal number) || number < 0.5m || number > 2m ||
                decimal.Truncate(number * 100m) != number * 100m)
                throw new InvalidOperationException(error);
            return (float)number;
        }

        public bool Matches(string employeeName, string traits) =>
            string.Equals(EmployeeName, employeeName?.Trim(), StringComparison.Ordinal) &&
            string.Equals(Traits, traits?.Trim(), StringComparison.Ordinal);

        public EmployeePersonaSetupData BuildPersona(EmployeePersonaSetupData baseline)
        {
            if (!Matches(baseline.EmployeeName, baseline.PersonalityTraits))
                throw new InvalidOperationException("이력서가 변경되어 심사 결과를 적용할 수 없습니다. 다시 심사해 주세요.");
            EmployeePersonalityProfile p = baseline.Profile;
            var profile = new EmployeePersonalityProfile(Diligence, Laziness, Loyalty,
                p.Archetype, p.InitialStress, WorkStressMultiplier, p.RestRecoveryMultiplier,
                p.TaskSwitchMultiplier, p.HarshToneMultiplier, p.PraiseRecoveryMultiplier,
                p.AmbiguousCommandPenalty, WageSensitivityMultiplier, p.MoveSpeedMultiplier);
            return new EmployeePersonaSetupData(EmployeeName, Traits, HourlyWageWon, profile);
        }
    }

    // UI owner calls EvaluateAsync before confirming, and Cancel on edit/close.
    // This object performs no UI changes or hiring by itself.
    public sealed class PersonaEvaluationClient : IDisposable
    {
        private readonly GatewayEmployeeAiService gateway;
        private readonly object sync = new object();
        private CancellationTokenSource pending;
        private bool disposed;

        public PersonaEvaluationClient(string gatewayBaseUrl)
        {
            gateway = new GatewayEmployeeAiService(gatewayBaseUrl);
        }

        public bool IsPending { get { lock (sync) return pending != null; } }

        public async Task<PersonaEvaluationResult> EvaluateAsync(string employeeName, string traits, CancellationToken cancellation = default)
        {
            CancellationTokenSource request;
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException(nameof(PersonaEvaluationClient));
                if (pending != null) throw new InvalidOperationException("이미 이력서를 심사하고 있습니다.");
                request = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                pending = request;
            }
            try
            {
                PersonaEvaluationResult result = await gateway.EvaluatePersonaAsync(employeeName, traits, request.Token).ConfigureAwait(false);
                request.Token.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                lock (sync) { if (ReferenceEquals(pending, request)) pending = null; }
                request.Dispose();
            }
        }

        public void Cancel()
        {
            CancellationTokenSource request;
            lock (sync) request = pending;
            try { request?.Cancel(); } catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            lock (sync) disposed = true;
            Cancel();
        }
    }
}
