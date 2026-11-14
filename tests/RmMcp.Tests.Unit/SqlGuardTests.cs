using RmMcp.Sql;

namespace RmMcp.Tests.Unit;

public class SqlGuardTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT * FROM PFUNC")]
    [InlineData("SELECT TOP 10 NOME FROM PPESSOA WHERE CODCOLIGADA = 1")]
    [InlineData("select lower(nome) from ppessoa")]
    public void Allows_Valid_Select(string sql)
    {
        var (allowed, reason) = SqlGuard.Validate(sql);
        Assert.True(allowed, reason);
    }

    [Fact]
    public void Allows_CTE()
    {
        const string sql = """
            WITH cte AS (
                SELECT CODCOLIGADA, COUNT(*) AS Total
                FROM PFUNC
                GROUP BY CODCOLIGADA
            )
            SELECT * FROM cte WHERE Total > 100
            """;
        var (allowed, reason) = SqlGuard.Validate(sql);
        Assert.True(allowed, reason);
    }

    [Theory]
    [InlineData("INSERT INTO PFUNC (NOME) VALUES ('teste')", "INSERT")]
    [InlineData("UPDATE PFUNC SET NOME = 'x'", "UPDATE")]
    [InlineData("DELETE FROM PFUNC", "DELETE")]
    [InlineData("DROP TABLE PFUNC", "DROP")]
    [InlineData("TRUNCATE TABLE PFUNC", "TRUNCATE")]
    [InlineData("EXEC sp_help", "EXEC")]
    [InlineData("EXECUTE sp_who2", "EXECUTE")]
    [InlineData("CREATE TABLE test (id INT)", "CREATE")]
    [InlineData("ALTER TABLE PFUNC ADD col INT", "ALTER")]
    [InlineData("MERGE INTO PFUNC USING src ON 1=1 WHEN MATCHED THEN UPDATE SET x=1", "MERGE")]
    public void Blocks_Write_And_DDL(string sql, string expectedKeyword)
    {
        var (allowed, reason) = SqlGuard.Validate(sql);
        Assert.False(allowed);
        Assert.Contains(expectedKeyword, reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Blocks_Select_Into()
    {
        var (allowed, reason) = SqlGuard.Validate("SELECT * INTO #temp FROM PFUNC");
        Assert.False(allowed);
        Assert.Contains("INTO", reason!);
    }

    [Fact]
    public void Blocks_Semicolon_Injection()
    {
        var (allowed, reason) = SqlGuard.Validate("SELECT 1; DROP TABLE PFUNC");
        Assert.False(allowed);
        Assert.Contains(";", reason!);
    }

    [Fact]
    public void Blocks_Comment_Injection()
    {
        var sql = "SELECT 1 --\nDROP TABLE PFUNC";
        var (allowed, reason) = SqlGuard.Validate(sql);
        Assert.False(allowed);
        Assert.Contains("DROP", reason!);
    }

    [Fact]
    public void Allows_Keyword_Inside_String_Literal()
    {
        var (allowed, reason) = SqlGuard.Validate(
            "SELECT * FROM PFUNC WHERE NOME = 'DELETE ME'");
        Assert.True(allowed, reason);
    }

    [Fact]
    public void Allows_Keyword_Inside_Bracket_Identifier()
    {
        var (allowed, reason) = SqlGuard.Validate(
            "SELECT [DELETE], [INSERT] FROM MinhaTabelaComColunasMalNomeadas");
        Assert.True(allowed, reason);
    }

    [Fact]
    public void Blocks_Empty_Sql()
    {
        var (allowed, _) = SqlGuard.Validate("");
        Assert.False(allowed);

        (allowed, _) = SqlGuard.Validate("   ");
        Assert.False(allowed);
    }

    [Fact]
    public void Blocks_Comment_Only_Sql()
    {
        var (allowed, _) = SqlGuard.Validate("-- apenas um comentário");
        Assert.False(allowed);
    }

    [Fact]
    public void Blocks_Block_Comment_Only_Sql()
    {
        var (allowed, _) = SqlGuard.Validate("/* bloco */");
        Assert.False(allowed);
    }

    [Fact]
    public void Blocks_Openrowset()
    {
        var (allowed, reason) = SqlGuard.Validate(
            "SELECT * FROM OPENROWSET('SQLNCLI','Server=hack;','SELECT 1')");
        Assert.False(allowed);
        Assert.Contains("OPENROWSET", reason!);
    }

    [Fact]
    public void Allows_Keyword_Inside_Double_Quoted_Identifier()
    {
        var (allowed, reason) = SqlGuard.Validate(
            "SELECT \"DELETE\" FROM tabela");
        Assert.True(allowed, reason);
    }
}
