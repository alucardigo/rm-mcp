using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Validates our mock RM.Host.Service REST endpoints match the real RM Swagger spec contract.
/// These endpoints (/connect/token, /framework/v1/companies, /framework/v1/Branches)
/// mirror the paths from the Swagger specs extracted from persontup/mcp-totvs-rm:1.0.
/// </summary>
[Collection("Docker")]
public class RestApiContractTests
{
    private readonly DockerFixture _docker;
    private readonly HttpClient _http;

    public RestApiContractTests(DockerFixture docker)
    {
        _docker = docker;
        _http = new HttpClient { BaseAddress = new Uri(docker.SoapBaseUrl) };
    }

    [Fact]
    public async Task ConnectToken_ValidCredentials_ReturnsOAuthResponse()
    {
        var payload = JsonSerializer.Serialize(new
        {
            userName = _docker.SoapUser,
            password = _docker.SoapPass,
            grant_type = "password"
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/connect/token", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // OAuth response fields match real RM Swagger spec (RM.Glb.Api.LoginController.ApiTokenResponse)
        Assert.True(json.TryGetProperty("access_token", out var token));
        Assert.False(string.IsNullOrEmpty(token.GetString()));
        Assert.Equal("bearer", json.GetProperty("token_type").GetString());
        Assert.True(json.GetProperty("expires_in").GetInt32() > 0);
        Assert.True(json.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task ConnectToken_InvalidCredentials_Returns401()
    {
        var payload = JsonSerializer.Serialize(new
        {
            userName = "invalid",
            password = "wrong",
            grant_type = "password"
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/connect/token", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Companies_ReturnsApiQueryResponse()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_docker.SoapUser}:{_docker.SoapPass}")));

        var response = await _http.GetAsync("/framework/v1/companies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // RM.Lib.Api.ApiQueryResponse pattern: hasNext, total, items
        Assert.False(json.GetProperty("hasNext").GetBoolean());
        Assert.Equal(2, json.GetProperty("total").GetInt32());

        var items = json.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        // CompanyDto fields match real Swagger spec
        var mip = items[0];
        Assert.Equal(1, mip.GetProperty("id").GetInt32());
        Assert.Equal("MIP Engenharia", mip.GetProperty("nomeFantasia").GetString());
        Assert.True(mip.TryGetProperty("federalId", out _));
        Assert.True(mip.TryGetProperty("name", out _));
        Assert.True(mip.GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Branches_ReturnsApiQueryResponse()
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_docker.SoapUser}:{_docker.SoapPass}")));

        var response = await _http.GetAsync("/framework/v1/Branches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.GetProperty("hasNext").GetBoolean());
        Assert.Equal(2, json.GetProperty("total").GetInt32());

        var items = json.GetProperty("items");
        var branch = items[0];
        Assert.True(branch.TryGetProperty("companyId", out _));
        Assert.True(branch.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Unauthenticated_GET_Returns401()
    {
        var response = await _http.GetAsync("/framework/v1/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
