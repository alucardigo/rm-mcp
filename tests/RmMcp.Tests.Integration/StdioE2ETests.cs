using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RmMcp.Tests.Integration;

/// <summary>
/// End-to-end tests over stdio JSON-RPC 2.0 — the actual MCP transport.
/// Starts the rm-mcp server process, performs the MCP handshake,
/// then exercises tools against the Docker containers (SQL + SOAP mock).
/// </summary>
[Collection("Docker")]
public class StdioE2ETests : IDisposable
{
    private readonly Process _server;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private int _nextId = 1;

    public StdioE2ETests(DockerFixture docker)
    {
        var serverDll = FindServerDll();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = serverDll,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.Environment["RM_SQL_CONNECTION"] = docker.SqlConnectionString;
        psi.Environment["RM_SOAP_URL"] = docker.SoapBaseUrl;
        psi.Environment["RM_SOAP_USER"] = docker.SoapUser;
        psi.Environment["RM_SOAP_PASS"] = docker.SoapPass;
        psi.Environment["DOTNET_ROOT"] = @"C:\Program Files\dotnet";

        _server = Process.Start(psi)!;
        _stdin = _server.StandardInput;
        _stdin.AutoFlush = true;
        _stdout = _server.StandardOutput;

        Handshake().GetAwaiter().GetResult();
    }

    private static string FindServerDll()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "RmMcp.Server", "bin", "Release", "net10.0", "rm-mcp.dll")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "RmMcp.Server", "bin", "Debug", "net10.0", "rm-mcp.dll")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "RmMcp.Server", "bin", "Release", "net9.0", "rm-mcp.dll")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "RmMcp.Server", "bin", "Debug", "net9.0", "rm-mcp.dll")),
        };
        return candidates.First(File.Exists);
    }

    private async Task Handshake()
    {
        var init = await SendRequest("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "e2e-test", version = "1.0" },
        });

        Assert.Equal("rm-mcp", init.GetProperty("serverInfo").GetProperty("name").GetString());

        await SendNotification("notifications/initialized");
    }

    private async Task<JsonElement> SendRequest(string method, object? @params = null)
    {
        var id = _nextId++;
        var msg = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (@params != null)
            msg["params"] = @params;

        var json = JsonSerializer.Serialize(msg);
        await _stdin.WriteLineAsync(json);

        var line = await ReadLineWithTimeout(TimeSpan.FromSeconds(15));
        Assert.NotNull(line);

        var response = JsonSerializer.Deserialize<JsonElement>(line!);
        Assert.Equal(id, response.GetProperty("id").GetInt32());
        return response.GetProperty("result");
    }

    private async Task SendNotification(string method)
    {
        var msg = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method,
        });
        await _stdin.WriteLineAsync(msg);
        await Task.Delay(200);
    }

    private async Task<string?> ReadLineWithTimeout(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await _stdout.ReadLineAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<JsonElement> CallTool(string name, object args)
    {
        return await SendRequest("tools/call", new
        {
            name,
            arguments = args,
        });
    }

    [Fact]
    public async Task ToolsList_Returns49Tools()
    {
        var result = await SendRequest("tools/list");
        var tools = result.GetProperty("tools");
        Assert.True(tools.GetArrayLength() >= 49,
            $"Expected >= 49 tools, got {tools.GetArrayLength()}");
    }

    [Fact]
    public async Task ToolsList_AllToolsHaveEnvParam()
    {
        var result = await SendRequest("tools/list");
        var tools = result.GetProperty("tools");

        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString()!;
            if (name == "rm_ping") continue; // ping tests connectivity, env is optional there

            var schema = tool.GetProperty("inputSchema");
            var required = schema.GetProperty("required");
            var requiredNames = required.EnumerateArray()
                .Select(r => r.GetString()).ToList();

            Assert.True(requiredNames.Contains("env"),
                $"Tool {name} missing required 'env' param");
        }
    }

    [Fact]
    public async Task RmListCompanies_ReturnsData()
    {
        var result = await CallTool("rm_list_companies", new { env = "teste" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("MIP Engenharia", text);
        Assert.Contains("CODCOLIGADA", text);
    }

    [Fact]
    public async Task RmListBranches_ReturnsData()
    {
        var result = await CallTool("rm_list_branches", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CODFILIAL", text);
    }

    [Fact]
    public async Task RmListEmployees_ReturnsData()
    {
        var result = await CallTool("rm_list_employees", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CHAPA", text);
    }

    [Fact]
    public async Task RmSqlQuery_SelectsFromGcoligada()
    {
        var result = await CallTool("rm_sql_query", new
        {
            env = "teste",
            sql = "SELECT CODCOLIGADA, NOMEFANTASIA FROM GCOLIGADA ORDER BY CODCOLIGADA",
        });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("NOMEFANTASIA", text);
        Assert.Contains("rowCount", text);
    }

    [Fact]
    public async Task RmSqlQuery_BlocksInsert()
    {
        var result = await CallTool("rm_sql_query", new
        {
            env = "teste",
            sql = "INSERT INTO GCOLIGADA (CODCOLIGADA) VALUES (99)",
        });

        // SqlGuard rejects non-SELECT; MCP SDK wraps as isError
        Assert.True(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            "INSERT should be rejected by SqlGuard");
    }

    [Fact]
    public async Task RmSqlTables_ListsCorpore()
    {
        var result = await CallTool("rm_sql_tables", new { env = "teste" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("GCOLIGADA", text);
        Assert.Contains("PFUNC", text);
        Assert.Contains("TMOV", text);
    }

    [Fact]
    public async Task RmSqlColumns_DescribesGcoligada()
    {
        var result = await CallTool("rm_sql_columns", new { env = "teste", table = "GCOLIGADA" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CODCOLIGADA", text);
        Assert.Contains("NOMEFANTASIA", text);
    }

    [Fact]
    public async Task RmSqlFks_ShowsForeignKeys()
    {
        var result = await CallTool("rm_sql_fks", new { env = "teste", table = "PFUNC" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("GCOLIGADA", text);
    }

    [Fact]
    public async Task RmSqlSample_ReturnsRows()
    {
        var result = await CallTool("rm_sql_sample", new { env = "teste", table = "GCOLIGADA", rows = 5 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("MIP Engenharia", text);
    }

    [Fact]
    public async Task RmVersion_ReportsServerInfo()
    {
        var result = await CallTool("rm_version", new { env = "teste" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("VersaoSQL", text);
        Assert.Contains("Corpore", text);
    }

    [Fact]
    public async Task RmListCostCenters_ReturnsData()
    {
        var result = await CallTool("rm_list_cost_centers", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CodCentroCusto", text);
    }

    [Fact]
    public async Task RmEmployeeCount_ReturnsNumber()
    {
        var result = await CallTool("rm_employee_count", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        // Should contain a count or total
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task RmGetEmployee_ReturnsByChapa()
    {
        var result = await CallTool("rm_get_employee", new { env = "teste", coligada = 1, chapa = "001" });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CHAPA", text);
        Assert.Contains("NOME", text);
    }

    [Fact]
    public async Task RmListEntries_ReturnsFinancialData()
    {
        var result = await CallTool("rm_list_entries", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task RmListProducts_ReturnsData()
    {
        var result = await CallTool("rm_list_products", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CODIGOPRD", text);
    }

    [Fact]
    public async Task RmListMovements_ReturnsData()
    {
        var result = await CallTool("rm_list_movements", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task RmChartOfAccounts_ReturnsData()
    {
        var result = await CallTool("rm_chart_of_accounts", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("CODCONTA", text);
    }

    [Fact]
    public async Task RmCustomTables_ListsZmip()
    {
        var result = await CallTool("rm_custom_tables", new { env = "teste", coligada = 1 });
        AssertNoError(result);

        var text = ExtractText(result);
        Assert.Contains("ZMIP", text.ToUpperInvariant());
    }

    [Theory]
    [InlineData("rm_dataserver_read", new[] { "env", "dataServer", "filter" })]
    [InlineData("rm_dataserver_schema", new[] { "env", "dataServer" })]
    [InlineData("rm_services", new[] { "env" })]
    public async Task SoapTools_RespondWithoutCrash(string toolName, string[] requiredParams)
    {
        var args = new Dictionary<string, object> { ["env"] = "teste" };
        if (requiredParams.Contains("dataServer"))
            args["dataServer"] = "GlbColigadaData";
        if (requiredParams.Contains("filter"))
            args["filter"] = "1=1";
        if (requiredParams.Contains("coligada"))
            args["coligada"] = 1;

        var result = await CallTool(toolName, args);
        var text = ExtractText(result);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task DryRunSafety_DataserverSave_PreviewsByDefault()
    {
        var result = await CallTool("rm_dataserver_save", new
        {
            env = "teste",
            dataServer = "GlbColigadaData",
            xml = "<GlbColigadaData><CODCOLIGADA>99</CODCOLIGADA></GlbColigadaData>",
        });

        var text = ExtractText(result);
        // Should indicate dry-run/preview, not actual execution
        Assert.True(
            text.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("dry", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("confirm", StringComparison.OrdinalIgnoreCase),
            $"Expected dry-run indicator in: {text[..Math.Min(200, text.Length)]}");
    }

    private static void AssertNoError(JsonElement result)
    {
        if (result.TryGetProperty("isError", out var isError))
            Assert.False(isError.GetBoolean(),
                $"Tool returned error: {ExtractText(result)[..Math.Min(300, ExtractText(result).Length)]}");
    }

    private static string ExtractText(JsonElement result)
    {
        var sb = new StringBuilder();
        if (result.TryGetProperty("content", out var content))
        {
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                    sb.AppendLine(text.GetString());
            }
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        try
        {
            _stdin.Close();
            if (!_server.WaitForExit(3000))
                _server.Kill();
        }
        catch { }
        _server.Dispose();
    }
}
