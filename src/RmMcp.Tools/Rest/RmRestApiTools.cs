using System.ComponentModel;
using ModelContextProtocol.Server;
using RmMcp.Tools.Helpers;

namespace RmMcp.Tools.Rest;

[McpServerToolType]
public static class RmRestApiTools
{
    [McpServerTool(Name = "rm_rest_get")]
    [Description("""
        Executa GET em qualquer endpoint REST do RM. Usa OAuth Bearer token (adquirido automaticamente).
        Endpoints conhecidos: /api/framework/v1/companies, /api/framework/v1/Branches,
        /api/framework/v1/users, /api/rhu/v1/employees, /api/fin/v1/invoices,
        /api/est/v1/movements, /api/cmp/v1/purchaseOrders.
        Use rm_rest_discover para listar endpoints disponíveis.
        """)]
    public static async Task<string> RestGet(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Caminho REST (ex: '/api/framework/v1/companies?pageSize=10')")] string path)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateRest(config);

        var response = await client.GetAsync(path);

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            path,
            resultado = TryParseJson(response),
        });
    }

    [McpServerTool(Name = "rm_rest_post")]
    [Description("""
        Executa POST em endpoint REST do RM (criação de registros via API).
        SECO: sem confirm=true retorna apenas preview.
        """)]
    public static async Task<string> RestPost(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Caminho REST (ex: '/api/framework/v1/companies')")] string path,
        [Description("Corpo JSON do request")] string jsonBody,
        [Description("true para executar — sem isso retorna apenas preview")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = DataServer.RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);

        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                path,
                ambiente = config.Environment.ToString(),
                bodyPreview = jsonBody.Length > 500 ? jsonBody[..500] + "..." : jsonBody,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateRest(config);
        var response = await client.PostAsync(path, jsonBody);

        return ToolHelper.ToJson(new
        {
            status = "executado",
            path,
            ambiente = config.Environment.ToString(),
            resultado = TryParseJson(response),
        });
    }

    [McpServerTool(Name = "rm_rest_put")]
    [Description("""
        Executa PUT em endpoint REST do RM (atualização de registros via API).
        SECO: sem confirm=true retorna apenas preview.
        """)]
    public static async Task<string> RestPut(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Caminho REST (ex: '/api/framework/v1/companies/1')")] string path,
        [Description("Corpo JSON do request")] string jsonBody,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = DataServer.RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);

        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                path,
                ambiente = config.Environment.ToString(),
                bodyPreview = jsonBody.Length > 500 ? jsonBody[..500] + "..." : jsonBody,
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateRest(config);
        var response = await client.PutAsync(path, jsonBody);

        return ToolHelper.ToJson(new
        {
            status = "executado",
            path,
            ambiente = config.Environment.ToString(),
            resultado = TryParseJson(response),
        });
    }

    [McpServerTool(Name = "rm_rest_delete")]
    [Description("""
        Executa DELETE em endpoint REST do RM.
        SECO: sem confirm=true retorna apenas preview.
        """)]
    public static async Task<string> RestDelete(
        [Description("Ambiente: 'teste' ou 'producao'")] string env,
        [Description("Caminho REST com ID")] string path,
        [Description("true para executar")] bool? confirm = null,
        [Description("true para confirmar em PRODUÇÃO")] bool? productionConfirmed = null)
    {
        var config = ToolHelper.LoadConfig(env);
        var action = DataServer.RmDataServerWriteTools.EvaluateAction(
            config.Environment, confirm, productionConfirmed);

        if (action != "execute")
        {
            return ToolHelper.ToJson(new
            {
                mode = action,
                path,
                ambiente = config.Environment.ToString(),
                instrucao = action == "preview"
                    ? "Adicione confirm=true para executar"
                    : "Adicione production_confirmed=true para confirmar em PRODUÇÃO",
            });
        }

        using var client = ToolHelper.CreateRest(config);
        var response = await client.DeleteAsync(path);

        return ToolHelper.ToJson(new
        {
            status = "executado",
            path,
            ambiente = config.Environment.ToString(),
            resultado = TryParseJson(response),
        });
    }

    [McpServerTool(Name = "rm_rest_discover")]
    [Description("Descobre endpoints REST disponíveis no RM via Swagger/OpenAPI")]
    public static async Task<string> RestDiscover(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateRest(config);

        var paths = new List<string>
        {
            "api/swagger",
            "api/framework/v1/companies",
            "api/framework/v1/Branches",
            "api/framework/v1/users",
        };

        var results = new List<object>();
        foreach (var path in paths)
        {
            try
            {
                var response = await client.GetAsync(path);
                results.Add(new
                {
                    path,
                    status = "disponivel",
                    tamanhoResposta = response.Length,
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    path,
                    status = "erro",
                    mensagem = ex.Message,
                });
            }
        }

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            endpoints = results,
            dica = "Use rm_rest_get com qualquer path da API REST do RM",
        });
    }

    [McpServerTool(Name = "rm_rest_token")]
    [Description("Obtém um token OAuth do RM (útil para diagnóstico de autenticação)")]
    public static async Task<string> GetToken(
        [Description("Ambiente: 'teste' ou 'producao'")] string env)
    {
        var config = ToolHelper.LoadConfig(env);
        using var client = ToolHelper.CreateRest(config);

        var token = await client.GetTokenAsync();

        return ToolHelper.ToJson(new
        {
            ambiente = config.Environment.ToString(),
            status = "autenticado",
            tokenParcial = token.Length > 20
                ? token[..10] + "..." + token[^10..] : "(curto)",
            tamanhoToken = token.Length,
        });
    }

    private static object TryParseJson(string text)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(text);
        }
        catch
        {
            return text;
        }
    }
}
