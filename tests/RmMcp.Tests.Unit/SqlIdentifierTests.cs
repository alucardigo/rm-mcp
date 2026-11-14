using RmMcp.Sql;

namespace RmMcp.Tests.Unit;

public class SqlIdentifierTests
{
    [Theory]
    [InlineData("PFUNC")]
    [InlineData("dbo.PFUNC")]
    [InlineData("PPESSOA")]
    [InlineData("ZMIP_CUSTOM")]
    [InlineData("#temp")]
    public void Accepts_Valid_Names(string name)
    {
        var result = SqlIdentifier.Validate(name);
        Assert.Equal(name, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1TABLE")]
    [InlineData("DROP TABLE x; --")]
    [InlineData("table name")]
    [InlineData("tab;le")]
    [InlineData("a.b.c")]
    public void Rejects_Invalid_Names(string name)
    {
        Assert.Throws<ArgumentException>(() => SqlIdentifier.Validate(name));
    }

    [Theory]
    [InlineData("PFUNC", "[PFUNC]")]
    [InlineData("dbo.PFUNC", "[dbo].[PFUNC]")]
    [InlineData("ZMIP_CUSTOM", "[ZMIP_CUSTOM]")]
    public void Brackets_Names_Correctly(string input, string expected)
    {
        Assert.Equal(expected, SqlIdentifier.Bracket(input));
    }
}
