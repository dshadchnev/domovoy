using MassTransit;
using Domovoy.Shared.Events;
using Domovoy.CommandDispatcher.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http.Json;

namespace Domovoy.CommandDispatcher.Service.Consumers;

public class CommandExecutor : IConsumer<ExecuteCommandEvent>
{
    private readonly IDbContextFactory<DispatcherDbContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CommandExecutor> _logger;

    public CommandExecutor(
        IDbContextFactory<DispatcherDbContext> dbFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<CommandExecutor> logger)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExecuteCommandEvent> context)
    {
        var command = context.Message;
        _logger.LogInformation("Executing command {Command} for {DeviceId}",
            command.Command, command.DeviceId);

        await using var db = await _dbFactory.CreateDbContextAsync();

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

        // 2. Ищем учётные данные устройства
        var device = await db.DeviceCredentials
            .FirstOrDefaultAsync(d => d.NetworkDeviceId == command.DeviceId);

        if (device == null)
        {
            log.Status = "failed";
            log.ErrorMessage = "Device not found";
            db.CommandLogs.Add(log);
            await db.SaveChangesAsync();
            _logger.LogWarning("[Warning] Device {DeviceId} not found", command.DeviceId);
            return;
        }

        var protocol = device.Protocol?.ToUpperInvariant() ?? "HTTP";
        var endpoint = device.Endpoint; 

        log.Protocol = protocol;
        log.Endpoint = endpoint;
        db.CommandLogs.Add(log);
        await db.SaveChangesAsync();

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

            _logger.LogInformation("[Success] Command {Command} sent to {DeviceId} via {Protocol}",
                command.Command, command.DeviceId, protocol);
        }
        catch (Exception ex)
        {
            // 5. Обработка ошибки
            log.Status = "failed";
            log.ErrorMessage = ex.Message;
            log.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogError(ex, "[Error] Failed to send command {Command} to {DeviceId}",
                command.Command, command.DeviceId);

            throw;
        }
    }

    private async Task SendHttpCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("HTTP endpoint not configured for device");

        var client = _httpClientFactory.CreateClient("DeviceHttp");

        var payload = new
        {
            command = command.Command,
            parameters = string.IsNullOrEmpty(command.Params)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(command.Params),
            timestamp = command.Timestamp
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await client.PostAsJsonAsync(endpoint, payload, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    private Task SendMqttCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("MQTT topic not configured for device");

        // TODO: Интеграция с MQTT брокером через MQTTnet
        _logger.LogInformation("[MQTT] Publish to {Topic}: {Command}", endpoint, command.Command);
        return Task.CompletedTask;
    }

    private Task SendZigbeeCommand(string? endpoint, ExecuteCommandEvent command, CommandLog log)
    {
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException("Zigbee endpoint not configured for device");

        // TODO: Интеграция с Zigbee Coordinator
        _logger.LogInformation("[Zigbee] Command to {Endpoint}: {Command}", endpoint, command.Command);
        return Task.CompletedTask;
    }
}