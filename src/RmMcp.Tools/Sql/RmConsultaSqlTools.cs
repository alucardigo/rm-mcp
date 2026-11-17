using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Sql;

[McpServerToolType]
public static class RmConsultaSqlTools
{
    [McpServerTool(Name = "rm_consulta_sql")]
    [Description("""
        Executa uma Consulta SQL (sentença) registrada no RM via SOAP wsConsultaSQL.
        As sentenças são queries pré-cadastradas no RM, identificadas por código.
        """)]
    public static async Task<string> RealizarConsulta(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int codColigada,
        [Description("Código do sistema (ex: 'P' = RH, 'S' = Estoque, 'F' = Financeiro, 'T' = Global)")] string codSistema,
        [Description("Código da sentença SQL cadastrada no RM")] string codSentenca,
        [Description("Parâmetros XML no formato <Params><Param Name='nome'>valor</Param></Params> (omitir se sem parâmetros)")] string? parametrosXml = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("RealizarConsultaSQL",
            new SoapParam("codColigada", codColigada.ToString()),
            new SoapParam("codSistema", codSistema),
            new SoapParam("codSentenca", codSentenca),
            new SoapParam("parameters", parametrosXml ?? ""));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ConsultaSqlEndpoint,
            SoapEnvelopeBuilder.ConsultaSqlAction("RealizarConsultaSQL"),
            envelope);

        var dataXml = SoapResponseParser.ExtractResult(response, "RealizarConsultaSQL");
        var records = SoapResponseParser.ParseDataXml(dataXml);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            codColigada,
            codSistema,
            codSentenca,
            recordCount = records.Count,
            records,
        });
    }

    [McpServerTool(Name = "rm_list_consultas_sql")]
    [Description("Lista as Consultas SQL (sentenças) cadastradas no RM")]
    public static async Task<string> ListConsultas(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int codColigada,
        [Description("Código do sistema (ex: 'P', 'S', 'F', 'T' — omitir = todos)")] string? codSistema = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                cs.CODCOLIGADA, cs.CODSENTENCA, cs.APLICACAO,
                cs.TITULO, cs.SENTENCA, cs.DISPONIVEL
            FROM GCONSSQL cs WITH (NOLOCK)
            WHERE cs.CODCOLIGADA IN (0, @codColigada)
              AND (@codSistema IS NULL OR cs.APLICACAO = @codSistema)
            ORDER BY cs.APLICACAO, cs.CODSENTENCA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codColigada"] = codColigada,
                ["@codSistema"] = codSistema,
            });

        return ToolHelper.FormatResult(result);
    }
}
