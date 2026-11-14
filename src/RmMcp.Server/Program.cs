using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RmMcp.Tools.Meta;

if (args.Contains("--list"))
{
    PrintTools();
    return;
}

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

static void PrintTools()
{
    Console.WriteLine("rm-mcp v0.1.0 — MCP server para RM TOTVS");
    Console.WriteLine();

    var assembly = typeof(RmPingTool).Assembly;
    var toolTypes = assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .OrderBy(t => t.Name);

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
        }
    }

    Console.WriteLine();
    Console.WriteLine("Uso: dotnet rm-mcp.dll              (servidor MCP stdio)");
    Console.WriteLine("     dotnet rm-mcp.dll --list        (lista ferramentas)");
}
