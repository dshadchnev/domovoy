using MassTransit;
using Domovoy.Shared.Events;
using StackExchange.Redis;
using System.Text.Json;

namespace Domovoy.ApiGateway.Consumers;

public class AuthAuditConsumer(IConnectionMultiplexer redis, ILogger<AuthAuditConsumer> logger)
    : IConsumer<UserLoggedInEvent>,
      IConsumer<UserLoggedOutEvent>,
      IConsumer<TokenRefreshedEvent>,
      IConsumer<DeviceAuthenticatedEvent>,
      IConsumer<DeviceRevokedEvent>
{
    public async Task Consume(ConsumeContext<UserLoggedInEvent> context)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"domovoy:audit:user:{context.Message.UserId}";
            var auditEntry = new
            {
                Action = "LOGIN",
                IpAddress = context.Message.IpAddress,
                Timestamp = context.Message.Timestamp,
                Status = "SUCCESS"
            };
            
            await db.ListLeftPushAsync(key, JsonSerializer.Serialize(auditEntry));
            await db.KeyExpireAsync(key, TimeSpan.FromDays(90)); // Храним 90 дней
            
            logger.LogInformation("Запись входа создана для пользователя {UserId} с IP {IpAddress}", 
                context.Message.UserId, context.Message.IpAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке события входа");
        }
    }

    public async Task Consume(ConsumeContext<UserLoggedOutEvent> context)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"domovoy:audit:user:{context.Message.UserId}";
            var auditEntry = new
            {
                Action = "LOGOUT",
                Timestamp = context.Message.Timestamp,
                Status = "SUCCESS"
            };
            
            await db.ListLeftPushAsync(key, JsonSerializer.Serialize(auditEntry));
            logger.LogInformation("Запись выхода создана для пользователя {UserId}", context.Message.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке события выхода");
        }
    }

    public async Task Consume(ConsumeContext<TokenRefreshedEvent> context)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"domovoy:audit:tokens:{context.Message.UserId}";
            var auditEntry = new
            {
                Action = "TOKEN_REFRESH",
                Timestamp = context.Message.Timestamp,
                Status = "SUCCESS"
            };
            
            await db.ListLeftPushAsync(key, JsonSerializer.Serialize(auditEntry));
            logger.LogDebug("Запись обновления токена создана для пользователя {UserId}", context.Message.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке события обновления токена");
        }
    }

    public async Task Consume(ConsumeContext<DeviceAuthenticatedEvent> context)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"domovoy:audit:device:{context.Message.DeviceId}";
            var auditEntry = new
            {
                Action = "AUTHENTICATED",
                OwnerId = context.Message.OwnerId,
                Timestamp = context.Message.Timestamp,
                Status = "SUCCESS"
            };
            
            await db.ListLeftPushAsync(key, JsonSerializer.Serialize(auditEntry));
            logger.LogDebug("Запись аутентификации устройства создана для {DeviceId}", context.Message.DeviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке события аутентификации устройства");
        }
    }

    public async Task Consume(ConsumeContext<DeviceRevokedEvent> context)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"domovoy:audit:device:{context.Message.DeviceId}";
            var auditEntry = new
            {
                Action = "REVOKED",
                OwnerId = context.Message.OwnerId,
                Timestamp = context.Message.Timestamp,
                Status = "SUCCESS"
            };
            
            await db.ListLeftPushAsync(key, JsonSerializer.Serialize(auditEntry));
            logger.LogInformation("Запись отзыва устройства создана для {DeviceId}", context.Message.DeviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке события отзыва устройства");
        }
    }
}
