using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using RmMcp.Soap;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// End-to-end tests exercising PRODUCTION code (RmSoapClient, SoapEnvelopeBuilder,
/// SoapResponseParser) through a FULL authenticated CRUD cycle against the spec-driven
/// RM.Host.Service emulator.
///
/// The emulator implements the exact protocol observed from the live RM at
/// aplicapcapitalizacao182671.rm.cloudtotvs.com.br:1356:
///   - OAuth /connect/token with Basic Auth (FE016 without, FE005 wrong creds)
///   - SOAP DataServer at /wsDataServer/IwsDataServer with proper envelope format
///   - REST CRUD at /api/framework/v1/companies with {hasNext, items} wrapping
///   - Real Swagger specs served at /api/swagger/docs/{module}
///
/// This is NOT a mock test — it exercises the real HTTP stack, real auth flow,
/// and real XML/JSON parsing through our production classes.
/// </summary>
[Trait("Category", "SpecRmE2E")]
public class SpecRmEndToEndTests : IDisposable
{
    private const string BaseUrl = "http://localhost:8060";
    private const string User = "mestre";
    private const string Pass = "totvs";

    private readonly HttpClient _http;
    private readonly RmSoapClient _soap;

    public SpecRmEndToEndTests()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(10) };
        _soap = new RmSoapClient(BaseUrl, User, Pass);
    }

    // ── Phase 1: OAuth Authentication ─────────────────────────────────

    [Fact]
    public async Task Phase1_OAuth_WithoutBasicAuth_Returns400_FE016()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = User,
            ["password"] = Pass,
        });
        var response = await _http.PostAsync("/api/connect/token", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("FE016", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Phase1_OAuth_WithBasicAuth_WrongCreds_Returns401_FE005()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/connect/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:creds")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "wrong",
            ["password"] = "creds",
        });
        var response = await _http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("FE005", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Phase1_OAuth_WithValidCreds_Returns200_WithToken()
    {
        var token = await AcquireTokenAsync();
        Assert.False(string.IsNullOrEmpty(token), "OAuth token should not be empty");
    }

    // ── Phase 2: REST Company CRUD with Bearer Token ──────────────────

    [Fact]
    public async Task Phase2_REST_ReadCompanies_WithBearer_ReturnsWrappedList()
    {
        var token = await AcquireTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(json.TryGetProperty("hasNext", out var hasNext));
        Assert.False(hasNext.GetBoolean());
        Assert.True(json.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() >= 2);

        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("federalId", out _));
    }

    [Fact]
    public async Task Phase2_REST_CreateCompany_Returns201()
    {
        var token = await AcquireTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new StringContent(
            JsonSerializer.Serialize(new { name = "E2E Test Corp", federalId = "99999999000199", active = true }),
            Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/framework/v1/companies", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.TryGetProperty("id", out _));
        Assert.Equal("E2E Test Corp", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Phase2_REST_UpdateCompany_Returns200()
    {
        var token = await AcquireTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new StringContent(
            JsonSerializer.Serialize(new { name = "Updated Corp" }),
            Encoding.UTF8, "application/json");

        var response = await _http.PutAsync("/api/framework/v1/companies/1", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Phase2_REST_DeleteCompany_Returns204()
    {
        var token = await AcquireTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.DeleteAsync("/api/framework/v1/companies/999");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Phase2_REST_WithoutAuth_Returns401()
    {
        var response = await _http.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Phase 3: SOAP DataServer via Production RmSoapClient ──────────

    [Fact]
    public async Task Phase3_SOAP_GetSchema_ViaProductionClient_ReturnsXsd()
    {
        var envelope = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("Contexto", "CODCOLIGADA=1"));

        var response = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetSchema"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetSchema");
        Assert.Contains("xs:schema", result);
        Assert.Contains("CODCOLIGADA", result);
        Assert.Contains("NOME", result);
    }

    [Fact]
    public async Task Phase3_SOAP_ReadView_ViaProductionClient_ReturnsData()
    {
        var envelope = SoapEnvelopeBuilder.Build("ReadView",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("Filtro", "1=1"),
            new SoapParam("Contexto", "CODCOLIGADA=1"));

        var response = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadView"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ReadView");
        var records = SoapResponseParser.ParseDataXml(result);
        Assert.True(records.Count >= 2, $"Expected ≥2 records, got {records.Count}");
        Assert.Equal("MIP Engenharia S.A.", records[0]["NOME"]);
        Assert.Equal("1", records[0]["CODCOLIGADA"]);
    }

    [Fact]
    public async Task Phase3_SOAP_ReadRecord_ViaProductionClient_ReturnsSingleRecord()
    {
        var envelope = SoapEnvelopeBuilder.Build("ReadRecord",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("PrimaryKey", "1"),
            new SoapParam("Contexto", "CODCOLIGADA=1"));

        var response = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ReadRecord");
        var records = SoapResponseParser.ParseDataXml(result);
        Assert.Single(records);
    }

    [Fact]
    public async Task Phase3_SOAP_SaveRecord_ViaProductionClient_ReturnsId()
    {
        var recordXml = @"<GlbColigadaData>
  <GCOLIGADA>
    <CODCOLIGADA>99</CODCOLIGADA>
    <NOME>E2E SOAP Test</NOME>
  </GCOLIGADA>
</GlbColigadaData>";

        var envelope = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("XML", recordXml, IsCdata: true),
            new SoapParam("Contexto", "CODCOLIGADA=99"));

        var response = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("SaveRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "SaveRecord");
        Assert.Equal("1", result);
    }

    [Fact]
    public async Task Phase3_SOAP_Ping_ViaProductionClient_Succeeds()
    {
        var response = await _soap.PingAsync();
        Assert.Contains("GetSchemaResult", response);
    }

    // ── Phase 4: SOAP Process via Production Code ─────────────────────

    [Fact]
    public async Task Phase4_SOAP_ProcessExecute_ViaProductionClient()
    {
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "EstAprovarMov"),
            new SoapParam("strXmlParams", "<params><CODCOLIGADA>1</CODCOLIGADA></params>", IsCdata: true));

        var response = await _soap.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");
        Assert.Contains("JobResult", result);
        Assert.Contains("Completed", result);
    }

    // ── Phase 5: Swagger Spec Serving (no auth, matching live RM) ─────

    [Fact]
    public async Task Phase5_SwaggerSpec_ServedWithoutAuth()
    {
        var response = await _http.GetAsync("/api/swagger/docs/framework");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("/api", json.GetProperty("basePath").GetString());
        Assert.Equal("2.0", json.GetProperty("swagger").GetString());
        Assert.True(json.GetProperty("paths").EnumerateObject().Count() >= 100);
    }

    [Fact]
    public async Task Phase5_AllModulesAvailable()
    {
        var modules = new[] { "framework", "backoffice", "construction", "educational", "hcg", "engineering", "rh", "others" };
        foreach (var module in modules)
        {
            var response = await _http.GetAsync($"/api/swagger/docs/{module}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ── Phase 6: Full CRUD Cycle (OAuth → REST Create → Read → Update → Delete) ──

    [Fact]
    public async Task Phase6_FullCrudCycle_OAuth_Create_Read_Update_Delete()
    {
        // 1. Authenticate via OAuth
        var token = await AcquireTokenAsync();
        Assert.False(string.IsNullOrEmpty(token));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Create
        var createPayload = new StringContent(
            JsonSerializer.Serialize(new { name = "CRUD Test Corp", federalId = "55555555000155", active = true }),
            Encoding.UTF8, "application/json");
        var createResponse = await _http.PostAsync("/api/framework/v1/companies", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonSerializer.Deserialize<JsonElement>(await createResponse.Content.ReadAsStringAsync());
        var newId = created.GetProperty("id").GetInt32();
        Assert.True(newId > 0);

        // 3. Read back (list should contain the new company)
        var listResponse = await _http.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = JsonSerializer.Deserialize<JsonElement>(await listResponse.Content.ReadAsStringAsync());
        Assert.True(list.GetProperty("items").GetArrayLength() >= 3,
            "After create, should have ≥3 companies");

        // 4. Update
        var updatePayload = new StringContent(
            JsonSerializer.Serialize(new { name = "CRUD Updated Corp" }),
            Encoding.UTF8, "application/json");
        var updateResponse = await _http.PutAsync($"/api/framework/v1/companies/{newId}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 5. Delete
        var deleteResponse = await _http.DeleteAsync($"/api/framework/v1/companies/{newId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    // ── Phase 7: Full SOAP Cycle (GetSchema → ReadView → SaveRecord) ──

    [Fact]
    public async Task Phase7_FullSoapCycle_Schema_Read_Write()
    {
        // 1. GetSchema — learn the structure
        var schemaEnvelope = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("Contexto", "CODCOLIGADA=1"));
        var schemaResponse = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetSchema"),
            schemaEnvelope);
        var schema = SoapResponseParser.ExtractResult(schemaResponse, "GetSchema");
        Assert.Contains("CODCOLIGADA", schema);

        // 2. ReadView — read existing data
        var readEnvelope = SoapEnvelopeBuilder.Build("ReadView",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("Filtro", "1=1"),
            new SoapParam("Contexto", "CODCOLIGADA=1"));
        var readResponse = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadView"),
            readEnvelope);
        var records = SoapResponseParser.ParseDataXml(
            SoapResponseParser.ExtractResult(readResponse, "ReadView"));
        Assert.True(records.Count >= 1);

        // 3. SaveRecord — write new data
        var saveEnvelope = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("XML", "<GlbColigadaData><GCOLIGADA><CODCOLIGADA>99</CODCOLIGADA><NOME>SOAP Cycle Test</NOME></GCOLIGADA></GlbColigadaData>", IsCdata: true),
            new SoapParam("Contexto", "CODCOLIGADA=99"));
        var saveResponse = await _soap.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("SaveRecord"),
            saveEnvelope);
        var saveResult = SoapResponseParser.ExtractResult(saveResponse, "SaveRecord");
        Assert.Equal("1", saveResult);
    }

    // ── Phase 8: Cross-Protocol (OAuth → Bearer REST + Basic SOAP) ────

    [Fact]
    public async Task Phase8_CrossProtocol_OAuthToken_ThenRestAndSoap()
    {
        // OAuth token
        var token = await AcquireTokenAsync();
        Assert.False(string.IsNullOrEmpty(token));

        // REST with Bearer
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var restResponse = await _http.GetAsync("/api/framework/v1/companies");
        Assert.Equal(HttpStatusCode.OK, restResponse.StatusCode);
        var companies = JsonSerializer.Deserialize<JsonElement>(await restResponse.Content.ReadAsStringAsync());
        Assert.True(companies.GetProperty("items").GetArrayLength() >= 1);

        // SOAP with Basic Auth (RmSoapClient uses Basic)
        var soapResponse = await _soap.PingAsync();
        Assert.Contains("GetSchemaResult", soapResponse);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private async Task<string> AcquireTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/connect/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:{Pass}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = User,
            ["password"] = Pass,
        });
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        return json.GetProperty("access_token").GetString()!;
    }

    public void Dispose()
    {
        _http.Dispose();
        _soap.Dispose();
    }
}
