namespace RmMcp.Core.Config;

public enum RmEnvironment
{
    Teste,
    Producao
}

public static class RmEnvironmentParser
{
    public static RmEnvironment Parse(string value) => value.Trim().ToLowerInvariant() switch
    {
        "teste" or "test" => RmEnvironment.Teste,
        "producao" or "produção" or "prod" or "production" => RmEnvironment.Producao,
        _ => throw new ArgumentException(
            $"Ambiente inválido: '{value}'. Use 'teste' ou 'producao'.")
    };
}
