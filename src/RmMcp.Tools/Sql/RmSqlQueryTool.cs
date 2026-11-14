using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Sql;

[McpServerToolType]
public static class RmSqlQueryTool
{
    [McpServerTool(Name = "rm_sql_query")]
    [Description("Executa consulta SQL SELECT no banco Corpore do RM. Apenas SELECT e WITH são permitidos.")]
    public static async Task<string> Query(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Consulta SQL (apenas SELECT/WITH)")] string sql,
        [Description("Timeout em segundos (padrão 30)")] int? timeout = null,
        [Description("Máximo de linhas retornadas (padrão 1000)")] int? maxRows = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        var result = await executor.ExecuteAsync(
            sql,
            timeoutSeconds: timeout ?? 30,
            maxRows: maxRows ?? 1000);

        return ToolHelper.FormatResult(result);
    }
}
