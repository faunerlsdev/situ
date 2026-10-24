using System.Text;

namespace Situ.Infrastructure.Persistence.Naming;

internal static class SnakeCase
{
    public static string FromPascal(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (char.IsUpper(character))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static string PluralTable(string entityName)
    {
        var snake = FromPascal(entityName);
        var split = snake.LastIndexOf('_');
        var prefix = split < 0 ? string.Empty : snake[..(split + 1)];
        var word = split < 0 ? snake : snake[(split + 1)..];
        return prefix + Pluralize(word);
    }

    private static string Pluralize(string word) => word switch
    {
        "media" or "settings" or "news" => word,
        _ when word.EndsWith('y') && word.Length > 1 && !"aeiou".Contains(word[^2]) => string.Concat(word.AsSpan(0, word.Length - 1), "ies"),
        _ when word.EndsWith("ch") || word.EndsWith("sh") || word.EndsWith('x') || word.EndsWith('z') => word + "es",
        _ when word.EndsWith('s') => word,
        _ => word + "s",
    };
}
