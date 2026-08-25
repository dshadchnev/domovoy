using MassTransit;
using StackExchange.Redis;
using System.Text.Json;
using Domovoy.Shared.Events;

namespace Domovoy.Auth.Service.Services;

/// <summary>
/// Консьюмер событий телеметрии IoT-устройств.
/// Слушает RabbitMQ exchange TelemetryReceivedEvent,
/// логирует каждое событие и сохраняет последнее значение в Redis.
/// 
/// Ключи Redis:
///   device:telemetry:{deviceId}        — последняя телеметрия (TTL 24h), JSON
///   device:telemetry:{deviceId}:count  — счётчик пакетов за всё время
/// </summary>
[EntityName("Telemetry")]   // Фиксирует имя очереди RabbitMQ
public class TelemetryConsumer(IDatabase redis, ILogger<TelemetryConsumer> logger)
    : IConsumer<TelemetryReceivedEvent>
{
    public async Task Consume(ConsumeContext<TelemetryReceivedEvent> context)
    {
        var evt = context.Message;

        logger.LogInformation(
            "[TelemetryConsumer] Device={DeviceId} | Ts={Timestamp:O} | Data={Data}",
            evt.DeviceId, evt.Timestamp, evt.Data);

        try
        {
            object dataObj;
            try
            {
                dataObj = JsonSerializer.Deserialize<JsonElement>(evt.Data);
            }
            catch
            {
                dataObj = evt.Data;
            }

            // Сериализуем для Redis
            var payload = JsonSerializer.Serialize(new
            {
                deviceId   = evt.DeviceId,
                data       = dataObj,
                timestamp  = evt.Timestamp,
                receivedAt = DateTime.UtcNow
            });

            var latestKey  = $"device:telemetry:{evt.DeviceId}";
            var historyKey = $"device:telemetry:history:{evt.DeviceId}";
            var counterKey = $"device:telemetry:{evt.DeviceId}:count";

            // Пишем последнюю телеметрию (TTL 24 часа)
            await redis.StringSetAsync(latestKey, payload, TimeSpan.FromHours(24));

            // Сохраняем в список истории (до 500 последних точек, TTL 7 дней)
            await redis.ListRightPushAsync(historyKey, payload);
            await redis.ListTrimAsync(historyKey, -500, -1);
            await redis.KeyExpireAsync(historyKey, TimeSpan.FromDays(7));

            // Инкрементируем счётчик
            var count = await redis.StringIncrementAsync(counterKey);

            logger.LogDebug(
                "[TelemetryConsumer] Saved to Redis: key={Key} | total_count={Count}",
                latestKey, count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[TelemetryConsumer] Failed to write to Redis for device={DeviceId}",
                evt.DeviceId);
            // Не пробрасываем — MassTransit сделает retry по политике
        }
    }
}
