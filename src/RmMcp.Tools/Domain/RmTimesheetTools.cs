using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmTimesheetTools
{
    [McpServerTool(Name = "rm_timesheet")]
    [Description("Espelho de ponto de um funcionário por período")]
    public static async Task<string> Timesheet(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa,
        [Description("Data inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data final (yyyy-MM-dd)")] string dataFim)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT a.CODCOLIGADA, a.CHAPA, a.DATA,
                   a.ENTRADA1, a.SAIDA1, a.ENTRADA2, a.SAIDA2,
                   a.ENTRADA3, a.SAIDA3, a.ENTRADA4, a.SAIDA4,
                   a.ABESSION, a.ABESSION2, a.CODABONO
            FROM AAFDT a WITH (NOLOCK)
            WHERE a.CODCOLIGADA = @coligada
              AND a.CHAPA = @chapa
              AND a.DATA BETWEEN @dataInicio AND @dataFim
            ORDER BY a.DATA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_absences")]
    [Description("Faltas e afastamentos por período")]
    public static async Task<string> Absences(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Data inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data final (yyyy-MM-dd)")] string dataFim,
        [Description("Chapa do funcionário (omitir = todos)")] string? chapa = null,
        [Description("Máximo de registros (padrão 200)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 200, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                a.CODCOLIGADA, a.CHAPA, f.NOME, a.DATA,
                a.CODABONO, a.ABESSION AS Motivo
            FROM AAFDT a WITH (NOLOCK)
            INNER JOIN PFUNC f WITH (NOLOCK)
                ON f.CODCOLIGADA = a.CODCOLIGADA AND f.CHAPA = a.CHAPA
            WHERE a.CODCOLIGADA = @coligada
              AND a.DATA BETWEEN @dataInicio AND @dataFim
              AND a.CODABONO IS NOT NULL
              AND (@chapa IS NULL OR a.CHAPA = @chapa)
            ORDER BY a.DATA DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_overtime")]
    [Description("Horas extras por período")]
    public static async Task<string> Overtime(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Data inicial (yyyy-MM-dd)")] string dataInicio,
        [Description("Data final (yyyy-MM-dd)")] string dataFim,
        [Description("Chapa do funcionário (omitir = todos)")] string? chapa = null,
        [Description("Máximo de registros (padrão 200)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 200, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                o.CODCOLIGADA, o.CHAPA, f.NOME,
                o.DATA, o.CODOCORR, o.QTDOCORR AS Horas
            FROM AOCORRENCIACALCULADAAM o WITH (NOLOCK)
            INNER JOIN PFUNC f WITH (NOLOCK)
                ON f.CODCOLIGADA = o.CODCOLIGADA AND f.CHAPA = o.CHAPA
            WHERE o.CODCOLIGADA = @coligada
              AND o.DATA BETWEEN @dataInicio AND @dataFim
              AND (@chapa IS NULL OR o.CHAPA = @chapa)
            ORDER BY o.DATA DESC, o.CHAPA
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_clock_entries")]
    [Description("Batidas brutas do relógio de ponto")]
    public static async Task<string> ClockEntries(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa,
        [Description("Data (yyyy-MM-dd)")] string data)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT a.CODCOLIGADA, a.CHAPA, a.DATA,
                   a.ENTRADA1, a.SAIDA1, a.ENTRADA2, a.SAIDA2,
                   a.ENTRADA3, a.SAIDA3, a.ENTRADA4, a.SAIDA4
            FROM AAFDT a WITH (NOLOCK)
            WHERE a.CODCOLIGADA = @coligada
              AND a.CHAPA = @chapa AND a.DATA = @data
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
                ["@data"] = data,
            });

        return ToolHelper.FormatResult(result);
    }
}
