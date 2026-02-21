using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Tests.Unit.Services;

public class RequestTelemetryBufferTests
{
    /// <summary>
    /// Cenario: volume de telemetria excede a capacidade configurada do buffer em memoria.
    /// Passos: enfileira eventos ate o limite e tenta inserir mais um item adicional.
    /// Resultado esperado: ultima escrita eh rejeitada e tamanho da fila permanece no teto configurado.
    /// </summary>
    [Fact(DisplayName = "Requisicao telemetry buffer | Try enqueue | Deve drop writes quando buffer full")]
    public void TryEnqueue_ShouldDropWrites_WhenBufferIsFull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:TelemetryBuffer:Capacity"] = "1000"
            })
            .Build();

        var buffer = new RequestTelemetryBuffer(configuration);

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(buffer.TryEnqueue(CreateEvent(i)));
        }

        Assert.False(buffer.TryEnqueue(CreateEvent(1000)));
        Assert.Equal(1000, buffer.ApproximateQueueLength);
    }

    /// <summary>
    /// Cenario: worker de flush consome evento previamente enfileirado no buffer.
    /// Passos: adiciona um evento, executa DequeueAsync e compara o item retornado com o original.
    /// Resultado esperado: evento correto e removido da fila, reduzindo o contador de itens pendentes.
    /// </summary>
    [Fact(DisplayName = "Requisicao telemetry buffer | Dequeue | Deve retornar event e reduce queue length")]
    public async Task DequeueAsync_ShouldReturnEvent_AndReduceQueueLength()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:TelemetryBuffer:Capacity"] = "1000"
            })
            .Build();

        var buffer = new RequestTelemetryBuffer(configuration);
        var expected = CreateEvent(1);

        Assert.True(buffer.TryEnqueue(expected));
        Assert.Equal(1, buffer.ApproximateQueueLength);

        var dequeued = await buffer.DequeueAsync();

        Assert.Equal(expected.CorrelationId, dequeued.CorrelationId);
        Assert.Equal(expected.EndpointTemplate, dequeued.EndpointTemplate);
        Assert.Equal(0, buffer.ApproximateQueueLength);
    }

    private static ApiRequestTelemetryEventDto CreateEvent(int index)
    {
        return new ApiRequestTelemetryEventDto(
            TimestampUtc: DateTime.UtcNow,
            CorrelationId: $"corr-{index}",
            TraceId: $"trace-{index}",
            Method: "GET",
            EndpointTemplate: "/api/test/{id}",
            Path: $"/api/test/{index}",
            StatusCode: 200,
            DurationMs: 10,
            Severity: "info",
            IsError: false,
            WarningCount: 0,
            WarningCodesJson: null,
            ErrorType: null,
            NormalizedErrorMessage: null,
            NormalizedErrorKey: null,
            IpHash: null,
            UserAgent: "tests",
            UserId: null,
            TenantId: null,
            RequestSizeBytes: null,
            ResponseSizeBytes: null,
            Scheme: "https",
            Host: "localhost");
    }
}
