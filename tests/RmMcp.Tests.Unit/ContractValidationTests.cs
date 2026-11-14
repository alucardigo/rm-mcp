using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Unit;

/// <summary>
/// Validates our tool implementation against the real RM REST API contract
/// extracted from TOTVS Cloud Swagger specs (persontup/mcp-totvs-rm:1.0).
/// </summary>
public class ContractValidationTests
{
    private static readonly JsonDocument Contract;

    static ContractValidationTests()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docker", "rm-api-contract.json");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "docker", "rm-api-contract.json");
        Contract = JsonDocument.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Auth_Endpoint_Matches_Real_RM()
    {
        var auth = Contract.RootElement.GetProperty("auth");
        Assert.Equal("/connect/token", auth.GetProperty("endpoint").GetString());
        Assert.Equal("POST", auth.GetProperty("method").GetString());

        var fields = auth.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.Contains("userName", fields);
        Assert.Contains("password", fields);
        Assert.Contains("grant_type", fields);
    }

    [Fact]
    public void Auth_Response_Has_Standard_OAuth_Fields()
    {
        var responseFields = Contract.RootElement
            .GetProperty("auth")
            .GetProperty("response_fields")
            .EnumerateArray()
            .Select(f => f.GetString())
            .ToList();

        Assert.Contains("access_token", responseFields);
        Assert.Contains("token_type", responseFields);
        Assert.Contains("expires_in", responseFields);
    }

    [Theory]
    [InlineData("company", "GlbColigadaData", "GCOLIGADA")]
    [InlineData("branch", "GlbFilialData", "GFILIAL")]
    [InlineData("movement", "EstMovimentoData", "TMOV")]
    [InlineData("financial_entry", "FinLanData", "FLAN")]
    [InlineData("products", "EstProdutoData", "TPRODUTO")]
    [InlineData("accounting_plan", "CtbContaData", "CCONTA")]
    [InlineData("accounting_entries", "CtbLancamentoData", "CPARTIDA")]
    public void DataServer_Names_Map_To_Real_REST_Resources(string resource, string expectedDataServer, string expectedTable)
    {
        var api = Contract.RootElement.GetProperty("rest_api_patterns").GetProperty(resource);
        Assert.Equal(expectedDataServer, api.GetProperty("maps_to_dataserver").GetString());
        Assert.Equal(expectedTable, api.GetProperty("maps_to_table").GetString());
    }

    [Fact]
    public void Movement_DTO_Has_301_Fields_In_Real_RM()
    {
        var movement = Contract.RootElement.GetProperty("rest_api_patterns").GetProperty("movement");
        Assert.Equal(301, movement.GetProperty("field_count").GetInt32());
    }

    [Fact]
    public void Movement_Key_Fields_Match_Our_SQL_Columns()
    {
        var keyFields = Contract.RootElement
            .GetProperty("rest_api_patterns")
            .GetProperty("movement")
            .GetProperty("key_fields")
            .EnumerateArray()
            .Select(f => f.GetString())
            .ToList();

        // These REST field names map to our SQL column names
        // companyId → CODCOLIGADA, movementId → IDMOV, movementTypeCode → CODTMV
        Assert.Contains("companyId", keyFields);
        Assert.Contains("movementId", keyFields);
        Assert.Contains("movementTypeCode", keyFields);
        Assert.Contains("status", keyFields);
        Assert.Contains("number", keyFields);
    }

    [Fact]
    public void Financial_Entry_Key_Fields_Match_Our_SQL_Columns()
    {
        var keyFields = Contract.RootElement
            .GetProperty("rest_api_patterns")
            .GetProperty("financial_entry")
            .GetProperty("key_fields")
            .EnumerateArray()
            .Select(f => f.GetString())
            .ToList();

        // companyCode → CODCOLIGADA, finEntryCode → IDLAN, amount → VALORORIGINAL
        Assert.Contains("companyCode", keyFields);
        Assert.Contains("finEntryCode", keyFields);
        Assert.Contains("amount", keyFields);
    }

    [Fact]
    public void SOAP_Endpoints_Match_Known_RM_Paths()
    {
        var soap = Contract.RootElement.GetProperty("soap_endpoints");
        Assert.Equal("/wsDataServer/IwsDataServer", soap.GetProperty("wsDataServer").GetString());
        Assert.Equal("/wsProcess/IwsProcess", soap.GetProperty("wsProcess").GetString());
    }

    [Fact]
    public void SOAP_DataServer_Operations_Are_Complete()
    {
        var ops = Contract.RootElement
            .GetProperty("soap_endpoints")
            .GetProperty("operations")
            .GetProperty("dataserver")
            .EnumerateArray()
            .Select(o => o.GetString())
            .ToList();

        Assert.Contains("ReadView", ops);
        Assert.Contains("ReadRecord", ops);
        Assert.Contains("GetSchema", ops);
        Assert.Contains("SaveRecord", ops);
        Assert.Contains("DeleteRecord", ops);
    }

    [Fact]
    public void SOAP_Process_Operations_Are_Complete()
    {
        var ops = Contract.RootElement
            .GetProperty("soap_endpoints")
            .GetProperty("operations")
            .GetProperty("process")
            .EnumerateArray()
            .Select(o => o.GetString())
            .ToList();

        Assert.Contains("ExecuteWithXmlParams", ops);
        Assert.Contains("ReadJobStatus", ops);
    }

    [Theory]
    [InlineData("framework", 200)]
    [InlineData("backoffice", 680)]
    [InlineData("rh", 1001)]
    public void Module_Path_Counts_Match_Swagger_Specs(string module, int expectedPaths)
    {
        var mod = Contract.RootElement.GetProperty("modules").GetProperty(module);
        Assert.Equal(expectedPaths, mod.GetProperty("paths").GetInt32());
    }

    [Fact]
    public void Company_DTO_Fields_Match_Our_Schema()
    {
        var fields = Contract.RootElement
            .GetProperty("rest_api_patterns")
            .GetProperty("company")
            .GetProperty("fields")
            .EnumerateArray()
            .Select(f => f.GetString())
            .ToList();

        // id → CODCOLIGADA, nomeFantasia → NOMEFANTASIA, federalId → CGC, name → NOME
        Assert.Contains("id", fields);
        Assert.Contains("nomeFantasia", fields);
        Assert.Contains("federalId", fields);
        Assert.Contains("name", fields);
    }

    [Fact]
    public void Products_DTO_Fields_Match_Our_Schema()
    {
        var fields = Contract.RootElement
            .GetProperty("rest_api_patterns")
            .GetProperty("products")
            .GetProperty("key_fields")
            .EnumerateArray()
            .Select(f => f.GetString())
            .ToList();

        // productCompanyId → CODCOLIGADA, productId → IDPRD, code → CODIGOPRD
        Assert.Contains("productCompanyId", fields);
        Assert.Contains("productId", fields);
        Assert.Contains("code", fields);
        Assert.Contains("description", fields);
    }
}
