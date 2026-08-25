using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Domovoy.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    private const string TokenKey = "domovoy_access_token";
    private const string UsernameKey = "domovoy_username";

    public string? Token { get; private set; }
    public string? Username { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnAuthStateChanged;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    /// <summary>
/// РџРµСЂРµРІРµРґРµРЅРѕ: Call once after component renders to restore token from localStorage.
/// РџРµСЂРµРІРµРґРµРЅРѕ: </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("domovoyStorage.getItem", TokenKey);
            var username = await _js.InvokeAsync<string?>("domovoyStorage.getItem", UsernameKey);
            if (!string.IsNullOrEmpty(token))
            {
                Token = token;
                Username = username;
                OnAuthStateChanged?.Invoke();
            }
        }
        catch
        {
            // РџРµСЂРµРІРµРґРµРЅРѕ: localStorage not available during prerendering вЂ” ignore
        }
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<JsonElement>();
                if (body.TryGetProperty("accessToken", out var tokenProp))
                {
                    Token = tokenProp.GetString();
                    Username = username;

                    await _js.InvokeVoidAsync("domovoyStorage.setItem", TokenKey, Token);
                    await _js.InvokeVoidAsync("domovoyStorage.setItem", UsernameKey, username);

                    OnAuthStateChanged?.Invoke();
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> RegisterAsync(string username, string email, string password, string firstName, string lastName)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/auth/register", new { username, email, password, firstName, lastName });
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        Token = null;
        Username = null;
        try
        {
            await _js.InvokeVoidAsync("domovoyStorage.removeItem", TokenKey);
            await _js.InvokeVoidAsync("domovoyStorage.removeItem", UsernameKey);
        }
        catch { }
        OnAuthStateChanged?.Invoke();
    }

    // РџРµСЂРµРІРµРґРµРЅРѕ: Keep for backward compatibility
    public void Logout() => _ = LogoutAsync();

    public void AddAuthHeader(HttpRequestMessage req)
    {
        if (IsAuthenticated)
        {
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        }
    }
}
