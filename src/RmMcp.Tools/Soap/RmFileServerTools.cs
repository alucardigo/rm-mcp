using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Soap;

[McpServerToolType]
public static class RmFileServerTools
{
    [McpServerTool(Name = "rm_list_attachments")]
    [Description("Lista anexos do módulo RH (AANEXO) no RM. Para outros módulos use rm_sql_query com a tabela de anexo específica.")]
    public static async Task<string> ListAttachments(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int codColigada,
        [Description("Máximo de registros (padrão 50)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 50, 1, 500);

        var sql = $"""
            SELECT TOP {n}
                a.CODCOLIGADA, a.ID, a.DESCRICAO,
                a.NOMEARQUIVO, a.ANEXO,
                a.RECCREATEDON, a.RECCREATEDBY
            FROM AANEXO a WITH (NOLOCK)
            WHERE a.CODCOLIGADA = @codColigada
            ORDER BY a.RECCREATEDON DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codColigada"] = codColigada,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_reports")]
    [Description("Lista relatórios disponíveis no RM")]
    public static async Task<string> ListReports(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código do sistema (ex: 'P' = RH, 'S' = Estoque, 'F' = Financeiro)")] string? codSistema = null,
        [Description("Filtro por descrição (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                r.CODCOLIGADA, r.ID, r.CODIGO, r.DESCRICAO,
                r.CODAPLICACAO, r.GRUPO, r.TIPO,
                r.DATACRIACAO, r.DISPONIVELMENU
            FROM RRPTREPORT r WITH (NOLOCK)
            WHERE (@codSistema IS NULL OR r.CODAPLICACAO = @codSistema)
              AND (@filter IS NULL OR r.DESCRICAO LIKE @filter)
            ORDER BY r.CODAPLICACAO, r.DESCRICAO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codSistema"] = codSistema,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_generate_report")]
    [Description("""
        Gera um relatório do RM via SOAP (processo de geração de relatório).
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> GenerateReport(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("ID do relatório")] int idRelatorio,
        [Description("Código da coligada")] int codColigada,
        [Description("Parâmetros XML do relatório (formato <ReportParams>...</ReportParams>)")] string? paramsXml = null,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = DataServer.RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);
        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                operacao = "GenerateReport",
                idRelatorio,
                codColigada,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        var xml = $"""
            <ProcessParams>
                <CodColigada>{codColigada}</CodColigada>
                <IdRelatorio>{idRelatorio}</IdRelatorio>
                {paramsXml ?? ""}
            </ProcessParams>
            """;

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "GlbGerarRelatorio"),
            new SoapParam("strXmlParams", xml, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "GenerateReport",
            idRelatorio,
            codColigada,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }
}
