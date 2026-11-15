using System.Text.RegularExpressions;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Finds "if you don't hear from us within N days/weeks, consider your
/// application unsuccessful" and works out the date it means.
///
/// Handles digits and words ("2", "two", "two (2)", "fourteen"), ranges
/// ("2-3 weeks": the longer one, to be fair to the applicant), calendar and
/// working/business days, weeks and months. The period is counted from the
/// day the email arrived: when an email says "from the closing date",
/// that's the closest date OppsView knows.
/// </summary>
public static partial class NoReplyDeadlineFinder
{
    public static DateOnly? Find(IReadOnlyList<string> sentences, DateOnly emailDate)
    {
        for (var i = 0; i < sentences.Count; i++)
        {
            // The two halves are usually in one sentence, occasionally split
            // over two ("…within 2 weeks. Please then consider…").
            var text = i + 1 < sentences.Count ? sentences[i] + " " + sentences[i + 1] : sentences[i];
            if (!NoReply().IsMatch(text) || !ConsiderUnsuccessful().IsMatch(text)) continue;

            var m = Period().Match(text);
            if (!m.Success) continue;

            var amount = Math.Max(Number(m.Groups["n1"].Value), Number(m.Groups["n2"].Value));
            if (amount is <= 0 or > 365) continue;

            var unit = m.Groups["unit"].Value.ToLowerInvariant();
            var working = m.Groups["working"].Success;
            return unit switch
            {
                _ when unit.StartsWith("week") => emailDate.AddDays(7 * amount),
                _ when unit.StartsWith("month") => emailDate.AddMonths(amount),
                _ when working => AddWorkingDays(emailDate, amount),
                _ => emailDate.AddDays(amount),
            };
        }
        return null;
    }

    private static DateOnly AddWorkingDays(DateOnly start, int days)
    {
        var date = start;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--;
        }
        return date;
    }

    private static readonly Dictionary<string, int> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = 1, ["an"] = 1, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6,
        ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["fourteen"] = 14,
        ["fifteen"] = 15, ["twenty"] = 20, ["twenty-one"] = 21, ["thirty"] = 30, ["sixty"] = 60, ["ninety"] = 90,
    };

    private static int Number(string value) =>
        string.IsNullOrEmpty(value) ? 0 : int.TryParse(value, out var n) ? n : Words.GetValueOrDefault(value);

    // "not heard from us", "do not receive a response", "haven't been contacted", "no feedback has been received"
    [GeneratedRegex(
        // "n't" has no word boundary before it ("don't"), hence the separate alternatives.
        @"(\bnot\b|n't\b|\bno\b).{0,40}\b(hear|heard|receive|received|been contacted|contacted|feedback|response|communication|correspondence)\b" +
        @"|\bno (feedback|response|communication|correspondence|contact)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex NoReply();

    // "consider your application unsuccessful", "regard yourself as unsuccessful", "assume ... not successful"
    [GeneratedRegex(
        @"\b(consider|regard|assume|deem|accept|take it)\b.{0,60}\b(unsuccessful|not (been )?successful|regretted|declined)\b" +
        @"|\b(application|you) (should be|will be|must be)? ?(considered|regarded|deemed) (as |to be |to have been )?(unsuccessful|not successful)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConsiderUnsuccessful();

    // "within 2 weeks", "within two (2) weeks", "in the next 14 working days", "after 2-3 weeks", "within a month"
    [GeneratedRegex(
        @"\b(?:within|in|after|for|of|by)\s+(?:the\s+next\s+|a\s+period\s+of\s+|the\s+following\s+)?" +
        @"(?<n1>\d{1,3}|a|an|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fourteen|fifteen|twenty|twenty-one|thirty|sixty|ninety)" +
        @"(?:\s*\(\d{1,3}\))?" +
        @"(?:\s*(?:-|–|to|or)\s*(?<n2>\d{1,3}|two|three|four|five|six|ten|fourteen|thirty)(?:\s*\(\d{1,3}\))?)?" +
        @"\s+(?<working>(?:working|business)\s+)?(?<unit>days?|weeks?|months?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Period();
}
