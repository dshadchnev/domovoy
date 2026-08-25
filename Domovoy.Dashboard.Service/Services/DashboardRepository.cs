using Domovoy.Dashboard.Service.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Domovoy.Dashboard.Service.Services;

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

        var rules = await _db.Rules
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var commandsToday = await _db.CommandLogs
            .Where(c => c.CreatedAt >= today &&
                       devices.Select(d => d.NetworkDeviceId).Contains(c.DeviceId))
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
            // Читаем последнюю телеметрию из Redis
            var lastTelemetry = await db.StringGetAsync($"device:telemetry:{device.NetworkDeviceId}");
            DateTime? lastSeen = null;
            string? lastStatus = null;

            if (lastTelemetry.HasValue)
            {
                try
                {
                    using var doc = JsonDocument.Parse(lastTelemetry.ToString());
                    var root = doc.RootElement;
                    if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetDateTime(out var ts))
                        lastSeen = ts;
                    if (root.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.ValueKind == JsonValueKind.Object && dataProp.TryGetProperty("status", out var stProp))
                            lastStatus = stProp.GetString();
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
        var db = _redis.GetDatabase();
        var historyKey = $"device:telemetry:history:{deviceId}";
        var entries = await db.ListRangeAsync(historyKey, -200, -1);
        var points = new List<TelemetryPoint>();

        if (entries != null && entries.Length > 0)
        {
            foreach (var entry in entries)
            {
                if (!entry.HasValue) continue;
                try
                {
                    using var doc = JsonDocument.Parse(entry.ToString());
                    var root = doc.RootElement;

                    DateTime ts = DateTime.UtcNow;
                    if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetDateTime(out var parsedTs))
                        ts = parsedTs;
                    else if (root.TryGetProperty("receivedAt", out var recProp) && recProp.TryGetDateTime(out var parsedRec))
                        ts = parsedRec;

                    var dataDict = new Dictionary<string, object>();
                    if (root.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in dataProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var num))
                                    dataDict[prop.Name] = num;
                                else if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    var strVal = prop.Value.GetString() ?? "";
                                    if (double.TryParse(strVal, System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out var parsedNum))
                                        dataDict[prop.Name] = parsedNum;
                                    else
                                        dataDict[prop.Name] = strVal;
                                }
                                else if (prop.Value.ValueKind == JsonValueKind.True ||
                                         prop.Value.ValueKind == JsonValueKind.False)
                                    dataDict[prop.Name] = prop.Value.GetBoolean();
                                else
                                    dataDict[prop.Name] = prop.Value.ToString();
                            }
                        }
                        else if (dataProp.ValueKind == JsonValueKind.String)
                        {
                            // data was stored as escaped JSON string — parse inner JSON
                            try
                            {
                                var innerJson = dataProp.GetString()!;
                                using var innerDoc = JsonDocument.Parse(innerJson);
                                foreach (var prop in innerDoc.RootElement.EnumerateObject())
                                {
                                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var num))
                                        dataDict[prop.Name] = num;
                                    else
                                        dataDict[prop.Name] = prop.Value.ToString();
                                }
                            }
                            catch
                            {
                                dataDict["value"] = dataProp.GetString() ?? "";
                            }
                        }
                    }

                    points.Add(new TelemetryPoint(ts, dataDict));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse telemetry history point for {DeviceId}", deviceId);
                }
            }
        }
        else
        {
            // Fallback: последнее одиночное значение из Redis
            var latest = await db.StringGetAsync($"device:telemetry:{deviceId}");
            if (latest.HasValue)
            {
                try
                {
                    using var doc = JsonDocument.Parse(latest.ToString());
                    var root = doc.RootElement;
                    DateTime ts = DateTime.UtcNow;
                    if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetDateTime(out var parsedTs))
                        ts = parsedTs;

                    var dataDict = new Dictionary<string, object>();
                    if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in dataProp.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var num))
                                dataDict[prop.Name] = num;
                            else
                                dataDict[prop.Name] = prop.Value.ToString();
                        }
                    }
                    points.Add(new TelemetryPoint(ts, dataDict));
                }
                catch { }
            }
        }

        return new TelemetryHistoryDto(deviceId, from, to, points);
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
        return await _db.Rules
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }
}
