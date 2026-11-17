using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Sql;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Sql;

[McpServerToolType]
public static class RmSqlWriteTool
{
    [McpServerTool(Name = "rm_sql_execute")]
    [Description("""
        Executa INSERT, UPDATE, DELETE ou MERGE no banco Corpore do RM.
        SECO: sem confirm=true retorna apenas preview.
        Em producao, exige também production_confirmed=true.
        UPDATE e DELETE sem WHERE são bloqueados.
        """)]
    public static async Task<string> Execute(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("SQL de escrita (INSERT, UPDATE, DELETE ou MERGE)")] string sql,
        [Description("true para executar — sem isso retorna apenas preview")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null,
        [Description("Timeout em segundos (padrão 30)")] int? timeout = null)
    {
        var (allowed, reason) = SqlWriteGuard.Validate(sql);
        if (!allowed)
        {
            return ToolHelper.ToJson(new
            {
                status = "bloqueado",
                motivo = reason,
            });
        }

        var config = ToolHelper.LoadConfig(env);
        var action = RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);

        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                ambiente = config.Environment.ToString(),
                sqlPreview = sql.Length > 500 ? sql[..500] + "..." : sql,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var executor = ToolHelper.CreateSql(config);
        var rowsAffected = await executor.ExecuteNonQueryAsync(sql,
            timeoutSeconds: timeout ?? 30);

        return ToolHelper.ToJson(new
        {
            status = "executado",
            ambiente = config.Environment.ToString(),
            linhasAfetadas = rowsAffected,
        });
    }
}
