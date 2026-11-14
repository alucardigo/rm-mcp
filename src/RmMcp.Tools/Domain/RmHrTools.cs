using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmHrTools
{
    [McpServerTool(Name = "rm_list_employees")]
    [Description("Lista funcionários com filtros por coligada, situação, seção e função")]
    public static async Task<string> ListEmployees(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Situação: A=ativo, D=demitido, F=férias, T=afastado (omitir = todas)")] string? situacao = null,
        [Description("Código da seção/centro de custo (omitir = todas)")] string? secao = null,
        [Description("Código da função (omitir = todas)")] string? funcao = null,
        [Description("Máximo de registros (padrão 100, máximo 1000)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                f.CODCOLIGADA, f.CHAPA, f.NOME,
                f.CODSITUACAO, f.DATAADMISSAO, f.DATADEMISSAO,
                s.DESCRICAO AS Secao,
                fn.NOME AS Funcao,
                p.CPF, p.DTNASCIMENTO
            FROM PFUNC f WITH (NOLOCK)
            INNER JOIN PPESSOA p WITH (NOLOCK) ON p.CODIGO = f.CODPESSOA
            LEFT JOIN PSECAO s WITH (NOLOCK)
                ON s.CODCOLIGADA = f.CODCOLIGADA AND s.CODIGO = f.CODSECAO
            LEFT JOIN PFUNCAO fn WITH (NOLOCK)
                ON fn.CODCOLIGADA = f.CODCOLIGADA AND fn.CODIGO = f.CODFUNCAO
            WHERE f.CODCOLIGADA = @coligada
              AND (@situacao IS NULL OR f.CODSITUACAO = @situacao)
              AND (@secao IS NULL OR f.CODSECAO = @secao)
              AND (@funcao IS NULL OR f.CODFUNCAO = @funcao)
            ORDER BY f.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@situacao"] = situacao,
                ["@secao"] = secao,
                ["@funcao"] = funcao,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_get_employee")]
    [Description("Detalhes completos de um funcionário por chapa")]
    public static async Task<string> GetEmployee(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                f.CODCOLIGADA, f.CHAPA, f.NOME,
                f.CODSITUACAO, f.DATAADMISSAO, f.DATADEMISSAO,
                f.CODSECAO, s.DESCRICAO AS Secao,
                f.CODFUNCAO, fn.NOME AS Funcao,
                f.SALARIO, f.CODTIPO, f.CODSINDICATO,
                p.CPF, p.DTNASCIMENTO, p.SEXO,
                p.RUA, p.NUMERO, p.COMPLEMENTO, p.BAIRRO,
                p.CIDADE, p.ESTADO, p.CEP, p.TELEFONE1, p.EMAIL
            FROM PFUNC f WITH (NOLOCK)
            INNER JOIN PPESSOA p WITH (NOLOCK) ON p.CODIGO = f.CODPESSOA
            LEFT JOIN PSECAO s WITH (NOLOCK)
                ON s.CODCOLIGADA = f.CODCOLIGADA AND s.CODIGO = f.CODSECAO
            LEFT JOIN PFUNCAO fn WITH (NOLOCK)
                ON fn.CODCOLIGADA = f.CODCOLIGADA AND fn.CODIGO = f.CODFUNCAO
            WHERE f.CODCOLIGADA = @coligada AND f.CHAPA = @chapa
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_employee_count")]
    [Description("Contagem de efetivo por critérios (coligada, situação, seção)")]
    public static async Task<string> EmployeeCount(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Agrupar por: 'situacao', 'secao', 'funcao' ou 'total'")] string? groupBy = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        var group = (groupBy ?? "total").ToLowerInvariant();

        var sql = group switch
        {
            "situacao" => """
                SELECT f.CODSITUACAO AS Grupo, COUNT(*) AS Total
                FROM PFUNC f WITH (NOLOCK)
                WHERE f.CODCOLIGADA = @coligada
                GROUP BY f.CODSITUACAO ORDER BY Total DESC
                """,
            "secao" => """
                SELECT s.DESCRICAO AS Grupo, COUNT(*) AS Total
                FROM PFUNC f WITH (NOLOCK)
                LEFT JOIN PSECAO s WITH (NOLOCK)
                    ON s.CODCOLIGADA = f.CODCOLIGADA AND s.CODIGO = f.CODSECAO
                WHERE f.CODCOLIGADA = @coligada
                GROUP BY s.DESCRICAO ORDER BY Total DESC
                """,
            "funcao" => """
                SELECT fn.NOME AS Grupo, COUNT(*) AS Total
                FROM PFUNC f WITH (NOLOCK)
                LEFT JOIN PFUNCAO fn WITH (NOLOCK)
                    ON fn.CODCOLIGADA = f.CODCOLIGADA AND fn.CODIGO = f.CODFUNCAO
                WHERE f.CODCOLIGADA = @coligada
                GROUP BY fn.NOME ORDER BY Total DESC
                """,
            _ => """
                SELECT COUNT(*) AS Total
                FROM PFUNC f WITH (NOLOCK)
                WHERE f.CODCOLIGADA = @coligada
                """,
        };

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?> { ["@coligada"] = coligada });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_sections")]
    [Description("Lista seções/centros de custo do RM")]
    public static async Task<string> ListSections(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por descrição (LIKE pattern, ex: '%ENGENHARIA%')")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT s.CODCOLIGADA, s.CODIGO, s.DESCRICAO, s.CODSECAOPAI,
                   (SELECT COUNT(*) FROM PFUNC f WITH (NOLOCK)
                    WHERE f.CODCOLIGADA = s.CODCOLIGADA AND f.CODSECAO = s.CODIGO
                    AND f.CODSITUACAO = 'A') AS FuncionariosAtivos
            FROM PSECAO s WITH (NOLOCK)
            WHERE s.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR s.DESCRICAO LIKE @filter)
            ORDER BY s.DESCRICAO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_positions")]
    [Description("Lista funções/cargos do RM")]
    public static async Task<string> ListPositions(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por nome (LIKE pattern)")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT fn.CODCOLIGADA, fn.CODIGO, fn.NOME,
                   (SELECT COUNT(*) FROM PFUNC f WITH (NOLOCK)
                    WHERE f.CODCOLIGADA = fn.CODCOLIGADA AND f.CODFUNCAO = fn.CODIGO
                    AND f.CODSITUACAO = 'A') AS FuncionariosAtivos
            FROM PFUNCAO fn WITH (NOLOCK)
            WHERE fn.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR fn.NOME LIKE @filter)
            ORDER BY fn.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_employee_history")]
    [Description("Histórico funcional de um funcionário (promoções, transferências)")]
    public static async Task<string> EmployeeHistory(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT h.DTMUDANCA, h.MOTIVO,
                   h.CODSECAO, s.DESCRICAO AS Secao,
                   h.CODFUNCAO, fn.NOME AS Funcao,
                   h.SALARIO
            FROM PFHSTFUN h WITH (NOLOCK)
            LEFT JOIN PSECAO s WITH (NOLOCK)
                ON s.CODCOLIGADA = h.CODCOLIGADA AND s.CODIGO = h.CODSECAO
            LEFT JOIN PFUNCAO fn WITH (NOLOCK)
                ON fn.CODCOLIGADA = h.CODCOLIGADA AND fn.CODIGO = h.CODFUNCAO
            WHERE h.CODCOLIGADA = @coligada AND h.CHAPA = @chapa
            ORDER BY h.DTMUDANCA DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_dependents")]
    [Description("Lista dependentes de um funcionário")]
    public static async Task<string> ListDependents(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Chapa do funcionário")] string chapa)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT d.CODCOLIGADA, d.CHAPA, d.NRODEPEND,
                   p.NOME, p.CPF, p.DTNASCIMENTO, p.SEXO,
                   d.GRAUPARENTESCO, d.IRRF, d.SALFAMILIA
            FROM PDEPEND d WITH (NOLOCK)
            INNER JOIN PPESSOA p WITH (NOLOCK) ON p.CODIGO = d.CODPESSOA
            WHERE d.CODCOLIGADA = @coligada AND d.CHAPA = @chapa
            ORDER BY d.NRODEPEND
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@chapa"] = chapa,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_payroll_summary")]
    [Description("Resumo de folha de pagamento por período")]
    public static async Task<string> PayrollSummary(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Mês/ano de referência (1-12)")] int mes,
        [Description("Ano de referência")] int ano)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                COUNT(DISTINCT e.CHAPA) AS Funcionarios,
                SUM(CASE WHEN e.CODTIPO IN ('P','V') THEN e.VALOR ELSE 0 END) AS TotalProventos,
                SUM(CASE WHEN e.CODTIPO = 'D' THEN e.VALOR ELSE 0 END) AS TotalDescontos,
                SUM(CASE WHEN e.CODTIPO IN ('P','V') THEN e.VALOR ELSE 0 END)
                  - SUM(CASE WHEN e.CODTIPO = 'D' THEN e.VALOR ELSE 0 END) AS Liquido
            FROM PFENCARGO e WITH (NOLOCK)
            WHERE e.CODCOLIGADA = @coligada
              AND e.MESREF = @mes AND e.ANOREF = @ano
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@mes"] = mes,
                ["@ano"] = ano,
            });

        return ToolHelper.FormatResult(result);
    }
}
