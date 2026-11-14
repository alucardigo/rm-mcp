using System.Text;

namespace RmMcp.Sql;

public static class SqlGuard
{
    private static readonly string[] Blocked =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "INTO",
        "CREATE", "ALTER", "DROP", "TRUNCATE",
        "GRANT", "REVOKE", "DENY",
        "EXEC", "EXECUTE",
        "BACKUP", "RESTORE", "SHUTDOWN", "RECONFIGURE",
        "OPENROWSET", "OPENDATASOURCE", "OPENQUERY",
        "DBCC", "BULK", "XP_CMDSHELL",
        "WRITETEXT", "UPDATETEXT",
    ];

    public static (bool Allowed, string? Reason) Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (false, "SQL vazio");

        var sanitized = Sanitize(sql).Trim();
        if (sanitized.Length == 0)
            return (false, "SQL vazio após remoção de comentários");

        var firstWord = ExtractFirstWord(sanitized);
        if (firstWord is not ("SELECT" or "WITH"))
            return (false, $"Comando '{firstWord}' não permitido — apenas SELECT e WITH");

        if (sanitized.Contains(';'))
            return (false, "Múltiplos statements não são permitidos (;)");

        foreach (var keyword in Blocked)
        {
            if (ContainsWord(sanitized, keyword))
                return (false, $"Palavra-chave proibida: {keyword}");
        }

        return (true, null);
    }

    internal static string Sanitize(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        int i = 0;
        while (i < sql.Length)
        {
            if (sql[i] == '\'')
            {
                sb.Append('\'');
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'' && (i + 1 >= sql.Length || sql[i + 1] != '\''))
                    {
                        sb.Append('\'');
                        i++;
                        break;
                    }
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        sb.Append("  ");
                        i += 2;
                        continue;
                    }
                    sb.Append(' ');
                    i++;
                }
            }
            else if (sql[i] == '[')
            {
                sb.Append('[');
                i++;
                while (i < sql.Length && sql[i] != ']')
                {
                    sb.Append(' ');
                    i++;
                }
                if (i < sql.Length) { sb.Append(']'); i++; }
            }
            else if (sql[i] == '"')
            {
                sb.Append('"');
                i++;
                while (i < sql.Length && sql[i] != '"')
                {
                    sb.Append(' ');
                    i++;
                }
                if (i < sql.Length) { sb.Append('"'); i++; }
            }
            else if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-')
            {
                sb.Append(' ');
                i += 2;
                while (i < sql.Length && sql[i] != '\n') i++;
            }
            else if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
            {
                sb.Append(' ');
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                if (i + 1 < sql.Length) i += 2;
            }
            else
            {
                sb.Append(sql[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string ExtractFirstWord(string sanitized)
    {
        var s = sanitized.AsSpan().TrimStart();
        int end = 0;
        while (end < s.Length && (char.IsLetter(s[end]) || s[end] == '_'))
            end++;
        return s[..end].ToString().ToUpperInvariant();
    }

    private static bool ContainsWord(string text, string word)
    {
        int idx = 0;
        while (true)
        {
            idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            bool leftOk = idx == 0
                || (!char.IsLetterOrDigit(text[idx - 1]) && text[idx - 1] != '_');
            bool rightOk = idx + word.Length >= text.Length
                || (!char.IsLetterOrDigit(text[idx + word.Length]) && text[idx + word.Length] != '_');

            if (leftOk && rightOk) return true;
            idx += word.Length;
        }
    }
}
