using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Domovoy.IoTSimulator;

class Program
{
    static async Task Main(string[] args)
    {
        // 🔧 Конфигурация (можно переопределить через args или appsettings.json)
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddCommandLine(args)
            .Build();

        string deviceId = config["DeviceId"] ?? "sim-light-01";
        string deviceSecret = config["DeviceSecret"] ?? "YOUR_DEVICE_SECRET_HERE";
        string gatewayUrl = config["GatewayUrl"] ?? "http://localhost:8085";
        int intervalSeconds = int.Parse(config["IntervalSeconds"] ?? "5");

        if (deviceSecret == "YOUR_DEVICE_SECRET_HERE")
        {
            Console.WriteLine("❌ ОШИБКА: Укажите DeviceSecret в appsettings.json или через аргументы: --DeviceSecret=xxx");
            return;
        }

        Console.WriteLine($"🚀 IoT Simulator запущен | Device: {deviceId} | Interval: {intervalSeconds}с");
        Console.WriteLine(new string('=', 60));

        using var httpClient = new HttpClient { BaseAddress = new Uri(gatewayUrl) };
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        string? token = null;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // 🔐 Если токена нет или истек, аутентифицируемся
                if (string.IsNullOrEmpty(token))
                {
                    Console.Write("🔑 Аутентификация устройства... ");
                    token = await AuthenticateAsync(httpClient, deviceId, deviceSecret);
                    if (string.IsNullOrEmpty(token))
                    {
                        Console.WriteLine("❌ Не удалось получить токен. Повтор через 10с...");
                        await Task.Delay(10000, cts.Token);
                        continue;
                    }
                    Console.WriteLine("✅ Успешно");
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                // 📡 Генерация и отправка телеметрии
                var telemetry = GenerateTelemetry();
                var response = await SendTelemetryAsync(httpClient, deviceId, telemetry, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] 📤 Отправлено: {telemetry}");
                }
                else if ((int)response.StatusCode == 401)
                {
                    Console.WriteLine("⏳ Токен истек. Запрашиваем новый...");
                    token = null; // Принудительная ре-аутентификация на следующей итерации
                }
                else
                {
                    Console.WriteLine($"⚠️ Ошибка отправки: {response.StatusCode} | {(await response.Content.ReadAsStringAsync())}");
                }

                await Task.Delay(intervalSeconds * 1000, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n🛑 Симулятор остановлен пользователем.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n💥 Критическая ошибка: {ex.Message}");
        }
    }

    static async Task<string?> AuthenticateAsync(HttpClient client, string deviceId, string secret)
    {
        var authPayload = JsonSerializer.Serialize(new { networkDeviceId = deviceId, secret = secret });
        var content = new StringContent(authPayload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/device-auth/authenticate", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
            return result.GetProperty("accessToken").GetString();
        }
        return null;
    }

    static string GenerateTelemetry()
    {
        var rand = new Random();
        return JsonSerializer.Serialize(new
        {
            status = rand.Next(2) == 1 ? "ON" : "OFF",
            brightness = rand.Next(10, 101),
            temperature = Math.Round(22.0 + rand.NextDouble() * 6.0, 1),
            voltage = Math.Round(220.0 + rand.NextDouble() * 10.0 - 5.0, 1)
        });
    }

    static async Task<HttpResponseMessage> SendTelemetryAsync(HttpClient client, string deviceId, string payload, CancellationToken ct)
    {
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        return await client.PostAsync($"/api/devices/{deviceId}/telemetry", content, ct);
    }
}