using System.IO.Compression;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Xml;
using System.Xml.Linq;

namespace RmMcp.Soap;

public sealed class RmWcfClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;
    private string? _principalXml;
    private bool? _securityEnabled;
    private bool? _compressionEnabled;

    public bool IsAuthenticated => _principalXml != null;
    public string? PrincipalXml => _principalXml;

    public RmWcfClient(string host, int port = 8050, TimeSpan? timeout = null)
    {
        _host = host;
        _port = port;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<(bool enabled, string? rawXml)> IsSecurityEnabledAsync(CancellationToken ct = default)
    {
        var result = await CallServiceAsync(
            "RMSBrokerServer", "IRMSBrokerServer",
            "http://www.totvs.com/IRMSBrokerServer/IsSecurityEnabled",
            "<IsSecurityEnabled xmlns=\"http://www.totvs.com/\"/>", ct);

        if (result != null)
        {
            var el = FindElement(result, "IsSecurityEnabledResult");
            if (el != null && bool.TryParse(el.Value, out var enabled))
            {
                _securityEnabled = enabled;
                return (enabled, result);
            }
        }
        _securityEnabled = false;
        return (false, result);
    }

    public async Task<(bool enabled, string? rawXml)> IsCompressionEnabledAsync(CancellationToken ct = default)
    {
        var result = await CallServiceAsync(
            "RMSBrokerServer", "IRMSBrokerServer",
            "http://www.totvs.com/IRMSBrokerServer/IsCompressionEnabled",
            "<IsCompressionEnabled xmlns=\"http://www.totvs.com/\"/>", ct);

        if (result != null)
        {
            var el = FindElement(result, "IsCompressionEnabledResult");
            if (el != null && bool.TryParse(el.Value, out var enabled))
            {
                _compressionEnabled = enabled;
                return (enabled, result);
            }
        }
        _compressionEnabled = false;
        return (false, result);
    }

    public async Task<(bool success, string? errorCode, string? rawXml)> ExecuteByPassAsync(
        string alias, string userName, CancellationToken ct = default)
    {
        var xml = $"""
            <ExecuteByPass xmlns="http://www.totvs.com/">
              <alias>{SecurityElement.Escape(alias)}</alias>
              <userName>{SecurityElement.Escape(userName)}</userName>
            </ExecuteByPass>
            """;

        string? errorCode = null;
        bool success = false;

        var result = await CallServiceAsync(
            "RMSLoginServer", "IRMSLoginServerByPass",
            "http://www.totvs.com/IRMSLoginServerByPass/ExecuteByPass",
            xml, ct,
            response =>
            {
                var errEl = FindElement(response, "errorCode");
                errorCode = errEl?.Value?.Trim();

                var principal = FindElement(response, "ExecuteByPassResult");
                if (principal != null &&
                    principal.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"))?.Value != "true")
                {
                    _principalXml = principal.ToString();
                    success = true;
                }
            });

        return (success, errorCode, result);
    }

    public Task<string?> ReadViewAsync(
        string serverName, int codColigada, string codSistema,
        string? filter = null, int codFilial = 1, CancellationToken ct = default)
    {
        var contextXml = BuildRmsContextXml(codColigada, codFilial, codSistema);
        var filterXml = filter != null
            ? $"""
              <filter xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/"
                      z:Id="2" z:Type="System.Object[]"
                      z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
                <anyType z:Id="3" z:Type="System.String"
                         z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">{SecurityElement.Escape(filter)}</anyType>
              </filter>
              """
            : "<filter xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" i:nil=\"true\"/>";

        var xml = $"""
            <ReadView xmlns="http://www.totvs.com/">
              {contextXml}
              {filterXml}
              <ownerData xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:nil="true"/>
            </ReadView>
            """;
        return CallServiceAsync(serverName, "IRMSDataServer",
            "http://www.totvs.com/IRMSDataServer/ReadView", xml, ct);
    }

    public Task<string?> ReadRecordAsync(
        string serverName, int codColigada, string codSistema,
        string primaryKey, int codFilial = 1, CancellationToken ct = default)
    {
        var contextXml = BuildRmsContextXml(codColigada, codFilial, codSistema);
        var xml = $"""
            <ReadRecord xmlns="http://www.totvs.com/">
              {contextXml}
              <primaryKey xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/"
                          z:Id="2" z:Type="System.Object[]"
                          z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
                <anyType z:Id="3" z:Type="System.String"
                         z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">{SecurityElement.Escape(primaryKey)}</anyType>
              </primaryKey>
              <ownerData xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:nil="true"/>
            </ReadRecord>
            """;
        return CallServiceAsync(serverName, "IRMSDataServer",
            "http://www.totvs.com/IRMSDataServer/ReadRecord", xml, ct);
    }

    public Task<string?> ReadSchemaAsync(
        string serverName, int codColigada, string codSistema,
        int codFilial = 1, CancellationToken ct = default)
    {
        var contextXml = BuildRmsContextXml(codColigada, codFilial, codSistema);
        var xml = $"""
            <ReadSchema xmlns="http://www.totvs.com/">
              {contextXml}
            </ReadSchema>
            """;
        return CallServiceAsync(serverName, "IRMSDataServer",
            "http://www.totvs.com/IRMSDataServer/ReadSchema", xml, ct);
    }

    public Task<string?> SaveRecordAsync(
        string serverName, int codColigada, string codSistema,
        string dataSetXml, int codFilial = 1, CancellationToken ct = default)
    {
        var contextXml = BuildRmsContextXml(codColigada, codFilial, codSistema);
        var xml = $"""
            <SaveRecord xmlns="http://www.totvs.com/">
              {contextXml}
              <dataXML>{SecurityElement.Escape(dataSetXml)}</dataXML>
              <ownerData xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:nil="true"/>
            </SaveRecord>
            """;
        return CallServiceAsync(serverName, "IRMSDataServer",
            "http://www.totvs.com/IRMSDataServer/SaveRecord", xml, ct);
    }

    public Task<string?> DeleteRecordAsync(
        string serverName, int codColigada, string codSistema,
        string primaryKey, int codFilial = 1, CancellationToken ct = default)
    {
        var contextXml = BuildRmsContextXml(codColigada, codFilial, codSistema);
        var xml = $"""
            <DeleteRecord xmlns="http://www.totvs.com/">
              {contextXml}
              <primaryKey xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/"
                          z:Id="2" z:Type="System.Object[]"
                          z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">
                <anyType z:Id="3" z:Type="System.String"
                         z:Assembly="mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089">{SecurityElement.Escape(primaryKey)}</anyType>
              </primaryKey>
            </DeleteRecord>
            """;
        return CallServiceAsync(serverName, "IRMSDataServer",
            "http://www.totvs.com/IRMSDataServer/DeleteRecord", xml, ct);
    }

    public Task<string?> GetAliasAsync(CancellationToken ct = default)
    {
        return CallServiceAsync("RMSLoginServer", "IRMSLoginServer",
            "http://www.totvs.com/IRMSLoginServer/GetAlias",
            "<GetAlias xmlns=\"http://www.totvs.com/\"/>", ct);
    }

    public Task<string?> GetSystemVersionAsync(CancellationToken ct = default)
    {
        return CallServiceAsync("RMSLoginServer", "IRMSLoginServer",
            "http://www.totvs.com/IRMSLoginServer/GetSystemVersion",
            "<GetSystemVersion xmlns=\"http://www.totvs.com/\"/>", ct);
    }

    public Task<string?> CheckServiceActivityAsync(CancellationToken ct = default)
    {
        return CallServiceAsync("RMSLoginServer", "IRMSServer",
            "http://www.totvs.com/IRMSServer/CheckServiceActivity",
            "<CheckServiceActivity xmlns=\"http://www.totvs.com/\"/>", ct);
    }

    public Task<string?> CallEndpointAsync(
        string typeId, string serviceName, string soapAction, string bodyXml,
        CancellationToken ct = default) =>
        CallServiceAsync(typeId, serviceName, soapAction, bodyXml, ct);

    private async Task<string?> CallServiceAsync(
        string typeId, string serviceName, string soapAction, string bodyXml,
        CancellationToken ct, Action<string>? onSuccess = null)
    {
        var binding = CreateBinding(typeId);
        var endpoint = new EndpointAddress($"net.tcp://{_host}:{_port}/{typeId}/{serviceName}");

        using var factory = new ChannelFactory<IRmWcfService>(binding, endpoint);

        if (UseTls(typeId))
        {
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication =
                new X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = X509CertificateValidationMode.None
                };
        }

        var channel = factory.CreateChannel();

        var request = Message.CreateMessage(
            binding.MessageVersion,
            soapAction,
            new RawBodyWriter(bodyXml));

        if (_principalXml != null)
        {
            request.Headers.Add(new RawXmlHeader(
                "CorporePrincipal", "http://www.totvs.com/", _principalXml));
        }

        var response = await Task.Run(() => channel.ProcessMessage(request), ct);
        var responseXml = ReadMessageBody(response);

        onSuccess?.Invoke(responseXml);
        ((IClientChannel)channel).Close();

        return responseXml;
    }

    private static string BuildRmsContextXml(int codColigada, int codFilial, string codSistema)
    {
        return $"""
            <context xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/"
                     z:Id="1" z:Type="RM.Lib.RMSContext"
                     z:Assembly="RM.Lib, Version=12.1.2506.1, Culture=neutral, PublicKeyToken=null">
              <_params xmlns="http://schemas.datacontract.org/2004/07/RM.Lib"
                       xmlns:a="http://schemas.microsoft.com/2003/10/Serialization/Arrays">
                <a:KeyValueOfanyTypeanyType>
                  <a:Key z:Type="System.String" z:Assembly="0">CodColigada</a:Key>
                  <a:Value z:Type="System.Int32" z:Assembly="0">{codColigada}</a:Value>
                </a:KeyValueOfanyTypeanyType>
                <a:KeyValueOfanyTypeanyType>
                  <a:Key z:Type="System.String" z:Assembly="0">CodFilial</a:Key>
                  <a:Value z:Type="System.Int32" z:Assembly="0">{codFilial}</a:Value>
                </a:KeyValueOfanyTypeanyType>
                <a:KeyValueOfanyTypeanyType>
                  <a:Key z:Type="System.String" z:Assembly="0">CodSistema</a:Key>
                  <a:Value z:Type="System.String" z:Assembly="0">{codSistema}</a:Value>
                </a:KeyValueOfanyTypeanyType>
              </_params>
              <Environment xmlns="http://schemas.datacontract.org/2004/07/RM.Lib"
                           xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:nil="true"/>
            </context>
            """;
    }

    private static bool IsBroker(string typeId) =>
        typeId.Contains("Broker", StringComparison.OrdinalIgnoreCase);

    private bool UseTls(string typeId) =>
        !IsBroker(typeId) && (_securityEnabled ?? false);

    private bool UseCompression(string typeId) =>
        !IsBroker(typeId) && (_compressionEnabled ?? false);

    private Binding CreateBinding(string typeId)
    {
        if (IsBroker(typeId) || !UseCompression(typeId))
        {
            var tcp = new NetTcpBinding
            {
                OpenTimeout = _timeout,
                SendTimeout = _timeout,
                ReceiveTimeout = _timeout,
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                TransferMode = TransferMode.Streamed,
            };
            tcp.ReliableSession.Enabled = false;

            if (IsBroker(typeId))
                tcp.Security.Mode = SecurityMode.None;
            else if (_securityEnabled == true)
            {
                tcp.Security.Mode = SecurityMode.Transport;
                tcp.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;
            }
            else
                tcp.Security.Mode = SecurityMode.None;

            return tcp;
        }

        var binaryEncoding = new BinaryMessageEncodingBindingElement();
        binaryEncoding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
        var gzipEncoding = new RmGZipEncodingBindingElement(binaryEncoding);

        var elements = new List<BindingElement> { gzipEncoding };

        if (_securityEnabled == true)
            elements.Add(new SslStreamSecurityBindingElement());

        elements.Add(new TcpTransportBindingElement
        {
            MaxBufferSize = int.MaxValue,
            MaxReceivedMessageSize = int.MaxValue,
            TransferMode = TransferMode.Streamed,
        });

        return new CustomBinding(elements.ToArray())
        {
            OpenTimeout = _timeout,
            SendTimeout = _timeout,
            ReceiveTimeout = _timeout,
        };
    }

    private static XElement? FindElement(string xml, string localName)
    {
        try
        {
            var doc = XDocument.Parse($"<root>{xml}</root>");
            return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        }
        catch { return null; }
    }

    private static string ReadMessageBody(Message message)
    {
        using var reader = message.GetReaderAtBodyContents();
        return reader.ReadOuterXml();
    }

    public void Dispose() { }
}

[ServiceContract(Namespace = "http://www.totvs.com/")]
public interface IRmWcfService
{
    [OperationContract(Action = "*", ReplyAction = "*")]
    Message ProcessMessage(Message request);
}

internal sealed class RawBodyWriter : BodyWriter
{
    private readonly string _xml;
    public RawBodyWriter(string xml) : base(false) => _xml = xml;

    protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
    {
        var doc = XDocument.Parse(_xml);
        doc.Root!.WriteTo(writer);
    }
}

internal sealed class RawXmlHeader : MessageHeader
{
    private readonly string _name;
    private readonly string _ns;
    private readonly string _innerXml;

    public RawXmlHeader(string name, string ns, string outerXml)
    {
        _name = name;
        _ns = ns;
        var el = XElement.Parse(outerXml);
        _innerXml = string.Concat(el.Nodes().Select(n => n.ToString()));
    }

    public override string Name => _name;
    public override string Namespace => _ns;

    protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
    {
        var wrapper = $"<_root xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\">{_innerXml}</_root>";
        var doc = XDocument.Parse(wrapper);
        foreach (var node in doc.Root!.Nodes())
        {
            if (node is XElement child)
                child.WriteTo(writer);
        }
    }
}

internal sealed class RmGZipEncodingBindingElement : MessageEncodingBindingElement
{
    private readonly MessageEncodingBindingElement _inner;

    public RmGZipEncodingBindingElement(MessageEncodingBindingElement inner) => _inner = inner;

    public override MessageVersion MessageVersion
    {
        get => _inner.MessageVersion;
        set => _inner.MessageVersion = value;
    }

    public override MessageEncoderFactory CreateMessageEncoderFactory() =>
        new RmGZipEncoderFactory(_inner.CreateMessageEncoderFactory());

    public override BindingElement Clone() => new RmGZipEncodingBindingElement(_inner);

    public override T GetProperty<T>(BindingContext context) =>
        typeof(T) == typeof(XmlDictionaryReaderQuotas)
            ? _inner.GetProperty<T>(context)
            : base.GetProperty<T>(context);

    public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
    {
        context.BindingParameters.Add(this);
        return context.BuildInnerChannelFactory<TChannel>();
    }
}

internal sealed class RmGZipEncoderFactory : MessageEncoderFactory
{
    private readonly MessageEncoder _encoder;
    public RmGZipEncoderFactory(MessageEncoderFactory inner) =>
        _encoder = new RmGZipMessageEncoder(inner.Encoder);
    public override MessageEncoder Encoder => _encoder;
    public override MessageVersion MessageVersion => _encoder.MessageVersion;
}

internal sealed class RmGZipMessageEncoder : MessageEncoder
{
    private readonly MessageEncoder _inner;
    public RmGZipMessageEncoder(MessageEncoder inner) => _inner = inner;
    public override string ContentType => "application/x-gzip";
    public override string MediaType => "application/x-gzip";
    public override MessageVersion MessageVersion => _inner.MessageVersion;

    public override bool IsContentTypeSupported(string contentType) =>
        contentType == "application/x-gzip" || _inner.IsContentTypeSupported(contentType);

    public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
    {
        using var compressed = new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzip.CopyTo(decompressed);
        var bytes = decompressed.ToArray();
        var buf = bufferManager.TakeBuffer(bytes.Length);
        Array.Copy(bytes, buf, bytes.Length);
        bufferManager.ReturnBuffer(buffer.Array!);
        return _inner.ReadMessage(new ArraySegment<byte>(buf, 0, bytes.Length), bufferManager);
    }

    public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize,
        BufferManager bufferManager, int messageOffset)
    {
        var innerBytes = _inner.WriteMessage(message, maxMessageSize, bufferManager, 0);
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(innerBytes.Array!, innerBytes.Offset, innerBytes.Count);
        var result = compressed.ToArray();
        var outBuf = bufferManager.TakeBuffer(result.Length + messageOffset);
        Array.Copy(result, 0, outBuf, messageOffset, result.Length);
        bufferManager.ReturnBuffer(innerBytes.Array!);
        return new ArraySegment<byte>(outBuf, messageOffset, result.Length);
    }

    public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
    {
        var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false);
        return _inner.ReadMessage(gzip, maxSizeOfHeaders);
    }

    public override void WriteMessage(Message message, Stream stream)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
        _inner.WriteMessage(message, gzip);
        gzip.Flush();
        stream.Flush();
    }
}
