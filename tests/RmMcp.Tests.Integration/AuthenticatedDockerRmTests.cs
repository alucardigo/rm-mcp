using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Authenticated end-to-end tests against the Docker RM.Host.Service emulator.
/// These tests validate the FULL authentication pipeline:
///   1. OAuth token acquisition (POST /api/connect/token → access_token)
///   2. REST API with Bearer auth (GET /api/framework/v1/companies → 200 + items)
///   3. RMSRestDataServer with Basic auth (GET /RMSRestDataServer/rest/{ds}/{action} → 200)
///   4. SOAP DataServer with Basic auth (POST /wsDataServer/IwsDataServer → SOAP response)
///   5. SOAP Process with Basic auth (POST /wsProcess/IwsProcess → SOAP response)
///
/// The Docker stack (docker-compose.yml) provides:
///   - SQL Server 2022 with Corpore-schema seed (init-corpore.sql)
///   - RM.Host.Service protocol emulator (mock-soap) implementing full RM auth + API surface
///   - Credentials: mestre/totvs (matching real RM default admin)
/// </summary>
[Collection("Docker")]
[Trait("Category", "AuthenticatedRM")]
public class AuthenticatedDockerRmTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly string _user;
    private readonly string _pass;

    public AuthenticatedDockerRmTests(DockerFixture docker)
    {
        _baseUrl = docker.SoapBaseUrl;
        _user = docker.SoapUser;
        _pass = docker.SoapPass;

        _client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    // ─── OAuth Token Flow ─────────────────────────────────────────────

    [Fact]
    public async Task OAuth_AcquiresToken_WithValidCredentials()
    {
        var token = await GetOAuthToken();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task OAuth_RejectsInvalidCredentials()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "invalid_user",
            ["password"] = "invalid_pass",
        });

        var response = await _client.PostAsync("/api/connect/token", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── REST API with Bearer Auth ────────────────────────────────────

    [Fact]
    public async Task RestApi_Companies_ReturnsData_WithBearerToken()
    {
        var token = await GetOAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(json.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() > 0);

        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("nomeFantasia", out _));
    }

    [Fact]
    public async Task RestApi_Branches_ReturnsData_WithBearerToken()
    {
        var token = await GetOAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/framework/v1/Branches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() > 0);
    }

    [Fact]
    public async Task RestApi_RejectsExpiredBearerToken()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "expired-fake-token");

        var response = await _client.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestApi_RejectsNoAuth()
    {
        var response = await _client.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── RMSRestDataServer with Basic Auth ────────────────────────────

    [Fact]
    public async Task DataServerRest_ReadView_ReturnsData_WithBasicAuth()
    {
        SetBasicAuth();

        var response = await _client.GetAsync(
            "/RMSRestDataServer/rest/GlbColigadaData/ReadView?CodColigada=1&Filtro=1%3D1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CODCOLIGADA", body);
    }

    [Fact]
    public async Task DataServerRest_AvailableServices_ListsDataServers()
    {
        SetBasicAuth();

        var response = await _client.GetAsync("/RMSRestDataServer/getAvailableServices");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.GetArrayLength() > 0);
    }

    [Fact]
    public async Task DataServerRest_Schema_ReturnsXsd()
    {
        SetBasicAuth();

        var response = await _client.GetAsync("/RMSRestDataServer/service/GlbColigadaData/schema");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("xs:schema", body);
        Assert.Contains("CODCOLIGADA", body);
    }

    [Fact]
    public async Task DataServerRest_Write_AcceptedWithBasicAuth()
    {
        SetBasicAuth();

        var content = new StringContent("{\"CODCOLIGADA\":1}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/RMSRestDataServer/rest/GlbColigadaData/SaveRecord", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── SOAP DataServer ──────────────────────────────────────────────

    [Fact]
    public async Task SoapDataServer_ReadView_ReturnsXmlData()
    {
        var envelope = BuildSoapEnvelope("ReadView", "GlbColigadaData",
            "<tot:Filtro>1=1</tot:Filtro><tot:Contexto>codColigada=1;codSistema=G</tot:Contexto>");

        var response = await SendSoap("/wsDataServer/IwsDataServer",
            "http://www.totvs.com/IwsDataServer/ReadView", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ReadViewResult", body);
        Assert.Contains("CODCOLIGADA", body);
    }

    [Fact]
    public async Task SoapDataServer_GetSchema_ReturnsXsd()
    {
        var envelope = BuildSoapEnvelope("GetSchema", "GlbColigadaData",
            "<tot:Contexto>codColigada=1;codSistema=G</tot:Contexto>");

        var response = await SendSoap("/wsDataServer/IwsDataServer",
            "http://www.totvs.com/IwsDataServer/GetSchema", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GetSchemaResult", body);
        Assert.Contains("xs:schema", body);
    }

    [Fact]
    public async Task SoapDataServer_SaveRecord_AcceptedWithAuth()
    {
        var envelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tot=""http://www.totvs.com/"">
  <soap:Body>
    <tot:SaveRecord>
      <tot:DataServerName>GlbColigadaData</tot:DataServerName>
      <tot:XML><![CDATA[<GlbColigadaData><GCOLIGADA><CODCOLIGADA>1</CODCOLIGADA><NOME>Test</NOME></GCOLIGADA></GlbColigadaData>]]></tot:XML>
      <tot:Contexto>codColigada=1;codSistema=G</tot:Contexto>
    </tot:SaveRecord>
  </soap:Body>
</soap:Envelope>";

        var response = await SendSoap("/wsDataServer/IwsDataServer",
            "http://www.totvs.com/IwsDataServer/SaveRecord", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SaveRecordResult", body);
    }

    // ─── SOAP Process ─────────────────────────────────────────────────

    [Fact]
    public async Task SoapProcess_Execute_ReturnsJobResult()
    {
        var envelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tot=""http://www.totvs.com/"">
  <soap:Body>
    <tot:ExecuteWithXmlParams>
      <tot:ProcessServerName>EstAprovarMov</tot:ProcessServerName>
      <tot:strXmlParams><![CDATA[<Params><CodColigada>1</CodColigada></Params>]]></tot:strXmlParams>
    </tot:ExecuteWithXmlParams>
  </soap:Body>
</soap:Envelope>";

        var response = await SendSoap("/wsProcess/IwsProcess",
            "http://www.totvs.com/IwsProcess/ExecuteWithXmlParams", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ExecuteWithXmlParamsResult", body);
        Assert.Contains("JobResult", body);
    }

    [Fact]
    public async Task SoapProcess_GetJobParams_ReturnsStatus()
    {
        var envelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tot=""http://www.totvs.com/"">
  <soap:Body>
    <tot:GetJobParams>
      <tot:JobId>12345</tot:JobId>
    </tot:GetJobParams>
  </soap:Body>
</soap:Envelope>";

        var response = await SendSoap("/wsProcess/IwsProcess",
            "http://www.totvs.com/IwsProcess/GetJobParams", envelope);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GetJobParamsResult", body);
        Assert.Contains("Running", body);
    }

    // ─── SOAP Rejects Bad Auth ────────────────────────────────────────

    [Fact]
    public async Task SoapDataServer_RejectsNoAuth()
    {
        var envelope = BuildSoapEnvelope("ReadView", "GlbColigadaData",
            "<tot:Filtro>1=1</tot:Filtro><tot:Contexto>codColigada=1</tot:Contexto>");

        var request = new HttpRequestMessage(HttpMethod.Post, "/wsDataServer/IwsDataServer")
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction",
            "\"http://www.totvs.com/IwsDataServer/ReadView\"");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Full Flow: Token → REST → SOAP → Data ───────────────────────

    [Fact]
    public async Task FullFlow_Token_Then_RestAndSoap_ReturnsConsistentData()
    {
        // Step 1: Get OAuth token
        var token = await GetOAuthToken();
        Assert.NotNull(token);

        // Step 2: REST API with Bearer token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var restResponse = await _client.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.OK, restResponse.StatusCode);
        var restBody = await restResponse.Content.ReadAsStringAsync();
        var restJson = JsonSerializer.Deserialize<JsonElement>(restBody);
        Assert.True(restJson.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() >= 2);

        // Step 3: SOAP DataServer with Basic auth (different auth mechanism, same server)
        _client.DefaultRequestHeaders.Authorization = null;
        var soapEnvelope = BuildSoapEnvelope("ReadView", "GlbColigadaData",
            "<tot:Filtro>1=1</tot:Filtro><tot:Contexto>codColigada=1;codSistema=G</tot:Contexto>");
        var soapResponse = await SendSoap("/wsDataServer/IwsDataServer",
            "http://www.totvs.com/IwsDataServer/ReadView", soapEnvelope);
        Assert.Equal(HttpStatusCode.OK, soapResponse.StatusCode);
        var soapBody = await soapResponse.Content.ReadAsStringAsync();
        Assert.Contains("MIP Engenharia", soapBody);

        // Step 4: DataServer REST with Basic auth
        SetBasicAuth();
        var dsResponse = await _client.GetAsync(
            "/RMSRestDataServer/rest/GlbColigadaData/ReadView?CodColigada=1&Filtro=1%3D1");
        Assert.Equal(HttpStatusCode.OK, dsResponse.StatusCode);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private async Task<string> GetOAuthToken()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = _user,
            ["password"] = _pass,
        });

        var response = await _client.PostAsync("/api/connect/token", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        return json.GetProperty("access_token").GetString()!;
    }

    private void SetBasicAuth()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private async Task<HttpResponseMessage> SendSoap(string path, string action, string envelope)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return await _client.SendAsync(request);
    }

    private static string BuildSoapEnvelope(string operation, string dataServerName, string extraElements)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tot=""http://www.totvs.com/"">
  <soap:Body>
    <tot:{operation}>
      <tot:DataServerName>{dataServerName}</tot:DataServerName>
      {extraElements}
    </tot:{operation}>
  </soap:Body>
</soap:Envelope>";
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
