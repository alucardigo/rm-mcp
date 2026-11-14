using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Soap;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmDomainWriteTools
{
    [McpServerTool(Name = "rm_create_movement")]
    [Description("""
        Cria um movimento (OC, requisição, NF) via SOAP SaveRecord no EstMovimentoData.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> CreateMovement(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Tipo de movimento (TMV, ex: '1.1.01')")] string codTmv,
        [Description("XML do movimento no formato do DataServer EstMovimentoData")] string xml,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        return await SaveWithDataServer(env, "EstMovimentoData", coligada, xml, confirm, productionConfirmed,
            $"Criação de movimento TMV={codTmv}");
    }

    [McpServerTool(Name = "rm_approve_movement")]
    [Description("""
        Aprova um movimento pendente via fórmula visual.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> ApproveMovement(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("ID do movimento (IDMOV)")] int idMov,
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
                operacao = "ApproveMovement",
                coligada,
                idMov,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        var paramsXml = $"""
            <ProcessParams>
                <CodColigada>{coligada}</CodColigada>
                <IdMov>{idMov}</IdMov>
            </ProcessParams>
            """;

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "EstAprovarMov"),
            new SoapParam("strXmlParams", paramsXml, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "ApproveMovement",
            coligada,
            idMov,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    [McpServerTool(Name = "rm_create_financial_entry")]
    [Description("""
        Cria lançamento financeiro via SOAP SaveRecord no FinLanData.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> CreateFinancialEntry(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("XML do lançamento no formato do DataServer FinLanData")] string xml,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        return await SaveWithDataServer(env, "FinLanData", coligada, xml, confirm, productionConfirmed,
            "Criação de lançamento financeiro");
    }

    [McpServerTool(Name = "rm_update_employee")]
    [Description("""
        Atualiza dados de funcionário via SOAP SaveRecord no RhuFuncDados.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> UpdateEmployee(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("XML do funcionário no formato do DataServer RhuFuncDados")] string xml,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        return await SaveWithDataServer(env, "RhuFuncDados", coligada, xml, confirm, productionConfirmed,
            "Atualização de funcionário");
    }

    [McpServerTool(Name = "rm_terminate_employee")]
    [Description("""
        Processa desligamento de funcionário via processo SOAP.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> TerminateEmployee(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa,
        [Description("Data de demissão (yyyy-MM-dd)")] string dataDemissao,
        [Description("Código do tipo de demissão")] string codTipoDemissao,
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
                operacao = "TerminateEmployee",
                coligada,
                chapa,
                dataDemissao,
                codTipoDemissao,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        var paramsXml = $"""
            <ProcessParams>
                <CodColigada>{coligada}</CodColigada>
                <Chapa>{chapa}</Chapa>
                <DataDemissao>{dataDemissao}</DataDemissao>
                <CodTipoDemissao>{codTipoDemissao}</CodTipoDemissao>
            </ProcessParams>
            """;

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "RhuDesligamento"),
            new SoapParam("strXmlParams", paramsXml, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "TerminateEmployee",
            coligada,
            chapa,
            dataDemissao,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    [McpServerTool(Name = "rm_run_formula")]
    [Description("""
        Executa uma fórmula visual genérica do RM via SOAP.
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> RunFormula(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do processo/fórmula no RM")] string processName,
        [Description("XML dos parâmetros (formato ProcessParams)")] string paramsXml,
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
                operacao = "RunFormula",
                processName,
                ambiente = config.Environment.ToString(),
                xmlPreview = paramsXml.Length > 500 ? paramsXml[..500] + "..." : paramsXml,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", processName),
            new SoapParam("strXmlParams", paramsXml, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "RunFormula",
            processName,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    private static async Task<string> SaveWithDataServer(
        string env, string dataServerName, int coligada, string xml,
        bool? confirm, bool? productionConfirmed, string operacaoDesc)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = RmDataServerWriteTools.EvaluateAction(config.Environment, confirm, productionConfirmed);
        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                operacao = operacaoDesc,
                dataServer = dataServerName,
                coligada,
                ambiente = config.Environment.ToString(),
                xmlPreview = xml.Length > 500 ? xml[..500] + "..." : xml,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        var context = $"<RMSContexto><CodColigada>{coligada}</CodColigada></RMSContexto>";
        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", dataServerName),
            new SoapParam("XML", xml, IsCdata: true),
            new SoapParam("Contexto", context));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("SaveRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "SaveRecord");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = operacaoDesc,
            dataServer = dataServerName,
            coligada,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }
}
