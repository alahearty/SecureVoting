using System.Text.RegularExpressions;
using SecureVoting.Domain;

namespace SecureVoting.Application.Security;

public static partial class MatriculationNumber
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9/_-]{4,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var candidate = input.Trim().ToUpperInvariant();
        if (!Format().IsMatch(candidate) || !candidate.Any(char.IsAsciiDigit))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string Normalize(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            throw new ValidationException(
                "Matriculation number must be 5-40 characters using letters, digits, slash, dash or underscore, and include a digit.");
        }

        return normalized;
    }
}
