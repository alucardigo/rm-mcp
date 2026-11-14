using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Soap;
using RmMcp.Sql;

namespace RmMcp.Tools.Meta;

[McpServerToolType]
public static class RmPingTool
{
    [McpServerTool(Name = "rm_ping")]
    [Description("Testa conectividade com SQL Server e RM.Host.Service SOAP")]
    public static async Task<string> Ping(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var environment = RmEnvironmentParser.Parse(env);
        var config = RmConfig.Load(environment);
        var results = new Dictionary<string, object>
        {
            ["ambiente"] = environment.ToString(),
        };

        results["sql"] = await TestSql(config);
        results["soap"] = await TestSoap(config);

        return JsonSerializer.Serialize(results,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static async Task<object> TestSql(RmConfig config)
    {
        try
        {
            using var executor = new SqlQueryExecutor(config.SqlConnectionString);
            var (server, version) = await executor.PingAsync();
            return new { status = "ok", server, version };
        }
        catch (Exception ex)
        {
            return new { status = "erro", mensagem = ex.Message };
        }
    }

    private static async Task<object> TestSoap(RmConfig config)
    {
        try
        {
            using var client = new RmSoapClient(
                config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);
            var response = await client.PingAsync();
            return new { status = "ok", bytesResposta = response.Length };
        }
        catch (Exception ex)
        {
            return new { status = "erro", mensagem = ex.Message };
        }
    }
}
