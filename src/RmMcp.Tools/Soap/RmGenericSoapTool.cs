using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Soap;

[McpServerToolType]
public static class RmGenericSoapTool
{
    [McpServerTool(Name = "rm_soap_call")]
    [Description("""
        Chamada SOAP genérica para qualquer endpoint do RM.
        Use quando os tools específicos não cobrem a operação desejada.
        Sem confirm=true retorna preview para operações de escrita.
        """)]
    public static async Task<string> SoapCall(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Endpoint relativo (ex: 'wsDataServer/IwsDataServer', 'wsProcess/IwsProcess', 'wsLogin/IwsLogin')")] string endpoint,
        [Description("SOAPAction completa (ex: 'http://www.totvs.com/IwsDataServer/ReadView')")] string soapAction,
        [Description("Nome da operação SOAP (ex: 'ReadView', 'Execute')")] string operation,
        [Description("Parâmetros como JSON {nome: valor} — serão convertidos em elementos SOAP")] string? parametersJson = null,
        [Description("true se a operação modifica dados (aplica SECO)")] bool? isWrite = null,
        [Description("true para executar (obrigatório se isWrite=true)")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);

        if (isWrite == true)
        {
            var action = RmDataServerWriteTools.EvaluateAction(config.Environment, confirm, productionConfirmed);
            if (action != "execute")
            {
                return ToolHelper.ToJson(new
                {
                    mode = action,
                    operacao = $"SOAP {operation}",
                    endpoint,
                    soapAction,
                    ambiente = config.Environment.ToString(),
                    instrucao = action == "preview"
                        ? "Adicione confirm=true para executar"
                        : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
                });
            }
        }

        var soapParams = Array.Empty<SoapParam>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson);
            if (dict != null)
            {
                soapParams = dict.Select(kv => new SoapParam(kv.Key, kv.Value)).ToArray();
            }
        }

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build(operation, soapParams);
        var response = await client.SendAsync(endpoint, soapAction, envelope);

        var result = SoapResponseParser.ExtractResult(response, operation);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            endpoint,
            operation,
            resultado = result.Length > 5000 ? result[..5000] + "... (truncado)" : result,
        });
    }

    [McpServerTool(Name = "rm_soap_raw")]
    [Description("""
        Envia envelope SOAP bruto para qualquer endpoint do RM.
        Use quando precisa controlar totalmente o XML do envelope.
        Sem confirm=true retorna preview se isWrite=true.
        """)]
    public static async Task<string> SoapRaw(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Endpoint relativo")] string endpoint,
        [Description("SOAPAction completa")] string soapAction,
        [Description("Envelope SOAP XML completo")] string envelope,
        [Description("true se a operação modifica dados")] bool? isWrite = null,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);

        if (isWrite == true)
        {
            var action = RmDataServerWriteTools.EvaluateAction(config.Environment, confirm, productionConfirmed);
            if (action != "execute")
            {
                return ToolHelper.ToJson(new
                {
                    mode = action,
                    operacao = "SOAP Raw",
                    endpoint,
                    soapAction,
                    ambiente = config.Environment.ToString(),
                    envelopePreview = envelope.Length > 500 ? envelope[..500] + "..." : envelope,
                    instrucao = action == "preview"
                        ? "Adicione confirm=true para executar"
                        : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
                });
            }
        }

        using var client = ToolHelper.CreateSoap(config);
        var response = await client.SendAsync(endpoint, soapAction, envelope);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            endpoint,
            responseLength = response.Length,
            response = response.Length > 5000 ? response[..5000] + "... (truncado)" : response,
        });
    }

    [McpServerTool(Name = "rm_list_formulas")]
    [Description("Lista fórmulas visuais (processos) disponíveis no RM")]
    public static async Task<string> ListFormulas(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código do sistema (ex: 'P', 'S', 'F' — omitir = todos)")] string? codSistema = null,
        [Description("Filtro por nome (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                f.CODCOLIGADA, f.CODIGO, f.TITULO,
                f.APLICACAO, f.CODTIPO, f.GRUPO
            FROM GFORMULA f WITH (NOLOCK)
            WHERE (@codSistema IS NULL OR f.APLICACAO = @codSistema)
              AND (@filter IS NULL OR f.TITULO LIKE @filter)
            ORDER BY f.APLICACAO, f.TITULO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codSistema"] = codSistema,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_dataservers")]
    [Description("Lista todos os DataServers registrados no RM com suas tabelas")]
    public static async Task<string> ListAllDataServers(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Filtro por nome do DataServer (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 200)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 200, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                ds.CODSERVICO, ds.NOME, ds.DESCRICAO,
                ds.CODACESSO, ds.KEYACESSO
            FROM GSERVICO ds WITH (NOLOCK)
            WHERE (@filter IS NULL OR ds.NOME LIKE @filter)
            ORDER BY ds.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }
}
