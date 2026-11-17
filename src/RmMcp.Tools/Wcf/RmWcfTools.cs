using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Wcf;

[McpServerToolType]
public static class RmWcfTools
{
    [McpServerTool(Name = "rm_wcf_login_bypass")]
    [Description("Autentica no RM via ExecuteByPass (net.tcp). Não precisa de senha — bypassa o version check. Retorna CorporePrincipal para chamadas subsequentes.")]
    public static async Task<string> LoginByPass(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Alias do banco (default: CorporeRM)")] string? alias = null,
        [Description("Usuário (default: mestre)")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);

        var (security, _) = await wcf.IsSecurityEnabledAsync();
        var (compression, _) = await wcf.IsCompressionEnabledAsync();

        var (success, errorCode, rawXml) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias,
            userName ?? config.WcfUser);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            endpoint = $"net.tcp://{config.WcfHost}:{config.WcfPort}",
            alias = alias ?? config.WcfAlias,
            userName = userName ?? config.WcfUser,
            securityEnabled = security,
            compressionEnabled = compression,
            success,
            errorCode,
            authenticated = wcf.IsAuthenticated,
            principalLength = wcf.PrincipalXml?.Length ?? 0,
        });
    }

    [McpServerTool(Name = "rm_wcf_broker_info")]
    [Description("Consulta o Broker do RM (net.tcp) para verificar segurança, compressão e servidores registrados.")]
    public static async Task<string> GetBrokerInfo(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);

        var (security, secXml) = await wcf.IsSecurityEnabledAsync();
        var (compression, compXml) = await wcf.IsCompressionEnabledAsync();
        var aliasXml = await wcf.GetAliasAsync();
        var versionXml = await wcf.GetSystemVersionAsync();

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            endpoint = $"net.tcp://{config.WcfHost}:{config.WcfPort}",
            securityEnabled = security,
            compressionEnabled = compression,
            aliasResponse = aliasXml,
            versionResponse = versionXml,
        });
    }

    [McpServerTool(Name = "rm_wcf_readview")]
    [Description("Lê dados de um DataServer do RM via net.tcp (requer autenticação prévia via rm_wcf_login_bypass). Use para consultar dados financeiros, RH, etc.")]
    public static async Task<string> ReadView(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer (ex: FinCxaData, FopFuncData, GlbColigadaData)")] string serverName,
        [Description("Código da coligada")] int codColigada = 1,
        [Description("Código do sistema (ex: F=Financeiro, P=Folha, G=Global)")] string codSistema = "G",
        [Description("Filtro SQL opcional (WHERE clause)")] string? filter = null,
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);

        await wcf.IsSecurityEnabledAsync();
        var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias, userName ?? config.WcfUser);

        if (!success)
        {
            return ToolHelper.ToJson(new
            {
                error = "Falha na autenticação ExecuteByPass",
                errorCode,
            });
        }

        var result = await wcf.ReadViewAsync(serverName, codColigada, codSistema, filter);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            serverName,
            codColigada,
            codSistema,
            filter,
            result,
        });
    }

    [McpServerTool(Name = "rm_wcf_readrecord")]
    [Description("Lê um registro específico de um DataServer do RM via net.tcp. Requer autenticação prévia.")]
    public static async Task<string> ReadRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string serverName,
        [Description("Chave primária do registro")] string primaryKey,
        [Description("Código da coligada")] int codColigada = 1,
        [Description("Código do sistema")] string codSistema = "G",
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);

        await wcf.IsSecurityEnabledAsync();
        var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias, userName ?? config.WcfUser);

        if (!success)
        {
            return ToolHelper.ToJson(new
            {
                error = "Falha na autenticação ExecuteByPass",
                errorCode,
            });
        }

        var result = await wcf.ReadRecordAsync(serverName, codColigada, codSistema, primaryKey);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            serverName,
            primaryKey,
            codColigada,
            codSistema,
            result,
        });
    }

    [McpServerTool(Name = "rm_wcf_readschema")]
    [Description("Lê o schema (estrutura XML) de um DataServer do RM via net.tcp. Útil para descobrir campos disponíveis.")]
    public static async Task<string> ReadSchema(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string serverName,
        [Description("Código da coligada")] int codColigada = 1,
        [Description("Código do sistema")] string codSistema = "G",
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);

        await wcf.IsSecurityEnabledAsync();
        var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias, userName ?? config.WcfUser);

        if (!success)
        {
            return ToolHelper.ToJson(new
            {
                error = "Falha na autenticação ExecuteByPass",
                errorCode,
            });
        }

        var result = await wcf.ReadSchemaAsync(serverName, codColigada, codSistema);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            serverName,
            codColigada,
            codSistema,
            result,
        });
    }

    [McpServerTool(Name = "rm_wcf_saverecord")]
    [Description("Grava um registro em um DataServer do RM via net.tcp. OPERAÇÃO DE ESCRITA — requer confirm=true.")]
    public static async Task<string> SaveRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string serverName,
        [Description("XML do DataSet a gravar")] string dataSetXml,
        [Description("Código da coligada")] int codColigada = 1,
        [Description("Código do sistema")] string codSistema = "G",
        [Description("Confirma a operação de escrita")] bool confirm = false,
        [Description("Confirma em produção")] bool production_confirmed = false,
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);

        if (!confirm)
            return "ERRO: operação de escrita requer confirm=true";
        if (config.Environment == RmMcp.Core.Config.RmEnvironment.Producao && !production_confirmed)
            return "ERRO: escrita em produção requer production_confirmed=true";

        using var wcf = ToolHelper.CreateWcf(config);
        await wcf.IsSecurityEnabledAsync();
        var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias, userName ?? config.WcfUser);

        if (!success)
        {
            return ToolHelper.ToJson(new
            {
                error = "Falha na autenticação ExecuteByPass",
                errorCode,
            });
        }

        var result = await wcf.SaveRecordAsync(serverName, codColigada, codSistema, dataSetXml);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            serverName,
            codColigada,
            codSistema,
            result,
        });
    }

    [McpServerTool(Name = "rm_wcf_deleterecord")]
    [Description("Exclui um registro de um DataServer do RM via net.tcp. OPERAÇÃO DESTRUTIVA — requer confirm=true.")]
    public static async Task<string> DeleteRecord(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome do DataServer")] string serverName,
        [Description("Chave primária do registro")] string primaryKey,
        [Description("Código da coligada")] int codColigada = 1,
        [Description("Código do sistema")] string codSistema = "G",
        [Description("Confirma a operação de exclusão")] bool confirm = false,
        [Description("Confirma em produção")] bool production_confirmed = false,
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);

        if (!confirm)
            return "ERRO: operação de exclusão requer confirm=true";
        if (config.Environment == RmMcp.Core.Config.RmEnvironment.Producao && !production_confirmed)
            return "ERRO: exclusão em produção requer production_confirmed=true";

        using var wcf = ToolHelper.CreateWcf(config);
        await wcf.IsSecurityEnabledAsync();
        var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
            alias ?? config.WcfAlias, userName ?? config.WcfUser);

        if (!success)
        {
            return ToolHelper.ToJson(new
            {
                error = "Falha na autenticação ExecuteByPass",
                errorCode,
            });
        }

        var result = await wcf.DeleteRecordAsync(serverName, codColigada, codSistema, primaryKey);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            serverName,
            primaryKey,
            codColigada,
            codSistema,
            result,
        });
    }

    [McpServerTool(Name = "rm_wcf_generic")]
    [Description("Executa qualquer operação WCF no RM via net.tcp. Para operações que não têm tool específica.")]
    public static async Task<string> GenericCall(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("TypeId do serviço (ex: RMSLoginServer, RMSBrokerServer, FinCxaData)")] string typeId,
        [Description("Nome do contrato de serviço (ex: IRMSDataServer, IRMSLoginServer)")] string serviceName,
        [Description("SOAPAction completa (ex: http://www.totvs.com/IRMSDataServer/ReadView)")] string soapAction,
        [Description("XML do body SOAP")] string bodyXml,
        [Description("Se true, autentica via ExecuteByPass antes da chamada")] bool authenticate = true,
        [Description("Alias do banco")] string? alias = null,
        [Description("Usuário")] string? userName = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var wcf = ToolHelper.CreateWcf(config);
        await wcf.IsSecurityEnabledAsync();

        if (authenticate)
        {
            var (success, errorCode, _) = await wcf.ExecuteByPassAsync(
                alias ?? config.WcfAlias, userName ?? config.WcfUser);
            if (!success)
            {
                return ToolHelper.ToJson(new
                {
                    error = "Falha na autenticação ExecuteByPass",
                    errorCode,
                });
            }
        }

        var result = await wcf.CallEndpointAsync(typeId, serviceName, soapAction, bodyXml);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            typeId,
            serviceName,
            soapAction,
            result,
        });
    }
}
