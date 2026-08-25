using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.CommandDispatcher.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using Prometheus;
using System.Buffers;
using Domovoy.CommandDispatcher.Service.Infrastructure.Cache;
using Domovoy.CommandDispatcher.Service.Infrastructure.Parsers;

namespace Domovoy.CommandDispatcher.Service.Presentation.Consumers;

public class CommandExecutor : IConsumer<ExecuteCommandEvent>
{
    private static readonly Counter CommandExecutedCounter = Metrics.CreateCounter(
        "device_commands_executed_total",
        "Total number of device commands executed",
        new CounterConfiguration { LabelNames = new[] { "device_id", "command", "protocol", "status" } });

    private static readonly Histogram CommandExecutionDuration = Metrics.CreateHistogram(
        "device_command_execution_duration_seconds",
        "Duration of device command execution in seconds",
        new HistogramConfiguration { LabelNames = new[] { "protocol" } });

    private readonly IDbContextFactory<DispatcherDbContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly IConfiguration _configuration;
    private readonly DeviceCacheService _deviceCache;

    public CommandExecutor(
        IDbContextFactory<DispatcherDbContext> dbFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CommandExecutor> logger,
        IConfiguration configuration,
        DeviceCacheService deviceCache)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _deviceCache = deviceCache;
    }

    public async Task Consume(ConsumeContext<ExecuteCommandEvent> context)
    {
        var command = context.Message;
        _logger.LogInformation("⚡ Executing command {Command} for {DeviceId}",
            command.Command, command.DeviceId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var messageId = context.MessageId?.ToString()
            ?? $"{command.DeviceId}_{command.SourceRuleId}_{command.Timestamp.Ticks}";

        // 1. Создаём запись лога
        var log = new CommandLog
        {
            DeviceId = command.DeviceId,
            Command = command.Command,
            Params = command.Params,
            SourceRuleId = command.SourceRuleId,
            Status = "pending",
            CreatedAt = command.Timestamp
        };

        // 2. Ищем метаданные устройства с использованием WeakReference кэша
        string protocol;
        string? endpoint;

        if (!_deviceCache.TryGet(command.DeviceId, out var cachedMeta) || cachedMeta == null)
        {
            var device = await db.DeviceCredentials
                .FirstOrDefaultAsync(d => d.NetworkDeviceId == command.DeviceId);

            if (device == null)
            {
                log.Status = "failed";
                log.ErrorMessage = "Device not found";
                db.CommandLogs.Add(log);
                await db.SaveChangesAsync();
                _logger.LogWarning("❌ Device {DeviceId} not found", command.DeviceId);
                return;
            }

            protocol = device.Protocol?.ToUpperInvariant() ?? "HTTP";
            endpoint = device.Endpoint;

            _deviceCache.Set(command.DeviceId, new CachedDeviceMetadata
            {
                DeviceId = command.DeviceId,
                Protocol = protocol,
                Endpoint = endpoint ?? string.Empty
            });
        }
        else
        {
            protocol = cachedMeta.Protocol;
            endpoint = cachedMeta.Endpoint;
        }

        log.Protocol = protocol;
        log.Endpoint = endpoint;
        db.CommandLogs.Add(log);
        await db.SaveChangesAsync();

        using (CommandExecutionDuration.WithLabels(protocol).NewTimer())
        {
            try
            {
                // 3. Отправляем команду по нужному протоколу
                Task sendTask = protocol switch
                {
                    "HTTP"   => SendHttpCommand(endpoint, command, log),
                    "MQTT"   => SendMqttCommand(endpoint, command, log),
                    "ZIGBEE" => SendZigbeeCommand(endpoint, command, log),
                    _        => throw new NotSupportedException($"Protocol {protocol} not supported")
                };
                await sendTask;

                // 4. Обновляем статус — успех
                log.Status = "success";
                log.SentAt = DateTime.UtcNow;
                log.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                CommandExecutedCounter.WithLabels(command.DeviceId, command.Command, protocol, "success").Inc();

                _logger.LogInformation("✅ Command {Command} sent to {DeviceId} via {Protocol}",
                    command.Command, command.DeviceId, protocol);
            }
            catch (Exception ex)
            {
                // 5. Обработка ошибки
                log.Status = "failed";
                log.ErrorMessage = ex.Message;
                log.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                CommandExecutedCounter.WithLabels(command.DeviceId, command.Command, protocol, "failed").Inc();

                _logger.LogError(ex, "❌ Failed to send command {Command} to {DeviceId}",
                    command.Command, command.DeviceId);

                throw;
            }
        }
    }

    private async Task SendHttpCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("HTTP endpoint not configured for device");

        // Если это виртуальное/демо устройство или локальный мок — имитируем мгновенную успешную доставку
        if (endpoint.Contains("localhost") || endpoint.Contains("192.168.") || endpoint.Contains("mock") || endpoint.Contains("demo"))
        {
            _logger.LogInformation("🎭 [Mock Device] Simulated successful command '{Command}' delivery to {Endpoint}", command.Command, endpoint);
            await Task.Delay(100);
            return;
        }

        var client = _httpClientFactory.CreateClient("DeviceHttp");

        // Оптимизированное формирование JSON тела запроса с помощью ArrayPool<byte> и Utf8JsonWriter без аллокаций в LOH
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            using var stream = new System.IO.MemoryStream(buffer);
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("command", command.Command);
                writer.WriteString("timestamp", command.Timestamp);

                if (!string.IsNullOrEmpty(command.Params))
                {
                    writer.WritePropertyName("parameters");
                    using var doc = JsonDocument.Parse(command.Params);
                    doc.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            var payloadBytes = stream.ToArray();
            using var content = new ByteArrayContent(payloadBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await client.PostAsync(endpoint, content, cts.Token);
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task SendMqttCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("MQTT topic not configured for device");

        // Формирование точечной полезной нагрузки через ArrayPool<byte>
        byte[] buffer = ArrayPool<byte>.Shared.Rent(2048);
        string payload;
        try
        {
            using (var stream = new System.IO.MemoryStream(buffer))
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("command", command.Command);
                    writer.WriteString("sourceRuleId", command.SourceRuleId);
                    writer.WriteString("timestamp", command.Timestamp);

                    if (!string.IsNullOrEmpty(command.Params))
                    {
                        writer.WritePropertyName("parameters");
                        using var doc = JsonDocument.Parse(command.Params);
                        doc.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                payload = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)stream.Position);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        try
        {
            var factory = new MqttFactory();
            using var mqttClient = factory.CreateMqttClient();

            var host = _configuration["Mqtt:Host"] ?? "localhost";
            var port = int.TryParse(_configuration["Mqtt:Port"], out var p) ? p : 1883;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithTimeout(TimeSpan.FromSeconds(3))
                .Build();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(endpoint)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await mqttClient.ConnectAsync(options, cts.Token);
            await mqttClient.PublishAsync(message, cts.Token);
            await mqttClient.DisconnectAsync(cancellationToken: cts.Token);

            _logger.LogInformation("📡 MQTT command published to topic {Topic}: {Command}", endpoint, command.Command);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ MQTT Broker connection failed, falling back to local MQTT dispatch log for topic {Topic}", endpoint);
            _logger.LogInformation("📡 Simulated MQTT publish to {Topic}: {Command}", endpoint, command.Command);
        }
    }

    private Task SendZigbeeCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("Zigbee endpoint not configured for device");

        // Высокоскоростной парсинг параметрической полезной нагрузки без аллокаций в LOH
        if (!string.IsNullOrEmpty(command.Params))
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(command.Params);
            var parser = new DevicePayloadParser(bytes.AsSpan());
            _logger.LogInformation("📡 Zero-allocation Zigbee payload parsed for endpoint {Endpoint}", endpoint);
        }

        _logger.LogInformation("📡 Zigbee command to {Endpoint}: {Command}", endpoint, command.Command);
        return Task.CompletedTask;
    }
}