using System.Text.RegularExpressions;

namespace RmMcp.Sql;

public static partial class SqlIdentifier
{
    [GeneratedRegex(@"^[A-Za-z_#@][A-Za-z0-9_#@]{0,127}(\.[A-Za-z_#@][A-Za-z0-9_#@]{0,127})?$")]
    private static partial Regex ValidPattern();

    public static string Validate(string name, string label = "tabela")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException($"Nome de {label} não pode ser vazio");

        name = name.Trim();
        if (!ValidPattern().IsMatch(name))
            throw new ArgumentException(
                $"Nome de {label} inválido: '{name}'. Use apenas letras, números e _.");
        return name;
    }

    public static string Bracket(string name)
    {
        var validated = Validate(name);
        if (validated.Contains('.'))
        {
            var parts = validated.Split('.');
            return $"[{parts[0]}].[{parts[1]}]";
        }
        return $"[{validated}]";
    }
}
