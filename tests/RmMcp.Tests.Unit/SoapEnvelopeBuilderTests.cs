using System.Xml.Linq;
using RmMcp.Soap;

namespace RmMcp.Tests.Unit;

public class SoapEnvelopeBuilderTests
{
    [Fact]
    public void Builds_Valid_Soap_Envelope()
    {
        var xml = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", "GlbColigadaData"));

        var doc = XDocument.Parse(xml);
        XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace tot = "http://www.totvs.com/";

        Assert.NotNull(doc.Root);
        Assert.Equal(soapenv + "Envelope", doc.Root!.Name);
        Assert.NotNull(doc.Root.Element(soapenv + "Header"));

        var body = doc.Root.Element(soapenv + "Body");
        Assert.NotNull(body);

        var operation = body!.Element(tot + "GetSchema");
        Assert.NotNull(operation);

        var param = operation!.Element(tot + "DataServerName");
        Assert.Equal("GlbColigadaData", param!.Value);
    }

    [Fact]
    public void Wraps_Cdata_Parameter()
    {
        var xmlContent = "<root><item>test</item></root>";
        var xml = SoapEnvelopeBuilder.Build("SaveRecord",
            new SoapParam("DataServerName", "FopFuncionarioData"),
            new SoapParam("XML", xmlContent, IsCdata: true));

        Assert.Contains("<![CDATA[", xml);
        Assert.Contains(xmlContent, xml);
    }

    [Fact]
    public void Builds_Process_Envelope()
    {
        var paramsXml = "<Params><JobServerName>test</JobServerName></Params>";
        var xml = SoapEnvelopeBuilder.Build("ExecuteWithXmlParams",
            new SoapParam("ProcessServerName", "GlbTesteProcess"),
            new SoapParam("strXmlParams", paramsXml, IsCdata: true));

        var doc = XDocument.Parse(xml);
        XNamespace tot = "http://www.totvs.com/";

        var operation = doc.Descendants(tot + "ExecuteWithXmlParams").Single();
        Assert.Equal("GlbTesteProcess",
            operation.Element(tot + "ProcessServerName")!.Value);
    }

    [Fact]
    public void DataServerAction_Format()
    {
        Assert.Equal(
            "http://www.totvs.com/IwsDataServer/GetSchema",
            SoapEnvelopeBuilder.DataServerAction("GetSchema"));
    }

    [Fact]
    public void ProcessAction_Format()
    {
        Assert.Equal(
            "http://www.totvs.com/IwsProcess/ExecuteWithXmlParams",
            SoapEnvelopeBuilder.ProcessAction("ExecuteWithXmlParams"));
    }

    [Fact]
    public void Handles_Multiple_Parameters()
    {
        var xml = SoapEnvelopeBuilder.Build("ReadView",
            new SoapParam("DataServerName", "FopFuncionarioData"),
            new SoapParam("Filtro", "CODCOLIGADA = 1"),
            new SoapParam("Contexto", "CODCOLIGADA=1;CODTPMOV=1"));

        var doc = XDocument.Parse(xml);
        XNamespace tot = "http://www.totvs.com/";

        var operation = doc.Descendants(tot + "ReadView").Single();
        Assert.Equal(3, operation.Elements().Count());
        Assert.Equal("FopFuncionarioData",
            operation.Element(tot + "DataServerName")!.Value);
        Assert.Equal("CODCOLIGADA = 1",
            operation.Element(tot + "Filtro")!.Value);
    }
}
