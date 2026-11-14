using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.DataServer;

[McpServerToolType]
public static class RmDataServerWriteTools
{
    [McpServerTool(Name = "rm_dataserver_save")]
    [Description("""
        Salva (insere ou atualiza) um registro via SOAP SaveRecord.
        SECO: sem confirm=true retorna apenas preview do que seria enviado.
        Em producao, exige também production_confirmed=true.
        """)]
    public static async Task<string> SaveRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer (ex: FopFuncionarioData)")] string dataServer,
        [Description("XML do registro no formato do DataServer (obter schema com rm_dataserver_schema)")] string xml,
        [Description("Contexto chave=valor separado por ; (ex: 'CODCOLIGADA=1')")] string? context = null,
        [Description("true para executar — sem isso retorna apenas preview")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO (obrigatório com confirm em producao)")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);

        var action = EvaluateAction(config.Environment, confirm, productionConfirmed);
        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                dataServer,
                ambiente = config.Environment.ToString(),
                operacao = "SaveRecord",
                xmlPreview = xml.Length > 500 ? xml[..500] + "..." : xml,
                contexto = context ?? "(vazio)",
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", dataServer),
            new SoapParam("XML", xml, IsCdata: true),
            new SoapParam("Contexto", context ?? ""));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("SaveRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "SaveRecord");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            dataServer,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    [McpServerTool(Name = "rm_dataserver_delete")]
    [Description("""
        Exclui um registro por chave primária via SOAP DeleteRecordByKey.
        SECO: sem confirm=true retorna apenas preview.
        Em producao, exige também production_confirmed=true.
        """)]
    public static async Task<string> DeleteRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string dataServer,
        [Description("Chave primária (valores separados por ; — ex: '1;00001')")] string primaryKey,
        [Description("Contexto chave=valor separado por ;")] string? context = null,
        [Description("true para executar — sem isso retorna apenas preview")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);

        var action = EvaluateAction(config.Environment, confirm, productionConfirmed);
        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                dataServer,
                primaryKey,
                ambiente = config.Environment.ToString(),
                operacao = "DeleteRecordByKey",
                contexto = context ?? "(vazio)",
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("DeleteRecordByKey",
            new SoapParam("DataServerName", dataServer),
            new SoapParam("PrimaryKey", primaryKey),
            new SoapParam("Contexto", context ?? ""));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("DeleteRecordByKey"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "DeleteRecordByKey");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            dataServer,
            primaryKey,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    internal static string EvaluateAction(RmEnvironment env, bool? confirm,
        bool? productionConfirmed = null) =>
        confirm != true
            ? "preview"
            : env == RmEnvironment.Producao && productionConfirmed != true
                ? "blocked_production"
                : "execute";
}
