using System.Xml.Linq;

namespace RmMcp.Soap;

public static class SoapEnvelopeBuilder
{
    private static readonly XNamespace Soapenv =
        "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Tot =
        "http://www.totvs.com/";

    public static string Build(string operation, params SoapParam[] parameters)
    {
        var operationElement = new XElement(Tot + operation,
            parameters.Select(p =>
                new XElement(Tot + p.Name,
                    p.IsCdata ? (object)new XCData(p.Value) : p.Value)));

        var envelope = new XElement(Soapenv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soapenv.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tot", Tot.NamespaceName),
            new XElement(Soapenv + "Header"),
            new XElement(Soapenv + "Body", operationElement));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    public static string DataServerAction(string operation) =>
        $"http://www.totvs.com/IwsDataServer/{operation}";

    public static string ProcessAction(string operation) =>
        $"http://www.totvs.com/IwsProcess/{operation}";

    public static string LoginAction(string operation) =>
        $"http://www.totvs.com/IwsLogin/{operation}";

    public const string DataServerEndpoint = "wsDataServer/IwsDataServer";
    public const string ProcessEndpoint = "wsProcess/IwsProcess";
    public const string LoginEndpoint = "wsLogin/IwsLogin";
    public const string ConsultaSqlEndpoint = "wsConsultaSQL/IwsConsultaSQL";

    public static string ConsultaSqlAction(string operation) =>
        $"http://www.totvs.com/IwsConsultaSQL/{operation}";
}

public readonly record struct SoapParam(string Name, string Value, bool IsCdata = false);
