var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 1. Эндпоинт для проверки работоспособности (Health Check)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", device = "mock-device" }));

// 2. Эндпоинт для приема команд от Command Dispatcher
app.MapPost("/api/command", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine($"🔔 [{DateTime.UtcNow:HH:mm:ss}] Получена команда!");
    Console.WriteLine($"📦 Тело запроса: {body}");
    Console.WriteLine("════════════════════════════════════════");

    return Results.Ok(new { status = "executed", receivedAt = DateTime.UtcNow });
});

app.Run();