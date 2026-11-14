using System.Xml.Linq;

namespace RmMcp.Soap;

public static class SoapResponseParser
{
    private static readonly XNamespace Tot = "http://www.totvs.com/";

    public static string ExtractResult(string soapXml, string operation)
    {
        var doc = XDocument.Parse(soapXml);
        var resultElement = doc.Descendants(Tot + $"{operation}Result").FirstOrDefault();
        return resultElement?.Value
            ?? throw new InvalidOperationException(
                $"Elemento '{operation}Result' não encontrado na resposta SOAP");
    }

    public static List<Dictionary<string, string?>> ParseDataXml(string dataXml)
    {
        if (string.IsNullOrWhiteSpace(dataXml))
            return [];

        var doc = XDocument.Parse(dataXml);
        var root = doc.Root;
        if (root == null) return [];

        return root.Elements()
            .Select(record => record.Elements()
                .ToDictionary(
                    e => e.Name.LocalName,
                    e => string.IsNullOrEmpty(e.Value) ? null : e.Value))
            .ToList();
    }
}
