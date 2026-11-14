using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Domain;

[McpServerToolType]
public static class RmPurchasingTools
{
    [McpServerTool(Name = "rm_list_movements")]
    [Description("Lista movimentos (OC/RM/NF) com filtros por TMV, período e fornecedor")]
    public static async Task<string> ListMovements(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Tipo de movimento/TMV (ex: '1.1.01', omitir = todos)")] string? tmv = null,
        [Description("Data inicial (yyyy-MM-dd, omitir = sem filtro)")] string? dataInicio = null,
        [Description("Data final (yyyy-MM-dd, omitir = sem filtro)")] string? dataFim = null,
        [Description("Código do fornecedor FCFO (omitir = todos)")] string? codFornecedor = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                m.CODCOLIGADA, m.IDMOV, m.CODTMV,
                m.NUMEROMOV, m.DATAEMISSAO, m.DATAMOVIMENTO,
                m.CODCFO, c.NOME AS Fornecedor,
                m.STATUS, m.VALORBRUTO, m.VALORLIQUIDO
            FROM TMOV m WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = m.CODCOLIGADA AND c.CODCFO = m.CODCFO
            WHERE m.CODCOLIGADA = @coligada
              AND (@tmv IS NULL OR m.CODTMV = @tmv)
              AND (@dataInicio IS NULL OR m.DATAEMISSAO >= @dataInicio)
              AND (@dataFim IS NULL OR m.DATAEMISSAO <= @dataFim)
              AND (@codFornecedor IS NULL OR m.CODCFO = @codFornecedor)
            ORDER BY m.DATAEMISSAO DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@tmv"] = tmv,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
                ["@codFornecedor"] = codFornecedor,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_get_movement")]
    [Description("Detalhes de um movimento (cabeçalho + itens)")]
    public static async Task<string> GetMovement(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("ID do movimento (IDMOV)")] int idMov)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT m.CODCOLIGADA, m.IDMOV, m.CODTMV, m.NUMEROMOV,
                   m.DATAEMISSAO, m.DATAMOVIMENTO, m.CODCFO,
                   c.NOME AS Fornecedor, c.CGCCFO AS CNPJ,
                   m.STATUS, m.VALORBRUTO, m.VALORLIQUIDO, m.CODFILIAL,
                   i.NSEQITMMOV, i.IDPRD, p.CODIGOPRD, p.DESCRICAO AS Produto,
                   i.QUANTIDADE, i.PRECOUNITARIO, i.VALORLIQUIDO1 AS ValorItem
            FROM TMOV m WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = m.CODCOLIGADA AND c.CODCFO = m.CODCFO
            LEFT JOIN TITMMOV i WITH (NOLOCK)
                ON i.CODCOLIGADA = m.CODCOLIGADA AND i.IDMOV = m.IDMOV
            LEFT JOIN TPRODUTO p WITH (NOLOCK)
                ON p.CODCOLIGADA = i.CODCOLIGADA AND p.IDPRD = i.IDPRD
            WHERE m.CODCOLIGADA = @coligada AND m.IDMOV = @idMov
            ORDER BY i.NSEQITMMOV
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@idMov"] = idMov,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_products")]
    [Description("Lista produtos/materiais do RM")]
    public static async Task<string> ListProducts(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por descrição (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                p.CODCOLIGADA, p.IDPRD, p.CODIGOPRD, p.DESCRICAO,
                p.CODUND, p.CODTIPOPRODUTO, p.INATIVO
            FROM TPRODUTO p WITH (NOLOCK)
            WHERE p.CODCOLIGADA = @coligada
              AND (@filter IS NULL OR p.DESCRICAO LIKE @filter)
            ORDER BY p.DESCRICAO
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_list_suppliers")]
    [Description("Lista fornecedores (FCFO) do RM")]
    public static async Task<string> ListSuppliers(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Filtro por nome (LIKE pattern)")] string? filter = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                c.CODCOLIGADA, c.CODCFO, c.NOME, c.CGCCFO AS CNPJ,
                c.RUA, c.CIDADE, c.ESTADO, c.CEP,
                c.TELEFONE, c.EMAIL, c.ATIVO
            FROM FCFO c WITH (NOLOCK)
            WHERE c.CODCOLIGADA = @coligada
              AND c.PAESSION = 'F'
              AND (@filter IS NULL OR c.NOME LIKE @filter)
            ORDER BY c.NOME
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@filter"] = filter,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_purchase_orders")]
    [Description("Ordens de compra por status/período")]
    public static async Task<string> PurchaseOrders(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Status do movimento (omitir = todos)")] string? status = null,
        [Description("Data inicial (yyyy-MM-dd)")] string? dataInicio = null,
        [Description("Data final (yyyy-MM-dd)")] string? dataFim = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var sql = $"""
            SELECT TOP {n}
                m.CODCOLIGADA, m.IDMOV, m.CODTMV, m.NUMEROMOV,
                m.DATAEMISSAO, m.CODCFO, c.NOME AS Fornecedor,
                m.STATUS, m.VALORBRUTO, m.VALORLIQUIDO
            FROM TMOV m WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = m.CODCOLIGADA AND c.CODCFO = m.CODCFO
            WHERE m.CODCOLIGADA = @coligada
              AND m.CODTMV LIKE '1.1%'
              AND (@status IS NULL OR m.STATUS = @status)
              AND (@dataInicio IS NULL OR m.DATAEMISSAO >= @dataInicio)
              AND (@dataFim IS NULL OR m.DATAEMISSAO <= @dataFim)
            ORDER BY m.DATAEMISSAO DESC
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@coligada"] = coligada,
                ["@status"] = status,
                ["@dataInicio"] = dataInicio,
                ["@dataFim"] = dataFim,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_invoices")]
    [Description("Notas fiscais de entrada/saída")]
    public static async Task<string> Invoices(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Código da coligada")] int coligada,
        [Description("Tipo: 'entrada' ou 'saida' (omitir = ambos)")] string? tipo = null,
        [Description("Data inicial (yyyy-MM-dd)")] string? dataInicio = null,
        [Description("Data final (yyyy-MM-dd)")] string? dataFim = null,
        [Description("Máximo de registros (padrão 100)")] int? limit = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);
        var n = Math.Clamp(limit ?? 100, 1, 1000);

        var tipoFilter = tipo?.ToLowerInvariant() switch
        {
            "entrada" => "AND m.CODTMV LIKE '1.2%'",
            "saida" => "AND m.CODTMV LIKE '2.2%'",
            _ => "",
        };

        var sql = $"""
            SELECT TOP {n}
                m.CODCOLIGADA, m.IDMOV, m.CODTMV, m.NUMEROMOV,
                m.DATAEMISSAO, m.CODCFO, c.NOME AS Fornecedor,
                m.VALORBRUTO, m.VALORLIQUIDO, m.STATUS
            FROM TMOV m WITH (NOLOCK)
            LEFT JOIN FCFO c WITH (NOLOCK)
                ON c.CODCOLIGADA = m.CODCOLIGADA AND c.CODCFO = m.CODCFO
            WHERE m.CODCOLIGADA = @coligada
              {tipoFilter}
              AND (@dataInicio IS NULL OR m.DATAEMISSAO >= @dataInicio)
              AND (@dataFim IS NULL OR m.DATAEMISSAO <= @dataFim)
            ORDER BY m.DATAEMISSAO DESC
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
}
