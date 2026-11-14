using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Cross-validation tests that exercise the SAME HTTP requests the persontup/mcp-totvs-rm
/// RMRestClient (a third-party RM client built by T-UP Soluções against a real RM.Host.Service)
/// makes, verifying our mock returns responses in the format that client expects.
///
/// These tests replicate the exact URL patterns, HTTP methods, auth mechanisms, and response
/// parsing that persontup's rm_rest.py performs, proving protocol compatibility with a real
/// RM client that was NOT written by us.
///
/// Source: persontup/mcp-totvs-rm:1.0 Docker image, /app/core/rm_rest.py
/// Author: personcunha@t-upsolucoes.com.br (T-UP Soluções)
/// Evidence: 10/10 operations pass when running their actual Python client against our mock.
/// </summary>
[Collection("Docker")]
[Trait("Category", "CrossValidation")]
public class PersontupCrossValidationTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _user;
    private readonly string _pass;

    public PersontupCrossValidationTests(DockerFixture docker)
    {
        _user = docker.SoapUser;
        _pass = docker.SoapPass;
        _client = new HttpClient
        {
            BaseAddress = new Uri(docker.SoapBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
        SetBasicAuth();
    }

    // ─── persontup rm_rest.py: get_available_services ─────────────────
    // URL: /RMSRestDataServer/getAvailableServices?start=0&limit=50
    // Expects: JSON array/object from response.json()

    [Fact]
    public async Task GetAvailableServices_ReturnsJsonList()
    {
        var response = await _client.GetAsync("/RMSRestDataServer/getAvailableServices?start=0&limit=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.ValueKind == JsonValueKind.Array);
        Assert.True(json.GetArrayLength() > 0);
    }

    // ─── persontup rm_rest.py: get_service_schema ─────────────────────
    // URL: /RMSRestDataServer/service/{DataServer}/schema
    // Expects: JSON from response.json()

    [Fact]
    public async Task GetServiceSchema_ReturnsJsonWithFields()
    {
        var response = await _client.GetAsync("/RMSRestDataServer/service/GlbColigadaData/schema");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.TryGetProperty("fields", out var fields));
        Assert.True(fields.GetArrayLength() > 0);

        var first = fields[0];
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("type", out _));
    }

    // ─── persontup rm_rest.py: get_wadl ───────────────────────────────
    // URL: /RMSRestDataServer/service/{DataServer}
    // Expects: text from response.text (WADL XML)

    [Fact]
    public async Task GetWadl_ReturnsApplicationXml()
    {
        var response = await _client.GetAsync("/RMSRestDataServer/service/GlbColigadaData");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<application", body);
    }

    // ─── persontup rm_rest.py: get_all ────────────────────────────────
    // URL: /RMSRestDataServer/rest/{DataServer}?start=0&limit=50&filter=...
    // Expects: JSON list/object from response.json()

    [Fact]
    public async Task GetAll_ReturnsJsonRecords()
    {
        var response = await _client.GetAsync(
            "/RMSRestDataServer/rest/GlbColigadaData?start=0&limit=10&filter=1%3D1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.ValueKind == JsonValueKind.Array || json.ValueKind == JsonValueKind.Object);
    }

    // ─── persontup rm_rest.py: get (by ID) ────────────────────────────
    // URL: /RMSRestDataServer/rest/{DataServer}/{id}
    // Expects: JSON object from response.json()

    [Fact]
    public async Task GetById_ReturnsJsonObject()
    {
        var response = await _client.GetAsync("/RMSRestDataServer/rest/GlbColigadaData/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.True(json.TryGetProperty("CODCOLIGADA", out _));
    }

    // ─── persontup rm_rest.py: create ─────────────────────────────────
    // URL: POST /RMSRestDataServer/rest/{DataServer}
    // Expects: HTTP 201, JSON from response.json()

    [Fact]
    public async Task Create_Returns201WithJson()
    {
        var content = new StringContent(
            "{\"CODCOLIGADA\":3,\"NOME\":\"Test Create\"}",
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/RMSRestDataServer/rest/GlbColigadaData", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
    }

    // ─── persontup rm_rest.py: update ─────────────────────────────────
    // URL: PUT /RMSRestDataServer/rest/{DataServer}/{id}
    // Expects: HTTP 200, JSON from response.json()

    [Fact]
    public async Task Update_Returns200WithJson()
    {
        var content = new StringContent(
            "{\"NOME\":\"Updated\"}",
            Encoding.UTF8, "application/json");

        var response = await _client.PutAsync("/RMSRestDataServer/rest/GlbColigadaData/1", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.TryGetProperty("NOME", out _));
    }

    // ─── persontup rm_rest.py: update_partial ─────────────────────────
    // URL: PATCH /RMSRestDataServer/rest/{DataServer}/{id}
    // Expects: HTTP 200, JSON from response.json()

    [Fact]
    public async Task UpdatePartial_Returns200WithJson()
    {
        var content = new StringContent(
            "{\"NOME\":\"Patched\"}",
            Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod("PATCH"),
            "/RMSRestDataServer/rest/GlbColigadaData/1")
        {
            Content = content,
        };
        CopyAuth(request);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── persontup rm_rest.py: delete ─────────────────────────────────
    // URL: DELETE /RMSRestDataServer/rest/{DataServer}/{id}
    // Expects: HTTP 204

    [Fact]
    public async Task Delete_Returns204()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            "/RMSRestDataServer/rest/GlbColigadaData/1");
        CopyAuth(request);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ─── persontup rm_rest.py: set_token (Bearer auth) ────────────────
    // After acquiring OAuth token, client switches from Basic to Bearer

    [Fact]
    public async Task BearerToken_WorksAfterOAuth()
    {
        // Acquire token (OAuth flow)
        var tokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = _user,
            ["password"] = _pass,
        });
        var tokenResponse = await _client.PostAsync("/api/connect/token", tokenContent);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenBody);
        var token = tokenJson.GetProperty("access_token").GetString()!;

        // Switch to Bearer auth (like persontup's set_token)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Confirm Bearer auth works for data access
        var response = await _client.GetAsync("/RMSRestDataServer/rest/GlbColigadaData?start=0&limit=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private void SetBasicAuth()
    {
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
    }

    private void CopyAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
