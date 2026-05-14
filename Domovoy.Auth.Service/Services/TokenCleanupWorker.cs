using Microsoft.EntityFrameworkCore;
using Domovoy.Auth.Service.Data;

namespace Domovoy.Auth.Service.Services;

public class TokenCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<TokenCleanupWorker> _logger;

    public TokenCleanupWorker(IServiceProvider sp, ILogger<TokenCleanupWorker> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

                // 1️⃣ Очистка истекших refresh токенов (старше 7 дней)
                var expiredTokens = await db.RefreshTokens
                    .Where(r => r.ExpiresAt < DateTime.UtcNow && r.RevokedAt != null)
                    .ToListAsync(stoppingToken);

                if (expiredTokens.Any())
                {
                    db.RefreshTokens.RemoveRange(expiredTokens);
                    _logger.LogInformation("Очищено {Count} истекших refresh токенов", expiredTokens.Count);
                }

                // 2️⃣ Очистка отозванных кредов устройств (старше 30 дней)
                var revokedCredentials = await db.DeviceCredentials
                    .Where(d => d.IsRevoked && d.CreatedAt < DateTime.UtcNow.AddDays(-30))
                    .ToListAsync(stoppingToken);

                if (revokedCredentials.Any())
                {
                    db.DeviceCredentials.RemoveRange(revokedCredentials);
                    _logger.LogInformation("Очищено {Count} отозванных устройств", revokedCredentials.Count);
                }

                // 3️⃣ Очистка старых логов аудита (старше 90 дней)
                var oldAuditLogs = await db.AuditLogs
                    .Where(a => a.CreatedAt < DateTime.UtcNow.AddDays(-90))
                    .ToListAsync(stoppingToken);

                if (oldAuditLogs.Any())
                {
                    db.AuditLogs.RemoveRange(oldAuditLogs);
                    _logger.LogInformation("Очищено {Count} старых логов аудита", oldAuditLogs.Count);
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в TokenCleanupWorker");
            }
        }
    }
}