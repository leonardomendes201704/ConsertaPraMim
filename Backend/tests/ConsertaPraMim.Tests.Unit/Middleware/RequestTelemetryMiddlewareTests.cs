using ConsertaPraMim.API.Middleware;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Tests.Unit.Middleware;

public class RequestTelemetryMiddlewareTests
{
    /// <summary>
    /// Cenario: requisicao bem-sucedida com warning funcional registrado durante o processamento.
    /// Passos: executa middleware com WarningCollector preenchido e endpoint template resolvido via roteamento.
    /// Resultado esperado: telemetria persistida com severity warn, warningCount=1 e metadados essenciais da requisicao.
    /// </summary>
    [Fact(DisplayName = "Requisicao telemetry middleware | Invoke | Deve capture warn severity quando warning existe")]
    public async Task InvokeAsync_ShouldCaptureWarnSeverity_WhenWarningExists()
    {
        var warningCollector = new RequestWarningCollector();
        var buffer = new TestTelemetryBuffer();
        var middleware = BuildMiddleware(
            context =>
            {
                warningCollector.AddWarning("validation_error");
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            buffer,
            captureSwagger: false);

        var context = CreateHttpContext("/api/mobile/orders/123");
        context.SetEndpoint(CreateRouteEndpoint("/api/mobile/orders/{id}"));

        await middleware.InvokeAsync(context, warningCollector);

        var telemetry = Assert.Single(buffer.Events);
        Assert.Equal("warn", telemetry.Severity);
        Assert.Equal("/api/mobile/orders/{id}", telemetry.EndpointTemplate);
        Assert.Equal(1, telemetry.WarningCount);
        Assert.Equal(StatusCodes.Status200OK, telemetry.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(telemetry.CorrelationId));
    }

    /// <summary>
    /// Cenario: pipeline gera excecao com dados sensiveis no texto (email, guid e numero).
    /// Passos: middleware executa delegate que lanca InvalidOperationException e captura evento de telemetria de erro.
    /// Resultado esperado: erro repropagado para o pipeline e telemetria com status 500, tipo da excecao e mensagem normalizada.
    /// </summary>
    [Fact(DisplayName = "Requisicao telemetry middleware | Invoke | Deve normalize exception e marcar como erro")]
    public async Task InvokeAsync_ShouldNormalizeExceptionAndMarkAsError()
    {
        var warningCollector = new RequestWarningCollector();
        var buffer = new TestTelemetryBuffer();
        var exception = new InvalidOperationException(
            $"Failure for user foo.bar@example.com id {Guid.Parse("11111111-2222-3333-4444-555555555555")} seq 987");

        var middleware = BuildMiddleware(_ => throw exception, buffer, captureSwagger: false);
        var context = CreateHttpContext("/api/admin/monitoring/overview");
        context.SetEndpoint(CreateRouteEndpoint("/api/admin/monitoring/overview"));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, warningCollector));
        Assert.Same(exception, thrown);

        var telemetry = Assert.Single(buffer.Events);
        Assert.Equal("error", telemetry.Severity);
        Assert.Equal(StatusCodes.Status500InternalServerError, telemetry.StatusCode);
        Assert.Equal("InvalidOperationException", telemetry.ErrorType);
        Assert.NotNull(telemetry.NormalizedErrorMessage);
        Assert.Contains("{email}", telemetry.NormalizedErrorMessage!, StringComparison.Ordinal);
        Assert.Contains("{guid}", telemetry.NormalizedErrorMessage!, StringComparison.Ordinal);
        Assert.Contains("{n}", telemetry.NormalizedErrorMessage!, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(telemetry.NormalizedErrorKey));
    }

    /// <summary>
    /// Cenario: endpoint de Swagger acessado quando configuracao de captura de swagger esta desabilitada.
    /// Passos: executa middleware com caminho /swagger/index.html e monitoramento habilitado.
    /// Resultado esperado: nenhum evento e enfileirado no buffer, evitando ruido de telemetria tecnica.
    /// </summary>
    [Fact(DisplayName = "Requisicao telemetry middleware | Invoke | Deve skip swagger quando capture disabled")]
    public async Task InvokeAsync_ShouldSkipSwagger_WhenCaptureIsDisabled()
    {
        var warningCollector = new RequestWarningCollector();
        var buffer = new TestTelemetryBuffer();
        var middleware = BuildMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            buffer,
            captureSwagger: false);

        var context = CreateHttpContext("/swagger/index.html");

        await middleware.InvokeAsync(context, warningCollector);

        Assert.Empty(buffer.Events);
    }

    private static RequestTelemetryMiddleware BuildMiddleware(
        RequestDelegate next,
        IRequestTelemetryBuffer buffer,
        bool captureSwagger)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:Enabled"] = "true",
                ["Monitoring:CaptureSwaggerRequests"] = captureSwagger ? "true" : "false",
                ["Monitoring:IpHashSalt"] = "testsalt"
            })
            .Build();

        return new RequestTelemetryMiddleware(
            next,
            configuration,
            NullLogger<RequestTelemetryMiddleware>.Instance,
            buffer,
            new TestMonitoringRuntimeSettings());
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 5001);
        context.Request.Headers.UserAgent = "integration-tests";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.20");
        return context;
    }

    private static RouteEndpoint CreateRouteEndpoint(string routeTemplate)
    {
        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(routeTemplate),
            order: 0,
            EndpointMetadataCollection.Empty,
            "test-endpoint");
    }

    private sealed class TestTelemetryBuffer : IRequestTelemetryBuffer
    {
        public List<ApiRequestTelemetryEventDto> Events { get; } = [];

        public int ApproximateQueueLength => Events.Count;

        public ValueTask<ApiRequestTelemetryEventDto> DequeueAsync(CancellationToken cancellationToken = default)
        {
            if (Events.Count == 0)
            {
                throw new InvalidOperationException("Buffer vazio.");
            }

            var item = Events[0];
            Events.RemoveAt(0);
            return ValueTask.FromResult(item);
        }

        public bool TryDequeue(out ApiRequestTelemetryEventDto? telemetryEvent)
        {
            if (Events.Count == 0)
            {
                telemetryEvent = null;
                return false;
            }

            telemetryEvent = Events[0];
            Events.RemoveAt(0);
            return true;
        }

        public bool TryEnqueue(ApiRequestTelemetryEventDto telemetryEvent)
        {
            Events.Add(telemetryEvent);
            return true;
        }
    }

    private sealed class TestMonitoringRuntimeSettings : IMonitoringRuntimeSettings
    {
        public Task<AdminMonitoringRuntimeConfigDto> GetTelemetryConfigAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AdminMonitoringRuntimeConfigDto(
                TelemetryEnabled: true,
                UpdatedAtUtc: DateTime.UtcNow));
        }

        public Task<bool> IsTelemetryEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public void InvalidateTelemetryCache()
        {
        }
    }
}
