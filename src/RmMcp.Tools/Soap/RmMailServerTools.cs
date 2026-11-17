using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.DataServer;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Soap;

[McpServerToolType]
public static class RmMailServerTools
{
    [McpServerTool(Name = "rm_send_mail")]
    [Description("""
        Envia e-mail via servidor de e-mail do RM (SOAP).
        Sem confirm=true retorna preview. Em produção exige production_confirmed=true.
        """)]
    public static async Task<string> SendMail(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Destinatário(s) separados por ;")] string to,
        [Description("Assunto")] string subject,
        [Description("Corpo do e-mail (HTML permitido)")] string body,
        [Description("CC (opcional)")] string? cc = null,
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
                operacao = "SendMail",
                to,
                subject,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        var paramsXml = $"""
            <MailParams>
                <To>{System.Security.SecurityElement.Escape(to)}</To>
                <Subject>{System.Security.SecurityElement.Escape(subject)}</Subject>
                <Body><![CDATA[{body}]]></Body>
                {(cc != null ? $"<CC>{System.Security.SecurityElement.Escape(cc)}</CC>" : "")}
            </MailParams>
            """;

        using var client = ToolHelper.CreateSoap(config);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "GlbEnviarEmail"),
            new SoapParam("strXmlParams", paramsXml, IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");

        return ToolHelper.ToJson(new
        {
            status = "executado",
            operacao = "SendMail",
            to,
            subject,
            ambiente = config.Environment.ToString(),
            resultado = result,
        });
    }

    [McpServerTool(Name = "rm_mail_queue")]
    [Description("Lista fila de e-mails pendentes/enviados no RM")]
    public static async Task<string> MailQueue(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Status: 'pendente' (sem FINISHTIME), 'enviado' (com FINISHTIME) — omitir = todos")] string? status = null,
        [Description("Máximo de registros (padrão 50)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 50, 1, 500);

        var statusFilter = status?.ToLowerInvariant() switch
        {
            "pendente" => "AND m.FINISHTIME IS NULL",
            "enviado" => "AND m.FINISHTIME IS NOT NULL",
            _ => "",
        };

        var sql = $"""
            SELECT TOP {n}
                m.CODSISTEMA, m.IDEVENTO, m.SENDER, m.SENDERADDR,
                m.TOADDR, m.CCADDR, m.SUBJECT,
                m.REQUESTTIME, m.STARTTIME, m.FINISHTIME
            FROM GMAILFILA m WITH (NOLOCK)
            WHERE 1=1 {statusFilter}
            ORDER BY m.REQUESTTIME DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql);
        return ToolHelper.FormatResult(result);
    }
}
