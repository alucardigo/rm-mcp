namespace RmMcp.Core.Config;

public sealed class RmConfig
{
    public required string SqlConnectionString { get; init; }
    public required string SoapBaseUrl { get; init; }
    public required string SoapUsername { get; init; }
    public required string SoapPassword { get; init; }
    public required RmEnvironment Environment { get; init; }
    public required string WcfHost { get; init; }
    public required int WcfPort { get; init; }
    public required string WcfAlias { get; init; }
    public required string WcfUser { get; init; }

    public static RmConfig Load(RmEnvironment env)
    {
        var suffix = env == RmEnvironment.Producao ? "_PROD" : "";
        var soapUrl = RequireEnv($"RM_SOAP_URL{suffix}").TrimEnd('/');

        return new RmConfig
        {
            Environment = env,
            SqlConnectionString = RequireEnv($"RM_SQL_CONNECTION{suffix}"),
            SoapBaseUrl = soapUrl,
            SoapUsername = RequireEnv($"RM_SOAP_USER{suffix}"),
            SoapPassword = RequireEnv($"RM_SOAP_PASS{suffix}"),
            WcfHost = GetEnv($"RM_WCF_HOST{suffix}") ?? ExtractHost(soapUrl),
            WcfPort = int.TryParse(GetEnv($"RM_WCF_PORT{suffix}"), out var p) ? p : 8050,
            WcfAlias = GetEnv($"RM_WCF_ALIAS{suffix}") ?? "CorporeRM",
            WcfUser = GetEnv($"RM_WCF_USER{suffix}") ?? "mestre",
        };
    }

    private static string RequireEnv(string name)
    {
        var value = System.Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Variável de ambiente obrigatória ausente: {name}");
        return value;
    }

    private static string? GetEnv(string name)
    {
        var value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ExtractHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host;
        return url;
    }
}
