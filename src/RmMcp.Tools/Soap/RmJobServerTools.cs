using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Soap;

[McpServerToolType]
public static class RmJobServerTools
{
    private const string JobEndpoint = "wsJob/IwsJob";

    private static string JobAction(string op) =>
        $"http://www.totvs.com/IwsJob/{op}";

    [McpServerTool(Name = "rm_list_jobs")]
    [Description("Lista jobs agendados no RM")]
    public static async Task<string> ListJobs(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                j.ID, j.NOME, j.CODSISTEMA,
                j.RELATORIO, j.IGNORALOG
            FROM GJOB j WITH (NOLOCK)
            ORDER BY j.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql);
        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_job_history")]
    [Description("Histórico de execução de um job")]
    public static async Task<string> JobHistory(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("ID do job")] int idJob,
        [Description("Máximo de registros (padrão 50)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 50, 1, 500);

        var sql = $"""
            SELECT TOP {n}
                h.IDJOB, h.IDEXECUCAO, h.DATAPROGRAMADA,
                h.SERVIDOR, h.DATAINIEXEC, h.DATAFIMEXEC,
                h.PERCPROGRESSO, h.STATUS, h.MENSAGEMSTATUS
            FROM GJOBXEXECUCAO h WITH (NOLOCK)
            WHERE h.IDJOB = @idJob
            ORDER BY h.DATAINIEXEC DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@idJob"] = idJob,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_execute_job")]
    [Description("""
        Executa um job via SOAP. Sem confirm=true retorna preview.
        Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> ExecuteJob(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("ID do job")] int idJob,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = RmDataServerWriteTools.EvaluateAction(config.Environment, confirm, productionConfirmed);
        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                operacao = "ExecuteJob",
                idJob,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("Execute",
            new SoapParam("JobId", idJob.ToString()));

        var response = await client.SendAsync(JobEndpoint, JobAction("Execute"), envelope);
        var result = SoapResponseParser.ExtractResult(response, "Execute");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "ExecuteJob",
            idJob,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }
}
