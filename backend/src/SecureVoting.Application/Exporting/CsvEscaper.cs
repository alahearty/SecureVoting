namespace SecureVoting.Application.Exporting;

public static class CsvEscaper
{
    public static string Escape(string? value)
    {
        var safe = value ?? string.Empty;
        var firstMeaningful = safe.TrimStart().FirstOrDefault();
        if (safe.Length > 0 && (firstMeaningful is '=' or '+' or '-' or '@' || char.IsControl(firstMeaningful)))
        {
            safe = "'" + safe;
        }

        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
