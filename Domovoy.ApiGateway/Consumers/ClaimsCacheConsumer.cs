using MassTransit;
using Domovoy.Shared.Events;
using StackExchange.Redis;
using System.Text.Json;

namespace Domovoy.ApiGateway.Consumers;

public class ClaimsCacheConsumer(IConnectionMultiplexer redis, ILogger<ClaimsCacheConsumer> logger)
    : IConsumer<DeviceLinkedEvent>, IConsumer<UserRolesChangedEvent>
{
    public async Task Consume(ConsumeContext<DeviceLinkedEvent> context)
    {
        var db = redis.GetDatabase();
        var key = $"domovoy:claims:{context.Message.OwnerId}";
        var current = await db.StringGetAsync(key);

        var data = current.IsNullOrEmpty
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(current!) ?? new();

        data["DeviceAccess"] = data.TryGetValue("DeviceAccess", out var existing)
            ? $"{existing},{context.Message.NetworkDeviceId}"
            : context.Message.NetworkDeviceId;

        if (context.Message.RoomId.HasValue)
        {
            data["RoomAccess"] = data.TryGetValue("RoomAccess", out var room)
                ? $"{room},{context.Message.RoomId}"
                : context.Message.RoomId.ToString()!;
        }

        await db.StringSetAsync(key, JsonSerializer.Serialize(data), TimeSpan.FromHours(2));
        logger.LogInformation("Кэш прав обновлён для пользователя {UserId}", context.Message.OwnerId);
    }

    public Task Consume(ConsumeContext<UserRolesChangedEvent> context) => Task.CompletedTask;
}