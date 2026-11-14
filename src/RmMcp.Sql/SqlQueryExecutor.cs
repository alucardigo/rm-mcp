using Microsoft.Data.SqlClient;

namespace RmMcp.Sql;

public sealed class SqlQueryExecutor : IDisposable
{
    private readonly string _connectionString;

    public SqlQueryExecutor(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<QueryResult> ExecuteAsync(string sql, int timeoutSeconds = 30,
        int maxRows = 1000, CancellationToken ct = default)
    {
        var (allowed, reason) = SqlGuard.Validate(sql);
        if (!allowed)
            throw new InvalidOperationException($"SQL bloqueado: {reason}");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
        return await ReadResultAsync(cmd, maxRows, ct);
    }

    public async Task<QueryResult> ExecuteParameterizedAsync(string sql,
        Dictionary<string, object?>? parameters = null,
        int timeoutSeconds = 30, int maxRows = 5000,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
        if (parameters != null)
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        return await ReadResultAsync(cmd, maxRows, ct);
    }

    public async Task<(string Server, string Version)> PingAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT @@SERVERNAME AS [Server], @@VERSION AS [Version]", conn)
        {
            CommandTimeout = 10
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Ping SQL não retornou resultado");

        return (reader.GetString(0), reader.GetString(1).Split('\n')[0]);
    }

    private static async Task<QueryResult> ReadResultAsync(SqlCommand cmd, int maxRows,
        CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var rows = new List<Dictionary<string, object?>>();
        bool truncated = false;

        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return new QueryResult
        {
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            Truncated = truncated,
        };
    }

    public void Dispose() { }
}

public sealed class QueryResult
{
    public required List<string> Columns { get; init; }
    public required List<Dictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public bool Truncated { get; init; }
}
