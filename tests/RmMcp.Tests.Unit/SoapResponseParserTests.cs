using RmMcp.Soap;

namespace RmMcp.Tests.Unit;

public class SoapResponseParserTests
{
    [Fact]
    public void Extracts_Result_From_Soap_Response()
    {
        const string soap = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body>
                <GetSchemaResponse xmlns="http://www.totvs.com/">
                  <GetSchemaResult>&lt;xs:schema /&gt;</GetSchemaResult>
                </GetSchemaResponse>
              </s:Body>
            </s:Envelope>
            """;

        var result = SoapResponseParser.ExtractResult(soap, "GetSchema");
        Assert.Equal("<xs:schema />", result);
    }

    [Fact]
    public void Throws_When_Result_Not_Found()
    {
        const string soap = """
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body>
                <SomethingElseResponse xmlns="http://www.totvs.com/">
                  <SomethingElseResult>data</SomethingElseResult>
                </SomethingElseResponse>
              </s:Body>
            </s:Envelope>
            """;

        Assert.Throws<InvalidOperationException>(
            () => SoapResponseParser.ExtractResult(soap, "GetSchema"));
    }

    [Fact]
    public void Parses_Data_Xml_To_Records()
    {
        const string xml = """
            <GlbColigadaData>
              <GColigada>
                <CODCOLIGADA>1</CODCOLIGADA>
                <NOMEFANTASIA>MIP Engenharia</NOMEFANTASIA>
              </GColigada>
              <GColigada>
                <CODCOLIGADA>2</CODCOLIGADA>
                <NOMEFANTASIA>Outra Coligada</NOMEFANTASIA>
              </GColigada>
            </GlbColigadaData>
            """;

        var records = SoapResponseParser.ParseDataXml(xml);

        Assert.Equal(2, records.Count);
        Assert.Equal("1", records[0]["CODCOLIGADA"]);
        Assert.Equal("MIP Engenharia", records[0]["NOMEFANTASIA"]);
        Assert.Equal("2", records[1]["CODCOLIGADA"]);
    }

    [Fact]
    public void Returns_Empty_For_Empty_Xml()
    {
        Assert.Empty(SoapResponseParser.ParseDataXml(""));
        Assert.Empty(SoapResponseParser.ParseDataXml("  "));
    }

    [Fact]
    public void Handles_Null_Values_In_Data()
    {
        const string xml = """
            <Data>
              <Row>
                <NAME>test</NAME>
                <VALUE></VALUE>
              </Row>
            </Data>
            """;

        var records = SoapResponseParser.ParseDataXml(xml);

        Assert.Single(records);
        Assert.Equal("test", records[0]["NAME"]);
        Assert.Null(records[0]["VALUE"]);
    }
}
