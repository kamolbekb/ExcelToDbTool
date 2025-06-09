using System.Text.RegularExpressions;

namespace DataInserter.Utilities;

public static class StringNormalizer
{
    public static string NormalizeEmail(string email)
    {
        return email?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    public static string NormalizeUserName(string userName)
    {
        return userName?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    public static string NormalizeName(string name)
    {
        return name?.Trim() ?? string.Empty;
    }

    public static string ExtractNumberSuffix(string input)
    {
        var match = Regex.Match(input, @"\d+$");
        return match.Success ? match.Value : string.Empty;
    }

    public static bool ContainsWord(string input, string word)
    {
        return Regex.IsMatch(input, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase);
    }
}
