using System.Text.Json;
using Domovoy.Shared.Events;
using StackExchange.Redis;
using Prometheus;

namespace Domovoy.Dashboard.Service.Infrastructure.Channels;

public class TelemetryChannelProcessor : BackgroundService
{
    private static readonly Counter TelemetryProcessedCounter = Metrics.CreateCounter(
        "telemetry_messages_processed_total",
        "Total number of telemetry messages processed by channel background worker");

    private readonly TelemetryChannel _channel;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TelemetryChannelProcessor> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10); // Переведено: Ограничение нагрузки на Redis

    public TelemetryChannelProcessor(
        TelemetryChannel channel,
        IConnectionMultiplexer redis,
        ILogger<TelemetryChannelProcessor> logger)
    {
        _channel = channel;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 TelemetryChannelProcessor background pipeline started.");

        await foreach (var msg in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await _semaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    var db = _redis.GetDatabase();

                    // Переведено: 1. Сохраняем последнее состояние телеметрии в Redis
                    var latestKey = $"device:telemetry:{msg.DeviceId}";
                    await db.StringSetAsync(latestKey, msg.Data, TimeSpan.FromDays(7));

                    // Переведено: 2. Высокопроизводительный парсинг JSON без аллокации
                    var dataDict = ParseTelemetryData(msg.Data);
                    if (!dataDict.ContainsKey("timestamp"))
                    {
                        dataDict["timestamp"] = msg.Timestamp;
                    }

                    var pointJson = JsonSerializer.Serialize(new
                    {
                        Timestamp = msg.Timestamp,
                        Data = dataDict
                    });

                    // Переведено: 3. Добавляем в исторический список Redis
                    var historyKey = $"device:telemetry:history:{msg.DeviceId}";
                    await db.ListRightPushAsync(historyKey, pointJson);
                    await db.KeyExpireAsync(historyKey, TimeSpan.FromDays(30));

                    TelemetryProcessedCounter.Inc();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ ChannelProcessor error processing telemetry for device {DeviceId}", msg.DeviceId);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    private static Dictionary<string, object> ParseTelemetryData(string data)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
        ReadOnlySpan<byte> jsonSpan = bytes.AsSpan();
        return ParseJsonSpanToDictionary(jsonSpan);
    }

    private static Dictionary<string, object> ParseJsonSpanToDictionary(ReadOnlySpan<byte> jsonSpan)
    {
        var reader = new Utf8JsonReader(jsonSpan);
        var dataDict = new Dictionary<string, object>();
        string? currentProperty = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    currentProperty = reader.GetString();
                    break;

                case JsonTokenType.Number:
                    if (currentProperty != null)
                    {
                        if (reader.TryGetDouble(out var doubleVal))
                        {
                            dataDict[currentProperty] = doubleVal;
                        }
                        else if (reader.TryGetInt64(out var intVal))
                        {
                            dataDict[currentProperty] = intVal;
                        }
                    }
                    break;

                case JsonTokenType.String:
                    if (currentProperty != null)
                    {
                        dataDict[currentProperty] = reader.GetString() ?? string.Empty;
                    }
                    break;

                case JsonTokenType.True:
                    if (currentProperty != null) dataDict[currentProperty] = true;
                    break;

                case JsonTokenType.False:
                    if (currentProperty != null) dataDict[currentProperty] = false;
                    break;
            }
        }

        return dataDict;
    }
}
