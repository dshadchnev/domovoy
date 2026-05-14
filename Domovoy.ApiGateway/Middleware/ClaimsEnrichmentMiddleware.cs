using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace Domovoy.ApiGateway.Middleware;

public class ClaimsEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ClaimsEnrichmentMiddleware> _logger;

    public ClaimsEnrichmentMiddleware(RequestDelegate next, IConnectionMultiplexer redis, ILogger<ClaimsEnrichmentMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var db = _redis.GetDatabase();
                    var cacheKey = $"domovoy:claims:{userId}";
                    var json = await db.StringGetAsync(cacheKey);

                    if (!json.IsNullOrEmpty)
                    {
                        var claims = JsonSerializer.Deserialize<Dictionary<string, string>>(json!)!;
                        foreach (var (k, v) in claims)
                        {
                            if (!string.IsNullOrEmpty(v))
                                ctx.Request.Headers[$"X-Domovoy-{k}"] = v;
                        }
                        _logger.LogDebug("Кэш прав обновлен для пользователя {UserId}", userId);
                    }
                    else
                    {
                        _logger.LogWarning("Кэш прав не найден для пользователя {UserId}", userId);
                    }

                    // Добавляем стандартные claims в заголовки
                    ctx.Request.Headers["X-Domovoy-UserId"] = userId;
                    ctx.Request.Headers["X-Domovoy-UserName"] = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "";
                    ctx.Request.Headers["X-Domovoy-Email"] = ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обогащении claims");
                // Продолжаем работу даже при ошибке кэша
            }
        }

        await _next(ctx);
    }
}
