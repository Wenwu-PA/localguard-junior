using System.Text.RegularExpressions;

namespace LocalGuardJunior;

// Ищем заданные пользователем слова и заменяем их звёздочками.
internal static class TextFilter
{
    public static List<string> ParseWords(string input)
    {
        // Например, строка "слово1, слово2" превращается в список из двух слов.
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
            // Regex.Escape не позволяет символам типа точки стать частью регулярного выражения.
            // Границы слова нужны, чтобы не заменять часть другого слова.
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
