using System.Diagnostics;
using System.Net;
using System.Text.Json;
using EmployeeGateway;

await using var host = GatewayHost.Build(args);
await host.RunAsync();

namespace EmployeeGateway
{
    public static class GatewayHost
    {
        public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? customize = null)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Production
            });
            // Framework request logs can include untrusted paths, queries or header values.
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
            builder.Services.AddHttpClient("OpenAI", client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false, UseCookies = false, ActivityHeadersPropagator = null
                })
                .RemoveAllLoggers();
            builder.Services.AddSingleton<IEmployeeModel, OpenAiModel>();
            customize?.Invoke(builder);

            if (!int.TryParse(builder.Configuration["Relay:Port"], out int port) || port is < 0 or > 65535)
                throw new InvalidOperationException("Relay:Port를 0~65535 범위에서 설정해 주세요.");
            builder.WebHost.PreferHostingUrls(false);
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Keep the local relay local even if system-wide ASPNETCORE_URLS or
                // Kestrel:Endpoints happens to be set for another application.
                options.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
                options.Listen(IPAddress.Loopback, port);
                options.Limits.MaxRequestBodySize = Contract.BodyLimit;
                options.AddServerHeader = false;
            });
            var app = builder.Build();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapPost("/api/v1/employee/classify", (HttpContext ctx) => HandleAsync(ctx, "classify"));
            app.MapPost("/api/v1/employee/dialogue", (HttpContext ctx) => HandleAsync(ctx, "dialogue"));
            app.MapPost("/api/v1/employee/evaluate-persona", (HttpContext ctx) => HandleAsync(ctx, "evaluate-persona"));
            return app;
        }

        private static async Task HandleAsync(HttpContext ctx, string operation)
        {
            string requestId = Guid.NewGuid().ToString("D");
            var total = Stopwatch.StartNew();
            var validation = Stopwatch.StartNew();
            var upstream = new Stopwatch();
            string code = "OK";
            int status = 200;
            var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EmployeeGateway.Request");
            var lifetime = ctx.RequestServices.GetRequiredService<IHostApplicationLifetime>();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, lifetime.ApplicationStopping);
            CancellationToken cancellation = linked.Token;
            object payload;
            try
            {
                try
                {
                    // A page on a third-party hostname redirected to the loopback address cannot use our relay.
                    if (ctx.Request.Host.Host != "127.0.0.1" && !string.Equals(ctx.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                        throw GatewayError.InvalidInput();
                    if (!ctx.Request.HasJsonContentType()) throw GatewayError.InvalidInput(415);
                    if (ctx.Request.ContentLength > Contract.BodyLimit) throw GatewayError.InvalidInput(413);
                    using var document = await JsonDocument.ParseAsync(ctx.Request.Body, Contract.Document, cancellation);
                    requestId = Contract.FindRequestId(document.RootElement) ?? requestId;
                    IEmployeeModel model = ctx.RequestServices.GetRequiredService<IEmployeeModel>();
                    cancellation.ThrowIfCancellationRequested();
                    if (operation == "evaluate-persona")
                    {
                        PersonaRequest request = PersonaRequest.Read(document.RootElement);
                        validation.Stop();
                        string analysis = await model.EvaluateAsync(request.Traits, upstream, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        payload = PersonaBalance.Evaluate(analysis, requestId);
                    }
                    else
                    {
                        bool dialogue = operation == "dialogue";
                        EmployeeRequest request = Contract.ReadRequest(document.RootElement, dialogue);
                        validation.Stop();
                        string output = await model.GenerateAsync(request, dialogue, upstream, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        payload = Contract.ReadAnswer(output, requestId, dialogue, request.Utterance);
                    }
                }
                catch (Exception ex)
                {
                    validation.Stop();
                    GatewayError error = ex switch
                    {
                        _ when cancellation.IsCancellationRequested => GatewayError.Cancelled(),
                        GatewayError known => known,
                        BadHttpRequestException bad => GatewayError.InvalidInput(bad.StatusCode),
                        JsonException => GatewayError.InvalidInput(),
                        _ => new GatewayError(500, "INTERNAL_ERROR", "중계서버에서 요청을 처리할 수 없습니다.")
                    };
                    status = error.Status;
                    code = error.Code;
                    payload = new { requestId, error = new { code, message = error.Message } };
                }
                if (!cancellation.IsCancellationRequested)
                {
                    ctx.Response.StatusCode = status;
                    // Kestrel cannot drain an oversized body for reuse on the next HTTP/1 request.
                    if (status == 413 && ctx.Request.Protocol.StartsWith("HTTP/1.", StringComparison.Ordinal))
                        ctx.Response.Headers.Connection = "close";
                    await ctx.Response.WriteAsJsonAsync(payload, cancellation);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException && cancellation.IsCancellationRequested)
            {
                status = 499;
                code = "CANCELLED";
            }
            finally
            {
                logger.LogInformation(
                    "requestId={requestId} operation={operation} status={status} code={code} validation_ms={validation_ms} openai_ms={openai_ms} total_ms={total_ms}",
                    requestId, operation, status, code, validation.ElapsedMilliseconds, upstream.ElapsedMilliseconds, total.ElapsedMilliseconds);
            }
        }
    }
}
