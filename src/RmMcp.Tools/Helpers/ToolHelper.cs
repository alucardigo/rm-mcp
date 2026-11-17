using System.Text.Json;
using RmMcp.Core.Config;
using RmMcp.Sql;
using RmMcp.Soap;

namespace RmMcp.Tools.Helpers;

internal static class ToolHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    internal static RmConfig LoadConfig(string env) =>
        RmConfig.Load(RmEnvironmentParser.Parse(env));

    internal static SqlQueryExecutor CreateSql(RmConfig config) =>
        new(config.SqlConnectionString);

    internal static RmSoapClient CreateSoap(RmConfig config) =>
        new(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    internal static RmRestClient CreateRest(RmConfig config) =>
        new(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    internal static RmWcfClient CreateWcf(RmConfig config) =>
        new(config.WcfHost, config.WcfPort);

    internal static string ToJson(object result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    internal static string FormatResult(QueryResult result) =>
        JsonSerializer.Serialize(new
        {
            columns = result.Columns,
            rowCount = result.RowCount,
            truncated = result.Truncated,
            rows = result.Rows,
        }, JsonOptions);
}
