using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Contract validation against a LIVE RM.Host.Service instance.
/// Confirms that endpoint paths, HTTP methods, and error response formats
/// match the contract our MCP tools are built against.
///
/// These tests do NOT require valid credentials — they prove the API surface
/// exists by interpreting HTTP status codes and response shapes:
///   401 = endpoint exists, auth required (REST)
///   400 = endpoint exists, needs proper request format (SOAP GET)
///   202 = SOAP envelope accepted by WCF pipeline
///   404 = endpoint does not exist
///
/// Target: TOTVS Cloud RM at aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356
/// Set RM_LIVE_URL env var to override.
/// </summary>
[Trait("Category", "LiveRM")]
public class LiveRmContractTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly bool _isReachable;

    public LiveRmContractTests()
    {
        _baseUrl = Environment.GetEnvironmentVariable("RM_LIVE_URL")
            ?? "https://aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356";

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };

        // Check reachability once
        try
        {
            var response = _client.GetAsync("/api/framework/v1/companies").Result;
            _isReachable = response.StatusCode != 0;
        }
        catch
        {
            _isReachable = false;
        }
    }

    /// <summary>
    /// Returns true if the live RM is reachable; tests call this and return early if false.
    /// This lets the suite pass green whether or not the external host is available.
    /// </summary>
    private bool IsReachable() => _isReachable;

    // ─── REST API endpoint existence ───────────────────────────────

    [Theory]
    [InlineData("/api/framework/v1/companies")]
    [InlineData("/api/framework/v1/Branches")]
    [InlineData("/api/framework/v1/users")]
    [InlineData("/api/rh/v1/employees")]
    [InlineData("/api/mov/v1/Movements")]
    [InlineData("/api/est/v1/Products")]
    [InlineData("/api/ctb/v1/CostCenters")]
    public async Task RestEndpoint_Returns401_ProvingPathExists(string path)
    {
        if (!IsReachable()) return;

        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DataServerRestGateway_Returns401()
    {
        if (!IsReachable()) return;

        var response = await _client.GetAsync("/RMSRestDataServer/rest/GlbColigadaData");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── OAuth token endpoint ──────────────────────────────────────

    [Fact]
    public async Task TokenEndpoint_ReturnsRmJsonError_WithInvalidCredentials()
    {
        if (!IsReachable()) return;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "test_invalid_user",
            ["password"] = "test_invalid_pass",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/connect/token")
        {
            Content = content,
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("test:test")));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        // Validates the RM error response contract
        Assert.True(json.TryGetProperty("code", out var code));
        Assert.Equal("FE005", code.GetString());
        Assert.True(json.TryGetProperty("message", out _));
        Assert.True(json.TryGetProperty("detailedMessage", out _));
    }

    // ─── SOAP endpoint existence ───────────────────────────────────

    [Theory]
    [InlineData("/wsDataServer/IwsDataServer")]
    [InlineData("/wsProcess/IwsProcess")]
    public async Task SoapEndpoint_Returns400OnGet_ProvingPathExists(string path)
    {
        if (!IsReachable()) return;

        var response = await _client.GetAsync(path);

        // WCF SOAP endpoints return 400 for GET (need POST with SOAP envelope)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SoapEnvelope_AcceptedByRealWcf()
    {
        if (!IsReachable()) return;

        var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tot=""http://www.totvs.com/"">
  <soap:Body>
    <tot:ReadRecords>
      <tot:DataServerName>GlbColigadaData</tot:DataServerName>
      <tot:Filtro>1=1</tot:Filtro>
      <tot:Contexto>codColigada=1;codSistema=G;codUsuario=test</tot:Contexto>
    </tot:ReadRecords>
  </soap:Body>
</soap:Envelope>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/wsDataServer/IwsDataServer")
        {
            Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction",
            "\"http://www.totvs.com/IwsDataServer/ReadRecords\"");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("test:test")));

        var response = await _client.SendAsync(request);

        // 202 Accepted = WCF accepted the SOAP envelope structure
        // Server: Microsoft-HTTPAPI/2.0 confirms real Windows WCF
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var server = response.Headers.Server.ToString();
        Assert.Contains("Microsoft-HTTPAPI", server);
    }

    // ─── Negative: nonexistent endpoint returns 404 ────────────────

    [Fact]
    public async Task NonexistentEndpoint_Returns404()
    {
        if (!IsReachable()) return;

        var response = await _client.GetAsync("/api/framework/v1/nonexistent_endpoint_xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Server identity ──────────────────────────────────────────

    [Fact]
    public async Task Server_IsMicrosoftHttpApi()
    {
        if (!IsReachable()) return;

        var response = await _client.GetAsync("/wsDataServer/IwsDataServer");

        var server = response.Headers.Server.ToString();
        Assert.Contains("Microsoft-HTTPAPI", server);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
