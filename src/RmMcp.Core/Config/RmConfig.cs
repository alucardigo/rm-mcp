namespace RmMcp.Core.Config;

public sealed class RmConfig
{
    public required string SqlConnectionString { get; init; }
    public required string SoapBaseUrl { get; init; }
    public required string SoapUsername { get; init; }
    public required string SoapPassword { get; init; }
    public required RmEnvironment Environment { get; init; }

    public static RmConfig Load(RmEnvironment env)
    {
        var suffix = env == RmEnvironment.Producao ? "_PROD" : "";

        return new RmConfig
        {
            Environment = env,
            SqlConnectionString = RequireEnv($"RM_SQL_CONNECTION{suffix}"),
            SoapBaseUrl = RequireEnv($"RM_SOAP_URL{suffix}").TrimEnd('/'),
            SoapUsername = RequireEnv($"RM_SOAP_USER{suffix}"),
            SoapPassword = RequireEnv($"RM_SOAP_PASS{suffix}"),
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
}
