using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RmMcp.Core.Config;
using RmMcp.Soap;
using RmMcp.Sql;
using RmMcp.Tools.Meta;

if (args.Length == 0)
{
    await RunMcpServer(args);
    return;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "--list":
        PrintTools();
        return;

    case "--help" or "-h" or "help":
        PrintUsage();
        return;

    case "--version" or "-v":
        Console.WriteLine("rm-mcp v0.1.0");
        return;

    case "query":
        await RunQuery(args[1..]);
        return;

    case "dataserver" or "ds":
        await RunDataServer(args[1..]);
        return;

    case "list-dataservers":
        await RunListDataServers(args[1..]);
        return;

    case "ping":
        await RunPing(args[1..]);
        return;

    default:
        Console.Error.WriteLine($"Comando desconhecido: {command}");
        Console.Error.WriteLine("Use 'dotnet rm-mcp.dll --help' para ver os comandos.");
        Environment.Exit(1);
        return;
}

// ─── MCP stdio server ──────────────────────────────────────────────

static async Task RunMcpServer(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "rm-mcp",
                Version = "0.1.0",
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(RmPingTool).Assembly);

    await builder.Build().RunAsync();
}

// ─── CLI: query ─────────────────────────────────────────────────────

static async Task RunQuery(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Uso: rm-mcp query <SQL> [--env teste|producao] [--timeout 30] [--max-rows 1000]");
        Environment.Exit(1);
    }

    var (env, remaining) = ExtractOption(args, "--env", "teste");
    var (timeout, remaining2) = ExtractOption(remaining, "--timeout", "30");
    var (maxRows, remaining3) = ExtractOption(remaining2, "--max-rows", "1000");
    var sql = string.Join(" ", remaining3);

    if (string.IsNullOrWhiteSpace(sql))
    {
        Console.Error.WriteLine("SQL não informado.");
        Environment.Exit(1);
    }

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var executor = new SqlQueryExecutor(config.SqlConnectionString);

    var result = await executor.ExecuteAsync(
        sql,
        timeoutSeconds: int.Parse(timeout),
        maxRows: int.Parse(maxRows));

    PrintQueryResult(result);
}

// ─── CLI: dataserver ────────────────────────────────────────────────

static async Task RunDataServer(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("""
            Uso: rm-mcp dataserver <subcomando> [opções]

            Subcomandos:
              read <DataServer> [--filter <SQL>] [--context <ctx>]   Lê registros via ReadView
              schema <DataServer>                                     Schema XML (XSD)
              count <DataServer> [--filter <SQL>]                     Conta registros
              record <DataServer> <PrimaryKey>                        Lê registro por chave

            Opções globais:
              --env teste|producao   (padrão: teste)
            """);
        Environment.Exit(1);
    }

    var sub = args[0].ToLowerInvariant();

    switch (sub)
    {
        case "read":
            await DataServerRead(args[1..]);
            break;
        case "schema":
            await DataServerSchema(args[1..]);
            break;
        case "count":
            await DataServerCount(args[1..]);
            break;
        case "record":
            await DataServerRecord(args[1..]);
            break;
        default:
            Console.Error.WriteLine($"Subcomando desconhecido: {sub}");
            Environment.Exit(1);
            break;
    }
}

static async Task DataServerRead(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Uso: rm-mcp dataserver read <DataServer> [--filter <SQL>] [--context <ctx>] [--env teste]");
        Environment.Exit(1);
    }

    var dsName = args[0];
    var (env, r1) = ExtractOption(args[1..], "--env", "teste");
    var (filter, r2) = ExtractOption(r1, "--filter", "");
    var (context, _) = ExtractOption(r2, "--context", "");

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var client = new RmSoapClient(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    var parameters = new List<SoapParam>
    {
        new("DataServerName", dsName),
        new("Filtro", filter),
        new("Contexto", context),
    };

    var envelope = SoapEnvelopeBuilder.Build("ReadView", parameters.ToArray());

    var response = await client.SendAsync(
        SoapEnvelopeBuilder.DataServerEndpoint,
        SoapEnvelopeBuilder.DataServerAction("ReadView"),
        envelope);

    var dataXml = SoapResponseParser.ExtractResult(response, "ReadView");
    var records = SoapResponseParser.ParseDataXml(dataXml);

    Console.WriteLine($"DataServer: {dsName}");
    Console.WriteLine($"Registros: {records.Count}");
    Console.WriteLine();
    PrintRecordsAsTable(records);
}

static async Task DataServerSchema(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Uso: rm-mcp dataserver schema <DataServer> [--env teste]");
        Environment.Exit(1);
    }

    var dsName = args[0];
    var (env, _) = ExtractOption(args[1..], "--env", "teste");

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var client = new RmSoapClient(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    var envelope = SoapEnvelopeBuilder.Build("GetSchema",
        new SoapParam("DataServerName", dsName));

    var response = await client.SendAsync(
        SoapEnvelopeBuilder.DataServerEndpoint,
        SoapEnvelopeBuilder.DataServerAction("GetSchema"),
        envelope);

    var schema = SoapResponseParser.ExtractResult(response, "GetSchema");
    Console.WriteLine(schema);
}

static async Task DataServerCount(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Uso: rm-mcp dataserver count <DataServer> [--filter <SQL>] [--env teste]");
        Environment.Exit(1);
    }

    var dsName = args[0];
    var (env, r1) = ExtractOption(args[1..], "--env", "teste");
    var (filter, _) = ExtractOption(r1, "--filter", "");

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var client = new RmSoapClient(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    var envelope = SoapEnvelopeBuilder.Build("GetTotalRecords",
        new SoapParam("DataServerName", dsName),
        new SoapParam("Filtro", filter),
        new SoapParam("Contexto", ""));

    var response = await client.SendAsync(
        SoapEnvelopeBuilder.DataServerEndpoint,
        SoapEnvelopeBuilder.DataServerAction("GetTotalRecords"),
        envelope);

    var result = SoapResponseParser.ExtractResult(response, "GetTotalRecords");
    Console.WriteLine($"DataServer: {dsName}");
    Console.WriteLine($"Total: {result}");
}

static async Task DataServerRecord(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Uso: rm-mcp dataserver record <DataServer> <PrimaryKey> [--env teste]");
        Environment.Exit(1);
    }

    var dsName = args[0];
    var pk = args[1];
    var (env, _) = ExtractOption(args[2..], "--env", "teste");

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var client = new RmSoapClient(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);

    var envelope = SoapEnvelopeBuilder.Build("ReadRecord",
        new SoapParam("DataServerName", dsName),
        new SoapParam("PrimaryKey", pk),
        new SoapParam("Contexto", ""));

    var response = await client.SendAsync(
        SoapEnvelopeBuilder.DataServerEndpoint,
        SoapEnvelopeBuilder.DataServerAction("ReadRecord"),
        envelope);

    var dataXml = SoapResponseParser.ExtractResult(response, "ReadRecord");
    var records = SoapResponseParser.ParseDataXml(dataXml);

    Console.WriteLine($"DataServer: {dsName}  PK: {pk}");
    Console.WriteLine();
    PrintRecordsAsTable(records);
}

// ─── CLI: list-dataservers ──────────────────────────────────────────

static async Task RunListDataServers(string[] args)
{
    var (env, _) = ExtractOption(args, "--env", "teste");

    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));
    using var executor = new SqlQueryExecutor(config.SqlConnectionString);

    var result = await executor.ExecuteAsync(
        "SELECT DISTINCT NOME FROM GDATASERVERNAME ORDER BY NOME",
        maxRows: 500);

    Console.WriteLine($"DataServers disponíveis ({result.RowCount}):");
    Console.WriteLine();

    foreach (var row in result.Rows)
    {
        if (row.TryGetValue("NOME", out var nome))
            Console.WriteLine($"  {nome}");
    }
}

// ─── CLI: ping ──────────────────────────────────────────────────────

static async Task RunPing(string[] args)
{
    var (env, _) = ExtractOption(args, "--env", "teste");
    var config = RmConfig.Load(RmEnvironmentParser.Parse(env));

    Console.Write("SQL... ");
    try
    {
        using var executor = new SqlQueryExecutor(config.SqlConnectionString);
        var r = await executor.ExecuteAsync("SELECT 1 AS ok");
        Console.WriteLine("OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FALHOU: {ex.Message}");
    }

    Console.Write("SOAP... ");
    try
    {
        using var client = new RmSoapClient(config.SoapBaseUrl, config.SoapUsername, config.SoapPassword);
        await client.PingAsync();
        Console.WriteLine("OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FALHOU: {ex.Message}");
    }

    Console.Write("WCF... ");
    try
    {
        using var wcf = new RmWcfClient(config.WcfHost, config.WcfPort);
        await wcf.ExecuteByPassAsync(config.WcfAlias, config.WcfUser);
        Console.WriteLine("OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FALHOU: {ex.Message}");
    }
}

// ─── Helpers ────────────────────────────────────────────────────────

static void PrintTools()
{
    Console.WriteLine("rm-mcp v0.1.0 — MCP server para RM TOTVS");
    Console.WriteLine();

    var assembly = typeof(RmPingTool).Assembly;
    var toolTypes = assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .OrderBy(t => t.Name);

    var count = 0;
    foreach (var type in toolTypes)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .OrderBy(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name ?? m.Name);

        foreach (var method in methods)
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>()!;
            var desc = method.GetCustomAttribute<DescriptionAttribute>();
            var name = toolAttr.Name ?? method.Name;
            var description = desc?.Description?.Split('\n')[0].Trim() ?? "";

            Console.WriteLine($"  {name,-30} {description}");
            count++;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {count} tools");
}

static void PrintUsage()
{
    Console.WriteLine("""
        rm-mcp v0.1.0 — MCP server + CLI para RM TOTVS

        MODO MCP (sem argumentos):
          dotnet rm-mcp.dll                            Inicia servidor MCP via stdio

        MODO CLI:
          dotnet rm-mcp.dll query <SQL>                Executa SELECT no Corpore
          dotnet rm-mcp.dll dataserver read <DS>       Lê registros via SOAP ReadView
          dotnet rm-mcp.dll dataserver schema <DS>     Schema XML de um DataServer
          dotnet rm-mcp.dll dataserver count <DS>      Conta registros
          dotnet rm-mcp.dll dataserver record <DS> <PK>  Lê registro por chave
          dotnet rm-mcp.dll list-dataservers            Lista DataServers disponíveis
          dotnet rm-mcp.dll ping                        Testa conectividade (SQL+SOAP+WCF)

        OPÇÕES GLOBAIS:
          --env teste|producao    Ambiente (padrão: teste)
          --filter <SQL>          Filtro WHERE para dataserver read/count
          --context <ctx>         Contexto chave=valor (ex: CODCOLIGADA=1;CODSISTEMA=P)
          --timeout <seg>         Timeout em segundos (query, padrão: 30)
          --max-rows <n>          Máximo de linhas (query, padrão: 1000)

        OUTROS:
          --list                  Lista todas as MCP tools disponíveis
          --help                  Este texto
          --version               Versão

        VARIÁVEIS DE AMBIENTE (obrigatórias):
          RM_SQL_CONNECTION       Connection string SQL Server (Corpore)
          RM_SOAP_URL             URL base do RM.Host.Service (ex: http://172.16.60.20:8051)
          RM_SOAP_USER            Usuário SOAP
          RM_SOAP_PASS            Senha SOAP

        VARIÁVEIS OPCIONAIS:
          RM_WCF_HOST             Host WCF (padrão: extraído de RM_SOAP_URL)
          RM_WCF_PORT             Porta WCF (padrão: 8050)
          RM_WCF_ALIAS            Alias WCF (padrão: CorporeRM)
          RM_WCF_USER             Usuário WCF (padrão: mestre)

        Para produção, use sufixo _PROD nas variáveis (RM_SQL_CONNECTION_PROD, etc).

        EXEMPLOS:
          dotnet rm-mcp.dll query "SELECT TOP 5 NOMEFANTASIA FROM GCOLIGADA"
          dotnet rm-mcp.dll dataserver read FinCxaData --filter "CODCOLIGADA=1"
          dotnet rm-mcp.dll dataserver schema GlbColigadaData
          dotnet rm-mcp.dll list-dataservers
          dotnet rm-mcp.dll ping --env producao
        """);
}

static (string value, string[] remaining) ExtractOption(string[] args, string option, string defaultValue)
{
    var list = new List<string>();
    string? value = null;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            value = args[i + 1];
            i++;
        }
        else
        {
            list.Add(args[i]);
        }
    }

    return (value ?? defaultValue, list.ToArray());
}

static void PrintRecordsAsTable(List<Dictionary<string, string?>> records)
{
    if (records.Count == 0)
    {
        Console.WriteLine("(nenhum registro)");
        return;
    }

    var columns = records[0].Keys.ToList();
    var widths = columns.ToDictionary(c => c,
        c => Math.Max(c.Length,
            records.Max(r => (r.TryGetValue(c, out var v) ? v?.Length : 0) ?? 0)));

    var header = string.Join(" | ", columns.Select(c => c.PadRight(widths[c])));
    Console.WriteLine(header);
    Console.WriteLine(new string('-', header.Length));

    foreach (var row in records)
    {
        var line = string.Join(" | ", columns.Select(c =>
        {
            var val = row.TryGetValue(c, out var v) ? v ?? "" : "";
            return val.PadRight(widths[c]);
        }));
        Console.WriteLine(line);
    }
}

static void PrintQueryResult(QueryResult result)
{
    if (result.RowCount == 0)
    {
        Console.WriteLine("(nenhum resultado)");
        return;
    }

    var columns = result.Columns;
    var widths = columns.ToDictionary(c => c,
        c => Math.Max(c.Length,
            result.Rows.Count > 0
                ? result.Rows.Max(r => (r.TryGetValue(c, out var v) ? v?.ToString()?.Length : 0) ?? 0)
                : 0));

    var header = string.Join(" | ", columns.Select(c => c.PadRight(widths[c])));
    Console.WriteLine(header);
    Console.WriteLine(new string('-', header.Length));

    foreach (var row in result.Rows)
    {
        var line = string.Join(" | ", columns.Select(c =>
        {
            var val = row.TryGetValue(c, out var v) ? v?.ToString() ?? "" : "";
            return val.PadRight(widths[c]);
        }));
        Console.WriteLine(line);
    }

    Console.WriteLine();
    Console.WriteLine($"({result.RowCount} linha{(result.RowCount != 1 ? "s" : "")}{(result.Truncated ? ", truncado" : "")})");
}
