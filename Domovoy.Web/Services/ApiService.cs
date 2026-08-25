using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Domovoy.Web.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public ApiService(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        _auth.AddAuthHeader(req);
        return req;
    }

    // Переведено: ─── Dashboard ───────────────────────────────────────────────────────────

    public async Task<JsonElement?> GetDashboardSummaryAsync()
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/dashboard/summary"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    public async Task<JsonElement?> GetDashboardDevicesAsync()
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/dashboard/devices"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    public async Task<JsonElement?> GetTelemetryHistoryAsync(string deviceId)
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, $"/api/dashboard/telemetry/{deviceId}"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    public async Task<JsonElement?> GetRecentCommandsAsync(int limit = 50)
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, $"/api/dashboard/commands/recent?limit={limit}"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    // Переведено: ─── Device Management ────────────────────────────────────────────────────

    public async Task<JsonElement?> GetDevicesMgmtAsync()
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/device-mgmt"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    public async Task<(bool Success, string? Secret, string? Error)> CreateDeviceAsync(string networkDeviceId, string? name, string? protocol, string? endpoint, int? roomId = null)
    {
        try
        {
            // 1. Register in Auth
            var regReq = CreateRequest(HttpMethod.Post, "/api/devices/register");
            regReq.Content = JsonContent.Create(new { networkDeviceId, roomId });
            var regRes = await _http.SendAsync(regReq);
            string? secret = null;
            if (regRes.IsSuccessStatusCode)
            {
                var el = await regRes.Content.ReadFromJsonAsync<JsonElement>();
                if (el.TryGetProperty("secret", out var secProp))
                    secret = secProp.GetString();
            }

            // 2. Update metadata in DeviceManager
            var putReq = CreateRequest(HttpMethod.Put, $"/api/device-mgmt/{networkDeviceId}");
            putReq.Content = JsonContent.Create(new
            {
                name = string.IsNullOrWhiteSpace(name) ? networkDeviceId : name,
                roomId = roomId?.ToString(),
                protocol = string.IsNullOrWhiteSpace(protocol) ? "HTTP" : protocol,
                endpoint = string.IsNullOrWhiteSpace(endpoint) ? "http://domovoy-mock-device:8080/api/command" : endpoint
            });
            var putRes = await _http.SendAsync(putReq);
            return (true, secret, null);
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> CreateDeviceAsync(object dto)
    {
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(dto));
            var devId = el.TryGetProperty("networkDeviceId", out var p1) ? p1.GetString() ?? "" : "";
            var name = el.TryGetProperty("name", out var p2) ? p2.GetString() : devId;
            var proto = el.TryGetProperty("protocol", out var p3) ? p3.GetString() : "HTTP";
            var ep = el.TryGetProperty("endpoint", out var p4) ? p4.GetString() : "http://domovoy-mock-device:8080/api/command";
            var (ok, _, err) = await CreateDeviceAsync(devId, name, proto, ep);
            return (ok, err);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> UpdateDeviceAsync(string deviceId, object dto)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Put, $"/api/device-mgmt/{deviceId}");
            req.Content = JsonContent.Create(dto);
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> DeleteDeviceAsync(string deviceId)
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Delete, $"/api/device-mgmt/{deviceId}"));
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // Переведено: ─── Rules ────────────────────────────────────────────────────────────────

    public async Task<JsonElement?> GetRulesAsync()
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/rules"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }

    public async Task<(bool Success, string? Error)> CreateRuleAsync(object dto)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Post, "/api/rules");
            req.Content = JsonContent.Create(dto);
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> UpdateRuleAsync(Guid ruleId, object dto)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Put, $"/api/rules/{ruleId}");
            req.Content = JsonContent.Create(dto);
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> ToggleRuleAsync(Guid ruleId)
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Post, $"/api/rules/{ruleId}/toggle"));
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string? Error)> DeleteRuleAsync(Guid ruleId)
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Delete, $"/api/rules/{ruleId}"));
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // Переведено: ─── Notifications ────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<NotificationSettingsDto?> GetNotificationSettingsDtoAsync()
    {
        try
        {
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, "/api/notifications/settings"));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<NotificationSettingsDto>(_jsonOptions);
        }
        catch { }
        return null;
    }

    public async Task<(bool Success, string? Error)> SaveNotificationSettingsAsync(NotificationSettingsDto settings)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Put, "/api/notifications/settings");
            req.Content = JsonContent.Create(settings);
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode) return (true, null);
            var body = await res.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> TestNotificationAsync()
    {
        try
        {
            var req = CreateRequest(HttpMethod.Post, "/api/notifications/test");
            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode) return (true, body);
            return (false, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ─── Device Auth & Telemetry Emulation ────────────────────────────────

    public async Task<(bool Success, string? NetworkDeviceId, string? Secret, string? Error)> RegisterDeviceAuthAsync(string networkDeviceId, int? roomId = null)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Post, "/api/devices/register");
            req.Content = JsonContent.Create(new { networkDeviceId, roomId });
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var el = await res.Content.ReadFromJsonAsync<JsonElement>();
                var id = el.TryGetProperty("networkDeviceId", out var p1) ? p1.GetString() : networkDeviceId;
                var sec = el.TryGetProperty("secret", out var p2) ? p2.GetString() : null;
                return (true, id, sec, null);
            }
            var body = await res.Content.ReadAsStringAsync();
            return (false, null, null, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, null, null, ex.Message); }
    }

    public async Task<(bool Success, string? Token, string? Error)> AuthenticateDeviceAsync(string networkDeviceId, string secret)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/device-auth/authenticate")
            {
                Content = JsonContent.Create(new { networkDeviceId, secret })
            };
            var res = await _http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var el = await res.Content.ReadFromJsonAsync<JsonElement>();
                var token = el.TryGetProperty("accessToken", out var p1) ? p1.GetString() : null;
                return (true, token, null);
            }
            var body = await res.Content.ReadAsStringAsync();
            return (false, null, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }

    public async Task<(bool Success, string? Response, string? Error)> SendDeviceTelemetryAsync(string networkDeviceId, string deviceToken, object telemetry)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{networkDeviceId}/telemetry");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);
            req.Content = JsonContent.Create(telemetry);
            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode) return (true, body, null);
            return (false, null, $"HTTP {(int)res.StatusCode}: {body}");
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }

    public async Task<JsonElement?> GetCommandsLogsAsync(string? deviceId = null, int limit = 50)
    {
        try
        {
            var url = string.IsNullOrEmpty(deviceId) 
                ? $"/api/commands?limit={limit}"
                : $"/api/commands?deviceId={Uri.EscapeDataString(deviceId)}&limit={limit}";
            var res = await _http.SendAsync(CreateRequest(HttpMethod.Get, url));
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { }
        return null;
    }
}

public class NotificationSettingsDto
{
    public bool EmailEnabled { get; set; }
    public bool TelegramEnabled { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? RecipientEmail { get; set; }
}
