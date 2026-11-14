using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// End-to-end tests: persontup/mcp-totvs-rm bridge (real third-party RM MCP client)
/// → RMRestClient → HTTP → mock RM.Host.Service → response.
///
/// The bridge is an independent Python implementation that reads Swagger specs
/// extracted from real TOTVS Cloud RM and builds MCP tools from them.
/// If these tests pass, it proves our mock serves responses that a real RM client accepts.
/// </summary>
[Collection("Docker")]
public class BridgeIntegrationTests
{
    private readonly HttpClient _bridge;
    private readonly HttpClient _mock;
    private readonly DockerFixture _docker;

    public BridgeIntegrationTests(DockerFixture docker)
    {
        _docker = docker;
        _bridge = new HttpClient { BaseAddress = new Uri(docker.BridgeBaseUrl) };
        _mock = new HttpClient { BaseAddress = new Uri(docker.SoapBaseUrl) };
    }

    [Fact]
    public async Task Bridge_Health_Returns_Ok()
    {
        var response = await _bridge.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Bridge_Token_AcquiresOAuthToken_FromMock()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _bridge.PostAsync("/token", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("access_token", out var token));
        Assert.False(string.IsNullOrEmpty(token.GetString()));
    }

    [Fact]
    public async Task Bridge_ListsTools_FromSwaggerSpecs()
    {
        var response = await _bridge.GetAsync("/mcp/tools");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tools = json.GetProperty("tools");

        // persontup builds one MCP tool per Swagger path+method.
        // framework.json (200 paths) + backoffice.json (680 paths) = 880+ paths,
        // many with multiple methods → 1400+ tools.
        Assert.True(tools.GetArrayLength() >= 800,
            $"Expected >= 800 tools from Swagger specs, got {tools.GetArrayLength()}");
    }

    [Fact]
    public async Task Bridge_McpCall_Companies_ReturnsData()
    {
        // Authenticate first
        await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var call = new
        {
            service = "framework",
            path = "/framework/v1/companies",
            method = "get"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(call), Encoding.UTF8, "application/json");

        var response = await _bridge.PostAsync("/mcp/call", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("result");

        Assert.Equal(200, result.GetProperty("status").GetInt32());
        var data = result.GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() > 0);

        var first = items[0];
        Assert.True(first.TryGetProperty("nomeFantasia", out _));
        Assert.True(first.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Bridge_McpCall_Branches_ReturnsApiQueryResponse()
    {
        await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var call = new
        {
            service = "framework",
            path = "/framework/v1/Branches",
            method = "get"
        };

        var response = await _bridge.PostAsync("/mcp/call",
            new StringContent(JsonSerializer.Serialize(call), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("result").GetProperty("data");

        Assert.True(data.TryGetProperty("hasNext", out _));
        Assert.True(data.TryGetProperty("total", out _));
        Assert.True(data.TryGetProperty("items", out _));
    }

    [Theory]
    [InlineData("framework", "/framework/v1/users", "get")]
    [InlineData("backoffice", "/financial/v1/cashaccounts", "get")]
    [InlineData("backoffice", "/ctb/v1/CostCenters", "get")]
    [InlineData("backoffice", "/mov/v1/Movements", "get")]
    public async Task Bridge_McpCall_MultiModule_ReturnsSuccess(
        string service, string path, string method)
    {
        await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var call = new { service, path, method };

        var response = await _bridge.PostAsync("/mcp/call",
            new StringContent(JsonSerializer.Serialize(call), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("result");
        Assert.Equal(200, result.GetProperty("status").GetInt32());
    }

    /// <summary>
    /// Cross-validation: same query through our mock (direct HTTP) AND through
    /// the persontup bridge. Both must return the same company data, proving
    /// our mock serves the real RM REST API contract as understood by an
    /// independent RM client implementation.
    /// </summary>
    [Fact]
    public async Task CrossValidation_BridgeAndDirect_ReturnSameCompanies()
    {
        // 1. Direct call to mock (how our MCP tools call RM)
        _mock.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_docker.SoapUser}:{_docker.SoapPass}")));

        var directResponse = await _mock.GetAsync("/framework/v1/companies");
        var directJson = await directResponse.Content.ReadFromJsonAsync<JsonElement>();
        var directItems = directJson.GetProperty("items");

        // 2. Bridge call (how persontup/mcp-totvs-rm calls RM)
        await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var bridgeResponse = await _bridge.PostAsync("/mcp/call",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    service = "framework",
                    path = "/framework/v1/companies",
                    method = "get"
                }),
                Encoding.UTF8, "application/json"));
        var bridgeJson = await bridgeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bridgeItems = bridgeJson.GetProperty("result").GetProperty("data").GetProperty("items");

        // 3. Same data from both paths
        Assert.Equal(directItems.GetArrayLength(), bridgeItems.GetArrayLength());

        for (int i = 0; i < directItems.GetArrayLength(); i++)
        {
            var d = directItems[i];
            var b = bridgeItems[i];

            Assert.Equal(
                d.GetProperty("nomeFantasia").GetString(),
                b.GetProperty("nomeFantasia").GetString());
            Assert.Equal(
                d.GetProperty("id").GetInt32(),
                b.GetProperty("id").GetInt32());
        }
    }

    [Fact]
    public async Task CrossValidation_BridgeAndDirect_ReturnSameBranches()
    {
        _mock.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_docker.SoapUser}:{_docker.SoapPass}")));

        var directResponse = await _mock.GetAsync("/framework/v1/Branches");
        var directJson = await directResponse.Content.ReadFromJsonAsync<JsonElement>();
        var directTotal = directJson.GetProperty("total").GetInt32();

        await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var bridgeResponse = await _bridge.PostAsync("/mcp/call",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    service = "framework",
                    path = "/framework/v1/Branches",
                    method = "get"
                }),
                Encoding.UTF8, "application/json"));
        var bridgeJson = await bridgeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var bridgeTotal = bridgeJson.GetProperty("result").GetProperty("data").GetProperty("total").GetInt32();

        Assert.Equal(directTotal, bridgeTotal);
    }

    [Fact]
    public async Task CrossValidation_OAuthContract_BothPathsWork()
    {
        // Direct OAuth call (our tools' auth flow)
        var directPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = _docker.SoapUser,
            ["password"] = _docker.SoapPass,
        });
        _mock.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_docker.SoapUser}:{_docker.SoapPass}")));
        var directResponse = await _mock.PostAsync("/connect/token", directPayload);
        var directToken = (await directResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString();

        // Bridge OAuth call (persontup's auth flow via conecta.py)
        var bridgeResponse = await _bridge.PostAsync("/token",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var bridgeToken = (await bridgeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString();

        // Both get valid tokens from the same mock
        Assert.False(string.IsNullOrEmpty(directToken));
        Assert.False(string.IsNullOrEmpty(bridgeToken));
        Assert.Equal(directToken, bridgeToken);
    }
}
