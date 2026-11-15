using System.Net;
using System.Text.RegularExpressions;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Turns a raw email body (HTML or plain text) into clean text the rules can
/// read: tags and links removed, entities decoded, whitespace tidied.
/// </summary>
public static partial class EmailText
{
    public static string Clean(string? body)
    {
        if (string.IsNullOrEmpty(body)) return "";

        var text = body;
        if (LooksLikeHtml().IsMatch(text))
        {
            text = StyleOrScript().Replace(text, " ");
            text = BlockTag().Replace(text, "\n");
            text = AnyTag().Replace(text, " ");
        }
        text = WebUtility.HtmlDecode(text);
        text = MarkdownLink().Replace(text, "$1");   // "[My Pnet](https://...)" -> "My Pnet"
        text = Url().Replace(text, " ");
        text = text.Replace(' ', ' ').Replace('’', '\'').Replace('‘', '\'').Replace('–', '-').Replace('—', '-');
        text = SpacesAndTabs().Replace(text, " ");
        text = BlankLines().Replace(text, "\n");
        return text.Trim();
    }

    /// <summary>
    /// Splits text into sentences (and lines, since emails often have
    /// sentence-like lines without full stops: greetings, sign-offs).
    /// </summary>
    public static IReadOnlyList<string> Sentences(string cleanText) =>
        SentenceBreak().Split(cleanText)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    [GeneratedRegex(@"<\s*(html|body|div|p|br|table|span|a)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LooksLikeHtml();

    [GeneratedRegex(@"<(style|script)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();

    [GeneratedRegex(@"<\s*(br|/p|/div|/tr|/li|/h\d)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\[([^\]]+)\]\((?:https?://|mailto:)[^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Url();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpacesAndTabs();

    [GeneratedRegex(@"\s*\n\s*")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"(?<=[.!?])\s+|\n")]
    private static partial Regex SentenceBreak();
}
