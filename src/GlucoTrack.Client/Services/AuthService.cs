using System.Net.Http.Json;
using Blazored.LocalStorage;
using GlucoTrack.Shared.DTOs.Auth;

namespace GlucoTrack.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;

    public AuthService(HttpClient http, ILocalStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode)
            return (false, "Неверный email или пароль");

        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (tokens is null) return (false, "Ошибка сервера");

        await _storage.SetItemAsync("access_token", tokens.AccessToken);
        await _storage.SetItemAsync("refresh_token", tokens.RefreshToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(string email, string password, string inviteCode)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, inviteCode));

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            return (false, "Ошибка регистрации. Проверьте инвайт-код.");
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (tokens is null) return (false, "Ошибка сервера");

        await _storage.SetItemAsync("access_token", tokens.AccessToken);
        await _storage.SetItemAsync("refresh_token", tokens.RefreshToken);
        return (true, null);
    }

    public async Task LogoutAsync()
    {
        await _storage.RemoveItemAsync("access_token");
        await _storage.RemoveItemAsync("refresh_token");
    }

    public async Task<string?> GetAccessTokenAsync() =>
        await _storage.GetItemAsync<string>("access_token");
}
