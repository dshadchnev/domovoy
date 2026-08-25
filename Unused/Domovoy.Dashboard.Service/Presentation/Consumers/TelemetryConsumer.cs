using MassTransit;
using Domovoy.Shared.Events;
using StackExchange.Redis;
using System.Text.Json;

using Prometheus;
using Domovoy.Dashboard.Service.Infrastructure.Channels;

namespace Domovoy.Dashboard.Service.Presentation.Consumers;

/// <summary>
/// Переведено: Потребитель телеметрии устройств для сервиса Dashboard.
/// Переведено: Сохраняет актуальную телеметрию и исторические точки в Redis.
/// Переведено: </summary>
public class TelemetryConsumer : IConsumer<TelemetryReceivedEvent>
{
    private static readonly Counter TelemetryReceivedCounter = Metrics.CreateCounter(
        "telemetry_messages_received_total",
        "Total number of telemetry messages received",
        new CounterConfiguration { LabelNames = new[] { "device_id" } });

    private static readonly Histogram TelemetryProcessingDuration = Metrics.CreateHistogram(
        "telemetry_processing_duration_seconds",
        "Duration of telemetry processing in seconds");

    private readonly TelemetryChannel _channel;
    private readonly ILogger<TelemetryConsumer> _logger;

    public TelemetryConsumer(TelemetryChannel channel, ILogger<TelemetryConsumer> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TelemetryReceivedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("📊 Received telemetry for device {DeviceId} at {Timestamp}", msg.DeviceId, msg.Timestamp);

        TelemetryReceivedCounter.WithLabels(msg.DeviceId).Inc();
        using (TelemetryProcessingDuration.NewTimer())
        {
            // Переведено: Мгновенная буферизованная передача события в конвейер (Producer)
            await _channel.Writer.WriteAsync(msg, context.CancellationToken);
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
                        if (reader.TryGetDouble(out double doubleVal))
                            dataDict[currentProperty] = doubleVal;
                        else if (reader.TryGetInt64(out long intVal))
                            dataDict[currentProperty] = intVal;
                    }
                    break;

                case JsonTokenType.String:
                    if (currentProperty != null)
                        dataDict[currentProperty] = reader.GetString() ?? string.Empty;
                    break;

                case JsonTokenType.True:
                    if (currentProperty != null)
                        dataDict[currentProperty] = true;
                    break;

                case JsonTokenType.False:
                    if (currentProperty != null)
                        dataDict[currentProperty] = false;
                    break;
            }
        }

        return dataDict;
    }
}
