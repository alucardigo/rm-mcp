using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmAccountingTools
{
    [McpServerTool(Name = "rm_journal_entries")]
    [Description("Partidas contábeis por período")]
    public static async Task<string> JournalEntries(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Data inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data final (yyyy-MM-dd)")] string dataFim,
        [Description("Código da conta contábil (omitir = todas)")] string? conta = null,
        [Description("Máximo de registros (padrão 200)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 200, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                p.CODCOLIGADA, p.IDCONTABIL, p.CODCONTA,
                cc.DESCRICAO AS Conta,
                p.DATALANCTO, p.DEBCRED,
                p.VALOR, p.HISTORICO, p.CODCCUSTO
            FROM CPARTIDA p WITH (NOLOCK)
            LEFT JOIN CCONTA cc WITH (NOLOCK)
                ON cc.CODCOLIGADA = p.CODCOLIGADA AND cc.CODCONTA = p.CODCONTA
            WHERE p.CODCOLIGADA = @coligada
              AND p.DATALANCTO BETWEEN @dataInicio AND @dataFim
              AND (@conta IS NULL OR p.CODCONTA = @conta)
            ORDER BY p.DATALANCTO DESC, p.IDCONTABIL
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
                ["@conta"] = conta,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_trial_balance")]
    [Description("Balancete por período com saldo devedor/credor")]
    public static async Task<string> TrialBalance(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Data inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data final (yyyy-MM-dd)")] string dataFim,
        [Description("Nível de detalhe do plano de contas (1-5, padrão 3)")] int? nivel = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var lvl = Math.Clamp(nivel ?? 3, 1, 5);

        var sql = $"""
            SELECT
                LEFT(p.CODCONTA, {lvl}) AS ContaResumo,
                MIN(cc.DESCRICAO) AS Descricao,
                SUM(CASE WHEN p.DEBCRED = 'D' THEN p.VALOR ELSE 0 END) AS Debito,
                SUM(CASE WHEN p.DEBCRED = 'C' THEN p.VALOR ELSE 0 END) AS Credito,
                SUM(CASE WHEN p.DEBCRED = 'D' THEN p.VALOR ELSE -p.VALOR END) AS Saldo
            FROM CPARTIDA p WITH (NOLOCK)
            LEFT JOIN CCONTA cc WITH (NOLOCK)
                ON cc.CODCOLIGADA = p.CODCOLIGADA AND cc.CODCONTA = p.CODCONTA
            WHERE p.CODCOLIGADA = @coligada
              AND p.DATALANCTO BETWEEN @dataInicio AND @dataFim
            GROUP BY LEFT(p.CODCONTA, {lvl})
            ORDER BY ContaResumo
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_chart_of_accounts")]
    [Description("Plano de contas contábeis")]
    public static async Task<string> ChartOfAccounts(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por código ou descrição (LIKE pattern)")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT c.CODCOLIGADA, c.CODCONTA, c.DESCRICAO,
                   c.GRAU, c.ANESSION AS Analitica,
                   c.CODCONTAPAI, c.CODREDUZIDO
            FROM CCONTA c WITH (NOLOCK)
            WHERE c.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR c.CODCONTA LIKE @filter OR c.DESCRICAO LIKE @filter)
            ORDER BY c.CODCONTA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }
}
