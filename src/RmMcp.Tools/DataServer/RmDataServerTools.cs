using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.DataServer;

[McpServerToolType]
public static class RmDataServerTools
{
    [McpServerTool(Name = "rm_dataserver_schema")]
    [Description("Obtém o schema XML (XSD) de um DataServer do RM via SOAP")]
    public static async Task<string> GetSchema(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer (ex: GlbColigadaData, FopFuncionarioData)")] string dataServer)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", dataServer));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetSchema"),
            envelope);

        var schema = SoapResponseParser.ExtractResult(response, "GetSchema");
        return schema;
    }

    [McpServerTool(Name = "rm_dataserver_read")]
    [Description("Lê registros de um DataServer do RM via SOAP ReadView. Retorna dados como JSON.")]
    public static async Task<string> ReadView(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer (ex: FopFuncionarioData)")] string dataServer,
        [Description("Filtro SQL WHERE (ex: 'CODCOLIGADA = 1 AND CHAPA = 00001')")] string? filter = null,
        [Description("Contexto chave=valor separado por ; (ex: 'CODCOLIGADA=1;CODSISTEMA=P')")] string? context = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var parameters = new List<SoapParam>
        {
            new("DataServerName", dataServer),
            new("Filtro", filter ?? ""),
            new("Contexto", context ?? ""),
        };

        var envelope = SoapEnvelopeBuilder.Build("ReadView", parameters.ToArray());

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadView"),
            envelope);

        var dataXml = SoapResponseParser.ExtractResult(response, "ReadView");
        var records = SoapResponseParser.ParseDataXml(dataXml);

        return ToolHelper.ToJson(new
        {
            dataServer,
            recordCount = records.Count,
            records,
        });
    }

    [McpServerTool(Name = "rm_dataserver_read_paged")]
    [Description("Lê registros de um DataServer com paginação via SOAP ReadViewWithLimit")]
    public static async Task<string> ReadViewPaged(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string dataServer,
        [Description("Filtro SQL WHERE")] string? filter = null,
        [Description("Contexto chave=valor separado por ;")] string? context = null,
        [Description("Registro inicial (0-based, padrão 0)")] int? startRecord = null,
        [Description("Máximo de registros por página (padrão 100)")] int? maxRecords = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var start = startRecord ?? 0;
        var max = Math.Clamp(maxRecords ?? 100, 1, 1000);

        var envelope = SoapEnvelopeBuilder.Build("ReadViewWithLimit",
            new SoapParam("DataServerName", dataServer),
            new SoapParam("Filtro", filter ?? ""),
            new SoapParam("Contexto", context ?? ""),
            new SoapParam("StartRecord", start.ToString()),
            new SoapParam("MaxRecords", max.ToString()));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadViewWithLimit"),
            envelope);

        var dataXml = SoapResponseParser.ExtractResult(response, "ReadViewWithLimit");
        var records = SoapResponseParser.ParseDataXml(dataXml);

        return ToolHelper.ToJson(new
        {
            dataServer,
            startRecord = start,
            maxRecords = max,
            recordCount = records.Count,
            hasMore = records.Count == max,
            records,
        });
    }

    [McpServerTool(Name = "rm_dataserver_count")]
    [Description("Conta total de registros em um DataServer via SOAP GetTotalRecords")]
    public static async Task<string> GetTotalRecords(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string dataServer,
        [Description("Filtro SQL WHERE")] string? filter = null,
        [Description("Contexto chave=valor separado por ;")] string? context = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetTotalRecords",
            new SoapParam("DataServerName", dataServer),
            new SoapParam("Filtro", filter ?? ""),
            new SoapParam("Contexto", context ?? ""));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetTotalRecords"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetTotalRecords");

        return ToolHelper.ToJson(new
        {
            dataServer,
            totalRegistros = int.TryParse(result, out var total) ? total : -1,
            filtro = filter ?? "(sem filtro)",
        });
    }

    [McpServerTool(Name = "rm_dataserver_record")]
    [Description("Lê um registro específico por chave primária via SOAP ReadRecord")]
    public static async Task<string> ReadRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string dataServer,
        [Description("Chave primária (valores separados por ; — ex: '1;00001')")] string primaryKey,
        [Description("Contexto chave=valor separado por ;")] string? context = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var parameters = new List<SoapParam>
        {
            new("DataServerName", dataServer),
            new("PrimaryKey", primaryKey),
            new("Contexto", context ?? ""),
        };

        var envelope = SoapEnvelopeBuilder.Build("ReadRecord", parameters.ToArray());

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadRecord"),
            envelope);

        var dataXml = SoapResponseParser.ExtractResult(response, "ReadRecord");
        var records = SoapResponseParser.ParseDataXml(dataXml);

        return ToolHelper.ToJson(new
        {
            dataServer,
            primaryKey,
            record = records.FirstOrDefault(),
        });
    }
}
