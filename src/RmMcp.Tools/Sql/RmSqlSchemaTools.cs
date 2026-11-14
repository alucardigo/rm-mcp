using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Sql;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Sql;

[McpServerToolType]
public static class RmSqlSchemaTools
{
    [McpServerTool(Name = "rm_sql_tables")]
    [Description("Lista tabelas e views do banco Corpore com contagem de linhas")]
    public static async Task<string> Tables(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Filtro por nome (LIKE pattern, ex: '%PFUNC%'). Omitir retorna todas.")] string? filter = null)
    {
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                SCHEMA_NAME(t.schema_id) AS [schema],
                t.name AS [name],
                t.type_desc AS [type],
                SUM(p.rows) AS [rows]
            FROM sys.tables t
            LEFT JOIN sys.partitions p
                ON t.object_id = p.object_id AND p.index_id IN (0, 1)
            WHERE (@filter IS NULL OR t.name LIKE @filter)
            GROUP BY SCHEMA_NAME(t.schema_id), t.name, t.type_desc
            UNION ALL
            SELECT
                SCHEMA_NAME(v.schema_id),
                v.name,
                'VIEW',
                NULL
            FROM sys.views v
            WHERE (@filter IS NULL OR v.name LIKE @filter)
            ORDER BY [schema], [name]
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?> { ["@filter"] = filter });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_sql_columns")]
    [Description("Descreve colunas de uma tabela ou view do banco Corpore")]
    public static async Task<string> Columns(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome da tabela ou view (ex: PFUNC, dbo.PPESSOA)")] string table)
    {
        var validated = SqlIdentifier.Validate(table);
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        var tableName = validated.Contains('.') ? validated.Split('.')[1] : validated;
        var schema = validated.Contains('.') ? validated.Split('.')[0] : null;

        var sql = """
            SELECT
                c.COLUMN_NAME AS [column],
                c.DATA_TYPE AS [type],
                c.CHARACTER_MAXIMUM_LENGTH AS [maxLength],
                c.NUMERIC_PRECISION AS [precision],
                c.NUMERIC_SCALE AS [scale],
                c.IS_NULLABLE AS [nullable],
                c.COLUMN_DEFAULT AS [default],
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END AS [primaryKey]
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = @table
                AND (@schema IS NULL OR c.TABLE_SCHEMA = @schema)
            ORDER BY c.ORDINAL_POSITION
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?>
            {
                ["@table"] = tableName,
                ["@schema"] = schema,
            });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_sql_fks")]
    [Description("Mostra foreign keys de uma tabela do Corpore")]
    public static async Task<string> ForeignKeys(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome da tabela (ex: PFUNC)")] string table)
    {
        var validated = SqlIdentifier.Validate(table);
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        const string sql = """
            SELECT
                fk.name AS [constraint],
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS [column],
                OBJECT_NAME(fkc.referenced_object_id) AS [referencedTable],
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS [referencedColumn],
                fk.delete_referential_action_desc AS [onDelete],
                fk.update_referential_action_desc AS [onUpdate]
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc
                ON fk.object_id = fkc.constraint_object_id
            WHERE fk.parent_object_id = OBJECT_ID(@table)
            ORDER BY fk.name, fkc.constraint_column_id
            """;

        var result = await executor.ExecuteParameterizedAsync(sql,
            new Dictionary<string, object?> { ["@table"] = validated });

        return ToolHelper.FormatResult(result);
    }

    [McpServerTool(Name = "rm_sql_sample")]
    [Description("Retorna amostra de dados de uma tabela (SELECT TOP N)")]
    public static async Task<string> Sample(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Nome da tabela ou view (ex: PFUNC)")] string table,
        [Description("Número de linhas (padrão 5, máximo 100)")] int? rows = null)
    {
        var bracketed = SqlIdentifier.Bracket(table);
        var n = Math.Clamp(rows ?? 5, 1, 100);
        var config = ToolHelper.LoadConfig(env);
        using var executor = ToolHelper.CreateSql(config);

        var sql = $"SELECT TOP {n} * FROM {bracketed}";

        var result = await executor.ExecuteAsync(sql, maxRows: n);
        return ToolHelper.FormatResult(result);
    }
}
