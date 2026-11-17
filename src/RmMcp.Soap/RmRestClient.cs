using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RmMcp.Soap;

public sealed class RmRestClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public RmRestClient(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _username = username;
        _password = password;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
                return _accessToken;

            var creds = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_username}:{_password}"));

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/api/connect/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = _username,
                ["password"] = _password,
            });

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"OAuth token failed: {(int)response.StatusCode} — {body}");

            var json = JsonSerializer.Deserialize<JsonElement>(body);
            _accessToken = json.GetProperty("access_token").GetString()!;

            var expiresIn = json.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32() : 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<string> GetAsync(string path, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_baseUrl}/{path.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"REST GET {(int)response.StatusCode}: {body}");

        return body;
    }

    public async Task<string> PostAsync(string path, string jsonBody,
        CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_baseUrl}/{path.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"REST POST {(int)response.StatusCode}: {body}");

        return body;
    }

    public async Task<string> PutAsync(string path, string jsonBody,
        CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"{_baseUrl}/{path.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"REST PUT {(int)response.StatusCode}: {body}");

        return body;
    }

    public async Task<string> DeleteAsync(string path, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{_baseUrl}/{path.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"REST DELETE {(int)response.StatusCode}: {body}");

        return body;
    }

    public async Task<string> PatchAsync(string path, string jsonBody,
        CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{_baseUrl}/{path.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"REST PATCH {(int)response.StatusCode}: {body}");

        return body;
    }

    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
    }
}
