using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Meta;

[McpServerToolType]
public static class RmDiscoveryTools
{
    [McpServerTool(Name = "rm_version")]
    [Description("Versão do RM: SQL Server, banco Corpore e RM.Host.Service")]
    public static async Task<string> Version(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                @@SERVERNAME AS Servidor,
                SERVERPROPERTY('ProductVersion') AS VersaoSQL,
                SERVERPROPERTY('Edition') AS Edicao,
                DB_NAME() AS Banco,
                (SELECT TOP 1 VERSAO FROM GPARAMS WITH (NOLOCK)) AS VersaoRM
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>());

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_dataserver_list")]
    [Description("Lista DataServers disponíveis no RM.Host.Service via REST broker")]
    public static async Task<string> DataServerList(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        try
        {
            var response = await client.GetAsync("RMSRestDataServer");
            return ToolHelper.ToJson(new
            {
                ambiente = config.Environment.ToString(),
                resultado = response,
            });
        }
        catch (Exception ex)
        {
            return ToolHelper.ToJson(new
            {
                ambiente = config.Environment.ToString(),
                status = "erro",
                mensagem = ex.Message,
                alternativa = "Use rm_sql_query com: SELECT DISTINCT NOME FROM GDATASERVERNAME ORDER BY NOME",
            });
        }
    }

    [McpServerTool(Name = "rm_services")]
    [Description("Lista serviços SOAP disponíveis no RM.Host.Service")]
    public static async Task<string> Services(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var services = new List<object>();
        string[] knownEndpoints =
        [
            "wsDataServer/MEX?wsdl",
            "wsProcess/MEX?wsdl",
        ];

        foreach (var ep in knownEndpoints)
        {
            try
            {
                var response = await client.GetAsync(ep);
                services.Add(new
                {
                    endpoint = ep,
                    status = "disponivel",
                    tamanho = response.Length,
                });
            }
            catch (Exception ex)
            {
                services.Add(new
                {
                    endpoint = ep,
                    status = "erro",
                    mensagem = ex.Message,
                });
            }
        }

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            servicos = services,
        });
    }

    [McpServerTool(Name = "rm_list_processes")]
    [Description("Lista processos/fórmulas visuais disponíveis no RM")]
    public static async Task<string> ListProcesses(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por nome (LIKE pattern)")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT p.CODCOLIGADA, p.CODPROCESSO, p.DESCRICAO,
                   p.CODAPLICACAO, p.ATIVO
            FROM GPROCESSO p WITH (NOLOCK)
            WHERE p.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR p.DESCRICAO LIKE @filter)
            ORDER BY p.DESCRICAO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }
}
