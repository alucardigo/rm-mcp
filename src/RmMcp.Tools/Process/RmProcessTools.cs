using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Soap;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Process;

[McpServerToolType]
public static class RmProcessTools
{
    [McpServerTool(Name = "rm_process_execute")]
    [Description("""
        Executa um processo do RM via SOAP ExecuteWithXmlParams.
        SECO: sem confirm=true retorna apenas preview.
        Em producao, exige também production_confirmed=true.
        """)]
    public static async Task<string> Execute(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do ProcessServer (ex: GlbTesteProcess)")] string processServer,
        [Description("Parâmetros XML (CDATA) para o processo")] string xmlParams,
        [Description("true para executar — sem isso retorna apenas preview")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);

        var action = RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);

        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                processServer,
                ambiente = config.Environment.ToString(),
                operacao = "ExecuteWithXmlParams",
                xmlParamsPreview = xmlParams.Length > 500
                    ? xmlParams[..500] + "..." : xmlParams,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", processServer),
            new SoapParam("strXmlParams", xmlParams, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            processServer,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    [McpServerTool(Name = "rm_process_status")]
    [Description("Consulta o status de um job assíncrono do RM via GetJobParams")]
    public static async Task<string> GetJobStatus(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("ID do job retornado por rm_process_execute")] string jobId,
        [Description("ID da execução")] string execId)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetJobParams",
            new SoapParam("jobId", jobId),
            new SoapParam("execId", execId));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("GetJobParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetJobParams");

        return ToolHelper.ToJson(new
        {
            jobId,
            execId,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }
}
