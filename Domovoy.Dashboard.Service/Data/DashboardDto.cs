namespace Domovoy.Dashboard.Service.Data;

// Сводная информация по системе
public record DashboardSummary(
    int TotalDevices,
    int ActiveDevices,
    int TotalRules,
    int ActiveRules,
    int CommandsToday,
    int CommandsSuccess,
    int CommandsFailed,
    Dictionary<string, int> DevicesByProtocol,
    Dictionary<string, int> CommandsByStatus);

// Устройство со статусом
public record DeviceStatusDto(
    Guid Id,
    string NetworkDeviceId,
    string? Name,
    string? Protocol,
    string? Endpoint,
    bool IsRevoked,
    DateTime CreatedAt,
    DateTime? LastSeen,
    string? LastStatus);

// Телеметрия за период
public record TelemetryHistoryDto(
    string DeviceId,
    DateTime From,
    DateTime To,
    List<TelemetryPoint> Points);

public record TelemetryPoint(
    DateTime Timestamp,
    Dictionary<string, object> Data);

// Статистика команд
public record CommandStatsDto(
    int Total,
    int Success,
    int Failed,
    int Pending,
    double SuccessRate,
    Dictionary<string, int> ByDevice,
    Dictionary<string, int> ByCommand);