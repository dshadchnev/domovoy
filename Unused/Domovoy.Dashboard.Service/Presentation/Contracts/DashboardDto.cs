namespace Domovoy.Dashboard.Service.Presentation.Contracts;

// Переведено: Р РЋР Р†Р С•Р Т‘Р Р…Р В°РЎРЏ Р С‘Р Р…РЎвЂћР С•РЎР‚Р СР В°РЎвЂ Р С‘РЎРЏ Р С—Р С• РЎРѓР С‘РЎРѓРЎвЂљР ВµР СР Вµ
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

// Переведено: Р Р€РЎРѓРЎвЂљРЎР‚Р С•Р в„–РЎРѓРЎвЂљР Р†Р С• РЎРѓР С• РЎРѓРЎвЂљР В°РЎвЂљРЎС“РЎРѓР С•Р С
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

// Переведено: Р СћР ВµР В»Р ВµР СР ВµРЎвЂљРЎР‚Р С‘РЎРЏ Р В·Р В° Р С—Р ВµРЎР‚Р С‘Р С•Р Т‘
public record TelemetryHistoryDto(
    string DeviceId,
    DateTime From,
    DateTime To,
    List<TelemetryPoint> Points);

public record TelemetryPoint(
    DateTime Timestamp,
    Dictionary<string, object> Data);

// Переведено: Р РЋРЎвЂљР В°РЎвЂљР С‘РЎРѓРЎвЂљР С‘Р С”Р В° Р С”Р С•Р СР В°Р Р…Р Т‘
public record CommandStatsDto(
    int Total,
    int Success,
    int Failed,
    int Pending,
    double SuccessRate,
    Dictionary<string, int> ByDevice,
    Dictionary<string, int> ByCommand);