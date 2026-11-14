using RmMcp.Soap;

namespace RmMcp.Tests.Integration;

/// <summary>
/// Integration tests for the SOAP client against the Docker mock RM.Host.Service.
/// Verifies envelope building, auth, parsing, and fault handling.
/// </summary>
[Collection("Docker")]
public class SoapClientTests
{
    private readonly DockerFixture _docker;

    public SoapClientTests(DockerFixture docker) => _docker = docker;

    [Fact]
    public async Task Ping_ReturnsSchemaXml()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var response = await client.PingAsync();

        Assert.Contains("GetSchemaResult", response);
        Assert.Contains("GCOLIGADA", response);
    }

    [Fact]
    public async Task GetSchema_GlbColigadaData_ContainsColigadaFields()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var envelope = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", "GlbColigadaData"));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetSchema"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "GetSchema");
        Assert.NotNull(result);
        Assert.Contains("CODCOLIGADA", result);
        Assert.Contains("NOME", result);
        Assert.Contains("NOMEFANTASIA", result);
    }

    [Fact]
    public async Task ReadView_GlbColigadaData_ReturnsMipData()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var envelope = SoapEnvelopeBuilder.Build("ReadView",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("Filtro", ""),
            new SoapParam("Contexto", "<RMSContexto><CodColigada>1</CodColigada></RMSContexto>"));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadView"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ReadView");
        Assert.NotNull(result);
        Assert.Contains("MIP Engenharia", result);
        Assert.Contains("CODCOLIGADA", result);
    }

    [Fact]
    public async Task ReadRecord_ReturnsExpectedRecord()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var envelope = SoapEnvelopeBuilder.Build("ReadRecord",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("PrimaryKey", "1"),
            new SoapParam("Contexto", "<RMSContexto><CodColigada>1</CodColigada></RMSContexto>"));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("ReadRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ReadRecord");
        Assert.NotNull(result);
        Assert.Contains("MIP Engenharia", result);
    }

    [Fact]
    public async Task SaveRecord_DryRun_ReturnsOne()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var envelope = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", "GlbColigadaData"),
            new SoapParam("XML", "<test/>", IsCdata: true),
            new SoapParam("Contexto", "<RMSContexto><CodColigada>1</CodColigada></RMSContexto>"));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("SaveRecord"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "SaveRecord");
        Assert.Equal("1", result);
    }

    [Fact]
    public async Task ExecuteProcess_ReturnsJobResult()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var envelope = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "EstAprovarMov"),
            new SoapParam("strXmlParams", "<ProcessParams/>", IsCdata: true));

        var response = await client.SendAsync(
            SoapEnvelopeBuilder.ProcessEndpoint,
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"),
            envelope);

        var result = SoapResponseParser.ExtractResult(response, "ExecuteWithXmlParams");
        Assert.NotNull(result);
        Assert.Contains("Completed", result);
        Assert.Contains("AP-001", result);
    }

    [Fact]
    public async Task Get_DataServerList_ContainsKnownNames()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, _docker.SoapUser, _docker.SoapPass);
        var list = await client.GetAsync("RMSRestDataServer");

        Assert.Contains("GlbColigadaData", list);
        Assert.Contains("RhuFuncDados", list);
        Assert.Contains("EstMovimentoData", list);
        Assert.Contains("FinLanData", list);
    }

    [Fact]
    public async Task BadCredentials_Throws401()
    {
        using var client = new RmSoapClient(_docker.SoapBaseUrl, "wrong", "creds");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PingAsync());
    }
}
