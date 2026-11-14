using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmGlobalTools
{
    [McpServerTool(Name = "rm_list_companies")]
    [Description("Lista coligadas (empresas do grupo) cadastradas no RM")]
    public static async Task<string> ListCompanies(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT g.CODCOLIGADA, g.NOMEFANTASIA, g.NOME, g.CGCCFO AS CNPJ,
                   g.CIDADE, g.ESTADO
            FROM GCOLIGADA g WITH (NOLOCK)
            ORDER BY g.CODCOLIGADA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>());

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_branches")]
    [Description("Lista filiais de uma coligada")]
    public static async Task<string> ListBranches(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT f.CODCOLIGADA, f.CODFILIAL, f.NOMEFANTASIA, f.NOME,
                   f.CGCCFO AS CNPJ, f.CIDADE, f.ESTADO
            FROM GFILIAL f WITH (NOLOCK)
            WHERE f.CODCOLIGADA = @coligada
            ORDER BY f.CODFILIAL
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?> { ["@coligada"] = coligada });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_users")]
    [Description("Lista usuários do sistema RM")]
    public static async Task<string> ListUsers(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Filtro por nome ou login (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 500);

        var sql = $"""
            SELECT TOP {n}
                u.CODUSUARIO, u.NOME, u.ATIVO
            FROM GUSUARIO u WITH (NOLOCK)
            WHERE (@filter IS NULL OR u.CODUSUARIO LIKE @filter OR u.NOME LIKE @filter)
            ORDER BY u.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?> { ["@filter"] = filter });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_cost_centers")]
    [Description("Lista centros de custo")]
    public static async Task<string> ListCostCenters(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por descrição (LIKE pattern)")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT s.CODCOLIGADA, s.CODIGO AS CodCentroCusto,
                   s.DESCRICAO, s.CODSECAOPAI
            FROM PSECAO s WITH (NOLOCK)
            WHERE s.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR s.DESCRICAO LIKE @filter)
            ORDER BY s.CODIGO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_custom_tables")]
    [Description("Lista tabelas customizadas da MIP (ZMIP_*)")]
    public static async Task<string> CustomTables(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                SCHEMA_NAME(t.schema_id) AS [Schema],
                t.name AS Tabela,
                SUM(p.rows) AS Linhas
            FROM sys.tables t
            LEFT JOIN sys.partitions p
                ON t.object_id = p.object_id AND p.index_id IN (0, 1)
            WHERE t.name LIKE 'ZMIP[_]%'
            GROUP BY SCHEMA_NAME(t.schema_id), t.name
            ORDER BY t.name
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>());

        return ToolHelper.FormatResult(result);
    }
}
