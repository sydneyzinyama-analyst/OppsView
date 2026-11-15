using System.Text;
using System.Text.RegularExpressions;

namespace OppsView.Api.Domain;

/// <summary>
/// Compares company names the way a person would. Emails and users write the
/// same company differently: "Acme (Pty) Ltd", "ACME", "Acme Ltd." should all
/// match the same application.
/// </summary>
public static partial class CompanyName
{
    // Legal suffixes that don't help tell companies apart.
    private static readonly HashSet<string> Suffixes =
    [
        "pty", "ltd", "limited", "inc", "incorporated", "llc", "plc", "corp", "corporation",
        "co", "company", "gmbh", "bv", "sa", "group", "holdings", "rf", "npc", "cc",
    ];

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        // "Café" -> "cafe": strip accents so spelling variants match.
        var decomposed = name.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                plain.Append(c);

        var words = NonAlphanumeric().Replace(plain.ToString().Replace("&", " and "), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Drop trailing legal suffixes ("acme pty ltd" -> "acme"), but never
        // the whole name ("Group" stays "group").
        while (words.Count > 1 && Suffixes.Contains(words[^1]))
            words.RemoveAt(words.Count - 1);

        return string.Join(' ', words);
    }

    public static bool AreSame(string? a, string? b)
    {
        var x = Normalize(a);
        return x.Length > 0 && x == Normalize(b);
    }

    /// <summary>
    /// Looser: one name is the other with extra words on the end
    /// ("BBD" and "BBD Software", "Discovery" and "Discovery Health").
    /// Only safe as a fallback when it identifies a single application.
    /// </summary>
    public static bool StartsWithSameWords(string? a, string? b)
    {
        var x = Normalize(a).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var y = Normalize(b).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (x.Length == 0 || y.Length == 0) return false;
        var (shorter, longer) = x.Length <= y.Length ? (x, y) : (y, x);
        return shorter.SequenceEqual(longer.Take(shorter.Length));
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
