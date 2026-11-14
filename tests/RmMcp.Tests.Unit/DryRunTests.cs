using RmMcp.Core.Config;
using RmMcp.Tools.DataServer;

namespace RmMcp.Tests.Unit;

public class DryRunTests
{
    [Fact]
    public void Returns_Preview_When_Not_Confirmed()
    {
        Assert.Equal("preview",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Teste, null, null));
        Assert.Equal("preview",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Teste, false, null));
        Assert.Equal("preview",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Producao, null, null));
    }

    [Fact]
    public void Executes_In_Teste_With_Confirm()
    {
        Assert.Equal("execute",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Teste, true, null));
    }

    [Fact]
    public void Blocks_Production_Without_Production_Confirmed()
    {
        Assert.Equal("blocked_production",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Producao, true, null));
        Assert.Equal("blocked_production",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Producao, true, false));
    }

    [Fact]
    public void Executes_In_Production_With_Both_Flags()
    {
        Assert.Equal("execute",
            RmDataServerWriteTools.EvaluateAction(RmEnvironment.Producao, true, true));
    }
}
