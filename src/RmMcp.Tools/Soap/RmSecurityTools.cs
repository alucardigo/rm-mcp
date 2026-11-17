using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Soap;

[McpServerToolType]
public static class RmSecurityTools
{
    [McpServerTool(Name = "rm_list_user_permissions")]
    [Description("Lista permissões de segurança de um usuário no RM")]
    public static async Task<string> ListUserPermissions(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int codColigada,
        [Description("Código do usuário")] string codUsuario,
        [Description("Máximo de registros (padrão 200)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 200, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                p.CODCOLIGADA, p.CODUSUARIO, p.CODSISTEMA,
                p.SUPERVISOR, p.CRIARELAT
            FROM GPERMIS p WITH (NOLOCK)
            WHERE p.CODCOLIGADA = @codColigada
              AND p.CODUSUARIO = @codUsuario
            ORDER BY p.CODSISTEMA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codColigada"] = codColigada,
                ["@codUsuario"] = codUsuario,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_profiles")]
    [Description("Lista perfis de segurança cadastrados no RM")]
    public static async Task<string> ListProfiles(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código do sistema (omitir = todos)")] string? codSistema = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                p.CODSISTEMA, p.CODPERFIL, p.NOME,
                p.STATUS, p.IDPERFIL
            FROM GPERFIL p WITH (NOLOCK)
            WHERE (@codSistema IS NULL OR p.CODSISTEMA = @codSistema)
            ORDER BY p.CODSISTEMA, p.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codSistema"] = codSistema,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_user_profiles")]
    [Description("Lista perfis atribuídos a um usuário")]
    public static async Task<string> UserProfiles(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int codColigada,
        [Description("Código do usuário")] string codUsuario)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT up.CODCOLIGADA, up.CODUSUARIO, up.CODPERFIL,
                   up.CODSISTEMA, p.NOME AS Perfil
            FROM GUSRPERFIL up WITH (NOLOCK)
            LEFT JOIN GPERFIL p WITH (NOLOCK)
                ON p.CODPERFIL = up.CODPERFIL
                AND p.CODSISTEMA = up.CODSISTEMA
            WHERE up.CODCOLIGADA = @codColigada
              AND up.CODUSUARIO = @codUsuario
            ORDER BY up.CODSISTEMA, p.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@codColigada"] = codColigada,
                ["@codUsuario"] = codUsuario,
            });

        return ToolHelper.FormatResult(result);
    }
}
