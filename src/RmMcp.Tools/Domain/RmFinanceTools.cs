using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmFinanceTools
{
    [McpServerTool(Name = "rm_list_entries")]
    [Description("Lista lançamentos financeiros com filtros")]
    public static async Task<string> ListEntries(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Tipo: 1=pagar, 2=receber (omitir = ambos)")] int? tipo = null,
        [Description("Data vencimento inicial (yyyy-MM-dd)")] string? dataInicio = null,
        [Description("Data vencimento final (yyyy-MM-dd)")] string? dataFim = null,
        [Description("Código do fornecedor/cliente FCFO")] string? codCfo = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                l.CODCOLIGADA, l.IDLAN, l.CODCFO,
                c.NOME AS Parceiro, l.CODTDO,
                l.PAESSION AS Tipo, l.STATUSLAN AS Status,
                l.DATAVENCIMENTO, l.DATABAIXA,
                l.VALORORIGINAL, l.VALORBAIXA, l.SALDO
            FROM FLAN l WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = l.CODCOLIGADA AND c.CODCFO = l.CODCFO
            WHERE l.CODCOLIGADA = @coligada
              AND (@tipo IS NULL OR l.PAESSION = @tipo)
              AND (@dataInicio IS NULL OR l.DATAVENCIMENTO >= @dataInicio)
              AND (@dataFim IS NULL OR l.DATAVENCIMENTO <= @dataFim)
              AND (@codCfo IS NULL OR l.CODCFO = @codCfo)
            ORDER BY l.DATAVENCIMENTO DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@tipo"] = tipo,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
                ["@codCfo"] = codCfo,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_get_entry")]
    [Description("Detalhes de um lançamento financeiro com histórico de baixas")]
    public static async Task<string> GetEntry(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("ID do lançamento (IDLAN)")] int idLan)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT l.CODCOLIGADA, l.IDLAN, l.CODCFO,
                   c.NOME AS Parceiro, l.CODTDO,
                   l.PAESSION AS Tipo, l.STATUSLAN AS Status,
                   l.DATAVENCIMENTO, l.DATABAIXA,
                   l.VALORORIGINAL, l.VALORBAIXA, l.SALDO,
                   l.NUMERODOCUMENTO, l.HISTORICOCURTO,
                   h.IDBAIXA, h.DATABAIXA AS DataBaixaHist,
                   h.VALORBAIXADO, h.VALORJURO, h.VALORMULTA, h.VALORDESCONTO
            FROM FLAN l WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = l.CODCOLIGADA AND c.CODCFO = l.CODCFO
            LEFT JOIN FLANBAIXA h WITH (NOLOCK)
                ON h.CODCOLIGADA = l.CODCOLIGADA AND h.IDLAN = l.IDLAN
            WHERE l.CODCOLIGADA = @coligada AND l.IDLAN = @idLan
            ORDER BY h.IDBAIXA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@idLan"] = idLan,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_cashflow")]
    [Description("Fluxo de caixa por período (a pagar vs a receber)")]
    public static async Task<string> Cashflow(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Data vencimento inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data vencimento final (yyyy-MM-dd)")] string dataFim)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                CONVERT(VARCHAR(7), l.DATAVENCIMENTO, 120) AS Periodo,
                SUM(CASE WHEN l.PAESSION = 2 THEN l.SALDO ELSE 0 END) AS AReceber,
                SUM(CASE WHEN l.PAESSION = 1 THEN l.SALDO ELSE 0 END) AS APagar,
                SUM(CASE WHEN l.PAESSION = 2 THEN l.SALDO ELSE 0 END)
                  - SUM(CASE WHEN l.PAESSION = 1 THEN l.SALDO ELSE 0 END) AS Saldo
            FROM FLAN l WITH (NOLOCK)
            WHERE l.CODCOLIGADA = @coligada
              AND l.STATUSLAN IN (0, 1)
              AND l.DATAVENCIMENTO BETWEEN @dataInicio AND @dataFim
            GROUP BY CONVERT(VARCHAR(7), l.DATAVENCIMENTO, 120)
            ORDER BY Periodo
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

    [McpServerTool(Name = "rm_aging")]
    [Description("Aging financeiro (vencidos x a vencer por faixa)")]
    public static async Task<string> Aging(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Tipo: 1=pagar, 2=receber")] int tipo,
        [Description("Data de referência (yyyy-MM-dd, padrão = hoje)")] string? dataRef = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                CASE
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 90 THEN '>90 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 60 THEN '61-90 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 30 THEN '31-60 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 0  THEN '1-30 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > -30 THEN 'A vencer 1-30'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > -60 THEN 'A vencer 31-60'
                    ELSE 'A vencer >60'
                END AS Faixa,
                COUNT(*) AS Quantidade,
                SUM(l.SALDO) AS Valor
            FROM FLAN l WITH (NOLOCK)
            WHERE l.CODCOLIGADA = @coligada
              AND l.PAESSION = @tipo
              AND l.STATUSLAN IN (0, 1)
              AND l.SALDO > 0
            GROUP BY
                CASE
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 90 THEN '>90 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 60 THEN '61-90 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 30 THEN '31-60 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > 0  THEN '1-30 dias'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > -30 THEN 'A vencer 1-30'
                    WHEN DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE())) > -60 THEN 'A vencer 31-60'
                    ELSE 'A vencer >60'
                END
            ORDER BY MIN(DATEDIFF(DAY, l.DATAVENCIMENTO, ISNULL(@dataRef, GETDATE()))) DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@tipo"] = tipo,
                ["@dataRef"] = dataRef,
            });

        return ToolHelper.FormatResult(result);
    }
}
