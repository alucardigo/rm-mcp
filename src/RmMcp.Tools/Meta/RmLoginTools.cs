using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Soap;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Meta;

[McpServerToolType]
public static class RmLoginTools
{
    [McpServerTool(Name = "rm_aliases")]
    [Description("Lista aliases (conexões de banco) configurados no RM.Host.Service")]
    public static async Task<string> GetAliases(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetAlias");

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.LoginEndpoint,
            SoapEnvelopeBuilder.LoginAction("GetAlias"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetAlias");

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            aliases = result,
        });
    }

    [McpServerTool(Name = "rm_system_version")]
    [Description("Obtém a versão do sistema RM via SOAP")]
    public static async Task<string> GetSystemVersion(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetSystemVersion");

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.LoginEndpoint,
            SoapEnvelopeBuilder.LoginAction("GetSystemVersion"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetSystemVersion");

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            versao = result,
        });
    }

    [McpServerTool(Name = "rm_user_security")]
    [Description("Obtém permissões de segurança de um usuário em uma coligada")]
    public static async Task<string> GetSecurityInfo(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Código do usuário")] string codUsuario,
        [Description("ID da permissão de segurança")] int securityId)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateSoap(config);

        var envelope = SoapEnvelopeBuilder.Build("GetSecurityInfo",
            new SoapParam("codColigada", coligada.ToString()),
            new SoapParam("codUsuario", codUsuario),
            new SoapParam("SecurityID", securityId.ToString()));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.LoginEndpoint,
            SoapEnvelopeBuilder.LoginAction("GetSecurityInfo"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetSecurityInfo");

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            coligada,
            usuario = codUsuario,
            securityId,
            permissao = result,
        });
    }
}
