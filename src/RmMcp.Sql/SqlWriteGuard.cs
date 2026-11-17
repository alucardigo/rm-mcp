namespace RmMcp.Sql;

public static class SqlWriteGuard
{
    private static readonly string[] Blocked =
    [
        "CREATE", "ALTER", "DROP", "TRUNCATE",
        "GRANT", "REVOKE", "DENY",
        "BACKUP", "RESTORE", "SHUTDOWN", "RECONFIGURE",
        "OPENROWSET", "OPENDATASOURCE", "OPENQUERY",
        "DBCC", "BULK", "XP_CMDSHELL",
        "WRITETEXT", "UPDATETEXT",
    ];

    private static readonly string[] AllowedFirst =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE",
    ];

    public static (bool Allowed, string? Reason) Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (false, "SQL vazio");

        var sanitized = SqlGuard.Sanitize(sql).Trim();
        if (sanitized.Length == 0)
            return (false, "SQL vazio após remoção de comentários");

        var firstWord = ExtractFirstWord(sanitized);
        if (!AllowedFirst.Contains(firstWord))
            return (false, $"Comando '{firstWord}' não permitido — apenas INSERT, UPDATE, DELETE, MERGE");

        if (sanitized.Count(c => c == ';') > 1)
            return (false, "Múltiplos statements não são permitidos");

        foreach (var keyword in Blocked)
        {
            if (ContainsWord(sanitized, keyword))
                return (false, $"Palavra-chave proibida: {keyword}");
        }

        if (firstWord == "UPDATE" && !ContainsWord(sanitized, "WHERE"))
            return (false, "UPDATE sem WHERE não é permitido — adicione uma cláusula WHERE");

        if (firstWord == "DELETE" && !ContainsWord(sanitized, "WHERE"))
            return (false, "DELETE sem WHERE não é permitido — adicione uma cláusula WHERE");

        return (true, null);
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
