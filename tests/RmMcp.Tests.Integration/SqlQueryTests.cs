using RmMcp.Sql;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Integration tests for SQL query execution against Docker SQL Server
/// with the seeded Corpore test database. Asserts actual data correctness.
/// </summary>
[Collection("Docker")]
public class SqlQueryTests
{
    private readonly DockerFixture _docker;

    public SqlQueryTests(DockerFixture docker) => _docker = docker;

    [Fact]
    public async Task Ping_ReturnsServerNameAndVersion()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var (server, version) = await executor.PingAsync();

        Assert.False(string.IsNullOrWhiteSpace(server));
        Assert.Contains("Microsoft SQL Server", version);
    }

    [Fact]
    public async Task Query_Coligadas_ReturnsMipEngenharia()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT CODCOLIGADA, NOME, ESTADO FROM GCOLIGADA ORDER BY CODCOLIGADA");

        Assert.Equal(2, result.RowCount);
        Assert.Equal(1, result.Rows[0]["CODCOLIGADA"]);
        Assert.Equal("MIP Engenharia S.A.", result.Rows[0]["NOME"]);
        Assert.Equal("RJ", result.Rows[0]["ESTADO"]);
        Assert.Equal(2, result.Rows[1]["CODCOLIGADA"]);
        Assert.Equal("SP", result.Rows[1]["ESTADO"]);
    }

    [Fact]
    public async Task Query_Funcionarios_ReturnsSeedData()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT CHAPA, NOME, CODSITUACAO, SALARIO FROM PFUNC WHERE CODCOLIGADA = 1 ORDER BY CHAPA");

        Assert.Equal(3, result.RowCount);

        Assert.Equal("00001", result.Rows[0]["CHAPA"]);
        Assert.Equal("Joao Silva", result.Rows[0]["NOME"]);
        Assert.Equal("A", result.Rows[0]["CODSITUACAO"]);
        Assert.Equal(12000.00m, result.Rows[0]["SALARIO"]);

        Assert.Equal("00002", result.Rows[1]["CHAPA"]);
        Assert.Equal("Maria Santos", result.Rows[1]["NOME"]);

        Assert.Equal("00003", result.Rows[2]["CHAPA"]);
        Assert.Equal("D", result.Rows[2]["CODSITUACAO"]);
    }

    [Fact]
    public async Task Query_Parametrized_FiltersByColigadaAndChapa()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteParameterizedAsync(
            "SELECT NOME, SALARIO FROM PFUNC WHERE CODCOLIGADA = @cod AND CHAPA = @ch",
            new Dictionary<string, object?> { ["@cod"] = 1, ["@ch"] = "00001" });

        Assert.Single(result.Rows);
        Assert.Equal("Joao Silva", result.Rows[0]["NOME"]);
    }

    [Fact]
    public async Task Query_Movimentos_ReturnsCorrectValues()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT IDMOV, CODTMV, NUMEROMOV, VALORBRUTO FROM TMOV WHERE CODCOLIGADA = 1 ORDER BY IDMOV");

        Assert.Equal(3, result.RowCount);
        Assert.Equal("OC-001", result.Rows[0]["NUMEROMOV"]);
        Assert.Equal(50000.00m, result.Rows[0]["VALORBRUTO"]);
        Assert.Equal("1.1.01", result.Rows[0]["CODTMV"]);
    }

    [Fact]
    public async Task Query_LancamentosFinanceiros_ReturnsTypes()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT IDLAN, CODCFO, PAESSION, SALDO FROM FLAN WHERE CODCOLIGADA = 1 ORDER BY IDLAN");

        Assert.Equal(3, result.RowCount);
        Assert.Equal("F001", result.Rows[0]["CODCFO"]);
        Assert.Equal(50000.00m, result.Rows[0]["SALDO"]);
        Assert.Equal(0m, result.Rows[2]["SALDO"]);
    }

    [Fact]
    public async Task Query_PartidasContabeis_DebCred()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT CODCONTA, DEBCRED, VALOR FROM CPARTIDA WHERE CODCOLIGADA = 1 ORDER BY IDCONTABIL");

        Assert.Equal(4, result.RowCount);
        Assert.Equal("D", result.Rows[0]["DEBCRED"]);
        Assert.Equal(200000.00m, result.Rows[0]["VALOR"]);
        Assert.Equal("C", result.Rows[1]["DEBCRED"]);
    }

    [Fact]
    public async Task Query_MaxRows_Truncates()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT * FROM PFUNC", maxRows: 2);

        Assert.Equal(2, result.RowCount);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task Query_CustomMipTable_ReturnsData()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync(
            "SELECT PARAMETRO, VALOR FROM ZMIP_OBRA_CONFIG WHERE CODCOLIGADA = 1 AND CODOBRA = '357' ORDER BY PARAMETRO");

        Assert.Equal(2, result.RowCount);
        Assert.Equal("ATIVA", result.Rows[0]["PARAMETRO"]);
        Assert.Equal("S", result.Rows[0]["VALOR"]);
    }

    [Fact]
    public async Task Query_RmVersion_FromGparams()
    {
        using var executor = new SqlQueryExecutor(_docker.SqlConnectionString);
        var result = await executor.ExecuteAsync("SELECT VERSAO FROM GPARAMS");

        Assert.Single(result.Rows);
        Assert.Equal("12.1.2406", result.Rows[0]["VERSAO"]);
    }
}
