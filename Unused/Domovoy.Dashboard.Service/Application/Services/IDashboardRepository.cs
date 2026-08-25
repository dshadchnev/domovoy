using Domovoy.Dashboard.Service.Presentation.Contracts;
using Domovoy.Dashboard.Service.Infrastructure.Persistence;

namespace Domovoy.Dashboard.Service.Application.Services;

public interface IDashboardRepository
{
    Task<DashboardSummary> GetSummaryAsync(Guid userId);
    Task<List<DeviceStatusDto>> GetDevicesAsync(Guid userId);
    Task<TelemetryHistoryDto> GetTelemetryHistoryAsync(string deviceId, DateTime from, DateTime to);
    Task<CommandStatsDto> GetCommandStatsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<List<CommandLog>> GetRecentCommandsAsync(Guid userId, int limit = 50);
    Task<List<Rule>> GetActiveRulesAsync(Guid userId);
}