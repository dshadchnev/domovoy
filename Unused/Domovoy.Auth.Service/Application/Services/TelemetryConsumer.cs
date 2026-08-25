using MassTransit;
using Prometheus;
using StackExchange.Redis;
using System.Text.Json;
using Domovoy.Shared.Events;

namespace Domovoy.Auth.Service.Application.Services;

/// <summary>
/// Консьюмер событий телеметрии IoT-устройств.
/// Слушает RabbitMQ exchange TelemetryReceivedEvent,
/// логирует каждое событие и сохраняет последнее значение в Redis.
///
/// Ключи Redis:
///   device:telemetry:{deviceId}        – последняя телеметрия (TTL 24h), JSON
///   device:telemetry:{deviceId}:count  – счётчик пакетов за всё время
/// </summary>
[EntityName("Telemetry")]
public class TelemetryConsumer(IDatabase redis, ILogger<TelemetryConsumer> logger)
    : IConsumer<TelemetryReceivedEvent>
{
    private static readonly Counter TelemetryReceivedCounter = Metrics.CreateCounter(
        "telemetry_messages_received_total",
        "Total number of telemetry messages received",
        new CounterConfiguration { LabelNames = new[] { "device_id" } });

    private static readonly Histogram TelemetryProcessingDuration = Metrics.CreateHistogram(
        "telemetry_processing_duration_seconds",
        "Duration of telemetry processing in seconds");

    public async Task Consume(ConsumeContext<TelemetryReceivedEvent> context)
    {
        var evt = context.Message;

        logger.LogInformation(
            "📡 [TelemetryConsumer] Device={DeviceId} | Ts={Timestamp:O} | Data={Data}",
            evt.DeviceId, evt.Timestamp, evt.Data);

        TelemetryReceivedCounter.WithLabels(evt.DeviceId).Inc();

        using (TelemetryProcessingDuration.NewTimer())
        {
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    deviceId   = evt.DeviceId,
                    data       = evt.Data,
                    timestamp  = evt.Timestamp,
                    receivedAt = DateTime.UtcNow
                });

                var latestKey  = $"device:telemetry:{evt.DeviceId}";
                var counterKey = $"device:telemetry:{evt.DeviceId}:count";

                await redis.StringSetAsync(latestKey, payload, TimeSpan.FromHours(24));

                var count = await redis.StringIncrementAsync(counterKey);

                logger.LogDebug(
                    "💾 [TelemetryConsumer] Saved to Redis: key={Key} | total_count={Count}",
                    latestKey, count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "❌ [TelemetryConsumer] Failed to write to Redis for device={DeviceId}",
                    evt.DeviceId);
            }
        }
    }
}
