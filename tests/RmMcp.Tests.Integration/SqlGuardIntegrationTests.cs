using RmMcp.Sql;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Integration tests for SqlGuard + SqlQueryExecutor: verifies that the guard
/// blocks dangerous statements while allowing legitimate SELECTs against real data.
/// </summary>
[Collection("Docker")]
public class SqlGuardIntegrationTests
{
    private readonly DockerFixture _docker;

    public SqlGuardIntegrationTests(DockerFixture docker) => _docker = docker;

    [Fact]
    public async Task AllowsSelectWithNolock()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT TOP 1 NOME FROM GCOLIGADA WITH (NOLOCK)");

        Assert.Single(result.Rows);
        Assert.Equal("MIP Engenharia S.A.", result.Rows[0]["NOME"]);
    }

    [Fact]
    public async Task AllowsCte()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync("""
            WITH ativos AS (
                SELECT CHAPA, NOME FROM PFUNC WHERE CODCOLIGADA = 1 AND CODSITUACAO = 'A'
            )
            SELECT COUNT(*) AS Total FROM ativos
            """);

        Assert.Single(result.Rows);
        Assert.Equal(2, result.Rows[0]["Total"]);
    }

    [Fact]
    public async Task BlocksInsert()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("INSERT INTO GCOLIGADA VALUES (99, 'Hack', '', '', '', '')"));
    }

    [Fact]
    public async Task BlocksUpdate()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("UPDATE PFUNC SET SALARIO = 999999 WHERE CHAPA = '00001'"));
    }

    [Fact]
    public async Task BlocksDelete()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("DELETE FROM PFUNC"));
    }

    [Fact]
    public async Task BlocksDrop()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("DROP TABLE PFUNC"));
    }
}
