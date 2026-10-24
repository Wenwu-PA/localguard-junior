using System.Text.RegularExpressions;

namespace LocalGuardJunior;

// Checks text for the words entered by the user and replaces matches with '*'.
internal static class TextFilter
{
    public static List<string> ParseWords(string input)
    {
        // Example: "bad, rude" -> ["bad", "rude"].
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static (string Text, int Count) ReplaceWords(string text, List<string> words)
    {
        int count = 0;
        string result = text;

        foreach (string word in words)
        {
            // Escape a user's word: characters like '.', '?' are treated literally.
            // Unicode boundaries prevent matching "cat" inside "catalog".
            string pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(word) + @"(?![\p{L}\p{N}])";

            result = Regex.Replace(result, pattern, match =>
            {
                count++;
                return new string('*', match.Length);
            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return (result, count);
    }
}
