using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace RmMcp.Soap;

public sealed class RmSoapClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public RmSoapClient(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<string> SendAsync(string endpoint, string soapAction,
        string envelope, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{_baseUrl}/{endpoint}");
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        request.Headers.Add("SOAPAction", $"\"{soapAction}\"");

        using var response = await _http.SendAsync(request, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = TryExtractFault(body);
            throw new HttpRequestException(
                $"SOAP {(int)response.StatusCode}: {detail ?? response.ReasonPhrase}");
        }

        return body;
    }

    public async Task<string> PingAsync(CancellationToken ct = default)
    {
        var envelope = SoapEnvelopeBuilder.Build("GetSchema",
            new SoapParam("DataServerName", "GlbColigadaData"));

        return await SendAsync(
            SoapEnvelopeBuilder.DataServerEndpoint,
            SoapEnvelopeBuilder.DataServerAction("GetSchema"),
            envelope, ct);
    }

    public async Task<string> GetAsync(string path, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/{path}");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string? TryExtractFault(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";
            var fault = doc.Descendants(soapenv + "Fault").FirstOrDefault();
            var faultString = fault?.Element("faultstring")?.Value;
            return faultString;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
