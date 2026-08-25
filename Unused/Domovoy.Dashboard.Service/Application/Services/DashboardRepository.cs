using Domovoy.Dashboard.Service.Presentation.Contracts;
using Domovoy.Dashboard.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Domovoy.Dashboard.Service.Application.Services;

public class DashboardRepository : IDashboardRepository
{
    private readonly DashboardDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DashboardRepository> _logger;

    public DashboardRepository(
        DashboardDbContext db,
        IConnectionMultiplexer redis,
        ILogger<DashboardRepository> logger)
    {
        _db = db;
        _redis = redis;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetSummaryAsync(Guid userId)
    {
        var devices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId)
            .ToListAsync();

        var userIdStr = userId.ToString();
        var rules = await _db.Rules
            .Where(r => r.UserId == userIdStr)
            .ToListAsync();

        var since24h = DateTime.UtcNow.AddHours(-24);
        var deviceIds = devices.Select(d => d.NetworkDeviceId).ToList();
        var commandsToday = await _db.CommandLogs
            .Where(c => c.CreatedAt >= since24h && deviceIds.Contains(c.DeviceId))
            .ToListAsync();

        return new DashboardSummary(
            TotalDevices: devices.Count,
            ActiveDevices: devices.Count(d => !d.IsRevoked),
            TotalRules: rules.Count,
            ActiveRules: rules.Count(r => r.IsActive),
            CommandsToday: commandsToday.Count,
            CommandsSuccess: commandsToday.Count(c => c.Status == "success"),
            CommandsFailed: commandsToday.Count(c => c.Status == "failed"),
            DevicesByProtocol: devices
                .GroupBy(d => d.Protocol ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count()),
            CommandsByStatus: commandsToday
                .GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.Count())
        );
    }

    public async Task<List<DeviceStatusDto>> GetDevicesAsync(Guid userId)
    {
        var devices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId)
            .ToListAsync();

        var db = _redis.GetDatabase();
        var result = new List<DeviceStatusDto>();

        foreach (var device in devices)
        {
            // Переведено: Р В Р’В Р РЋРЎСџР В Р’В Р РЋРІР‚СћР В Р’В Р вЂ™Р’В»Р В Р Р‹Р РЋРІР‚СљР В Р Р‹Р Р†Р вЂљР Р‹Р В Р’В Р вЂ™Р’В°Р В Р’В Р вЂ™Р’ВµР В Р’В Р РЋР’В Р В Р’В Р РЋРІР‚вЂќР В Р’В Р РЋРІР‚СћР В Р Р‹Р В РЎвЂњР В Р’В Р вЂ™Р’В»Р В Р’В Р вЂ™Р’ВµР В Р’В Р СћРІР‚ВР В Р’В Р В РІР‚В¦Р В Р Р‹Р В РІР‚в„–Р В Р Р‹Р В РІР‚в„– Р В Р Р‹Р Р†Р вЂљРЎв„ўР В Р’В Р вЂ™Р’ВµР В Р’В Р вЂ™Р’В»Р В Р’В Р вЂ™Р’ВµР В Р’В Р РЋР’ВР В Р’В Р вЂ™Р’ВµР В Р Р‹Р Р†Р вЂљРЎв„ўР В Р Р‹Р В РІР‚С™Р В Р’В Р РЋРІР‚ВР В Р Р‹Р В РІР‚в„– Р В Р’В Р РЋРІР‚ВР В Р’В Р вЂ™Р’В· Redis
            var lastTelemetry = await db.StringGetAsync($"device:telemetry:{device.NetworkDeviceId}");
            DateTime? lastSeen = null;
            string? lastStatus = null;

            if (lastTelemetry.HasValue)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(lastTelemetry!);
                    if (data != null && data.ContainsKey("timestamp"))
                    {
                        lastSeen = DateTime.Parse(data["timestamp"]!.ToString()!);
                        lastStatus = data.ContainsKey("status") ? data["status"]!.ToString() : null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse telemetry for {DeviceId}", device.NetworkDeviceId);
                }
            }

            result.Add(new DeviceStatusDto(
                device.Id,
                device.NetworkDeviceId,
                device.Name,
                device.Protocol,
                device.Endpoint,
                device.IsRevoked,
                device.CreatedAt,
                lastSeen,
                lastStatus
            ));
        }

        return result;
    }

    public async Task<TelemetryHistoryDto> GetTelemetryHistoryAsync(string deviceId, DateTime from, DateTime to)
    {
        var points = new List<TelemetryPoint>();
        try
        {
            var db = _redis.GetDatabase();
            var historyKey = $"device:telemetry:history:{deviceId}";
            var rawItems = await db.ListRangeAsync(historyKey, 0, -1);

            foreach (var raw in rawItems)
            {
                if (!raw.HasValue) continue;

                using var doc = JsonDocument.Parse(raw.ToString());
                var root = doc.RootElement;

                var timestamp = root.GetProperty("Timestamp").GetDateTime();
                if (timestamp < from || timestamp > to)
                    continue;

                var dataDict = new Dictionary<string, object>();
                if (root.TryGetProperty("Data", out var dataEl))
                {
                    foreach (var prop in dataEl.EnumerateObject())
                    {
                        dataDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number => prop.Value.TryGetDouble(out var d) ? d : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.GetString() ?? string.Empty
                        };
                    }
                }

                points.Add(new TelemetryPoint(timestamp, dataDict));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read telemetry history from Redis for {DeviceId}", deviceId);
        }

        return new TelemetryHistoryDto(deviceId, from, to, points.OrderBy(p => p.Timestamp).ToList());
    }

    public async Task<CommandStatsDto> GetCommandStatsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var devices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId)
            .Select(d => d.NetworkDeviceId)
            .ToListAsync();

        var query = _db.CommandLogs
            .Where(c => devices.Contains(c.DeviceId));

        if (from.HasValue)
            query = query.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(c => c.CreatedAt <= to.Value);

        var commands = await query.ToListAsync();

        var total = commands.Count;
        var success = commands.Count(c => c.Status == "success");
        var failed = commands.Count(c => c.Status == "failed");
        var pending = commands.Count(c => c.Status == "pending");

        return new CommandStatsDto(
            Total: total,
            Success: success,
            Failed: failed,
            Pending: pending,
            SuccessRate: total > 0 ? (double)success / total * 100 : 0,
            ByDevice: commands
                .GroupBy(c => c.DeviceId)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByCommand: commands
                .GroupBy(c => c.Command)
                .ToDictionary(g => g.Key, g => g.Count())
        );
    }

    public async Task<List<CommandLog>> GetRecentCommandsAsync(Guid userId, int limit = 50)
    {
        var devices = await _db.DeviceCredentials
            .Where(d => d.OwnerUserId == userId)
            .Select(d => d.NetworkDeviceId)
            .ToListAsync();

        return await _db.CommandLogs
            .Where(c => devices.Contains(c.DeviceId))
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Rule>> GetActiveRulesAsync(Guid userId)
    {
        var userIdStr = userId.ToString();
        return await _db.Rules
            .Where(r => r.UserId == userIdStr && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }
}
