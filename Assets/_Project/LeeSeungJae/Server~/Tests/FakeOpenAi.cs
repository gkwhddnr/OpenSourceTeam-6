using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace GatewayChecks;

// Only the OpenAI transport is scripted. The HTTP host, prompt builder, adapter,
// response parser, error mapper and client disconnect handling all run normally.
internal sealed class FakeOpenAi : HttpMessageHandler
{
    private Func<CancellationToken, Task<HttpResponseMessage>>? pending;
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public JsonNode? LastBody { get; private set; }
    public bool ReceivedTestKey { get; private set; }

    public void Reply(HttpStatusCode status, string json)
    {
        pending = _ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    public void Output(string text)
    {
        Reply(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } }
        }));
    }

    // A signal from the fake proves both request start and token propagation.
    public (Task Started, Task Cancelled) WaitForCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = async token =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unexpected completion");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled.SetResult();
                throw;
            }
        };
        return (started.Task, cancelled.Task);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref calls);
        if (request.Method != HttpMethod.Post || request.RequestUri?.AbsoluteUri != "https://api.openai.com/v1/responses")
            throw new InvalidOperationException("Unexpected OpenAI request destination");
        ReceivedTestKey = request.Headers.Authorization?.Scheme == "Bearer" &&
                          request.Headers.Authorization.Parameter == TestSuite.KeyMarker;
        LastBody = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token));
        var respond = Interlocked.Exchange(ref pending, null)
            ?? throw new InvalidOperationException("Unexpected extra OpenAI call");
        return await respond(token);
    }
}

internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<string> entries = new();
    public string Text => string.Join("\n", entries);
    public ILogger CreateLogger(string categoryName) => new EntryLogger(entries, categoryName);
    public void Dispose() { }

    private sealed class EntryLogger(ConcurrentQueue<string> entries, string category) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(category + " " + formatter(state, exception) + " " + exception);
    }
}
