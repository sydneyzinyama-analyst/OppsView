using System.Text.RegularExpressions;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Decides what kind of email this is from the phrases in it.
///
/// Two rules keep it from jumping to conclusions:
///   - Sentences that describe what *might* happen ("If you do not meet the
///     requirements, we will let you know that your application was
///     unsuccessful") are skipped, because they appear in confirmation emails.
///   - The checks run from the most specific outcome to the least: a
///     rejection often also says "thank you for applying", so rejection is
///     checked before confirmation.
/// </summary>
public static partial class EmailClassifier
{
    public static EmailKind Classify(string subject, IReadOnlyList<string> sentences)
    {
        var factual = sentences.Where(s => !IsHypothetical(s)).Prepend(subject).ToList();
        bool Any(Regex pattern) => factual.Any(pattern.IsMatch);

        if (Any(Rejection())) return EmailKind.Rejection;
        if (Any(Offer())) return EmailKind.Offer;
        if (Any(Interview())) return EmailKind.InterviewInvite;
        if (Any(Viewed())) return EmailKind.ApplicationViewed;
        if (Any(Received())) return EmailKind.ApplicationReceived;
        if (Any(JobAlert())) return EmailKind.NotRelevant;
        // Nothing about an application at all: not for us.
        if (!sentences.Prepend(subject).Any(JobWords().IsMatch)) return EmailKind.NotRelevant;
        return EmailKind.Unknown;
    }

    /// <summary>"If…", "Should…", "In the event…": describes a possibility, not what happened.</summary>
    private static bool IsHypothetical(string sentence) => Hypothetical().IsMatch(sentence);

    [GeneratedRegex(@"^(if|should|in the event|in case|once|when)\b|\b(if|unless) (you|your|we)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Hypothetical();

    [GeneratedRegex(
        @"\b(has|have|had) (not )?been unsuccessful\b|\bwas unsuccessful\b|\bnot been successful\b|\bwere not successful\b" +
        @"|\b(won't|will not|wouldn't|would not|are not|aren't|not) (be )?(moving|move|going|taking (it|this|your application)) (ahead|forward|further)\b" +
        @"|\b(won't|will not|unable to|not able to|cannot|can't) (be )?(proceed(ing)?|progress(ing)?) (with )?your (application|candidacy)\b" +
        @"|\bregret to (inform|advise|let you know)\b|\bdecided (to|not to) (proceed|move forward|pursue|continue) with other\b" +
        @"|\b(other|another) candidates? (whose|who|that)\b|\bposition has (now )?been filled\b|\bno longer (being )?consider",
        RegexOptions.IgnoreCase)]
    private static partial Regex Rejection();

    [GeneratedRegex(@"\b(pleased|delighted|happy) to (offer|extend an offer)\b|\boffer of employment\b|\bjob offer\b|\bemployment offer\b", RegexOptions.IgnoreCase)]
    private static partial Regex Offer();

    [GeneratedRegex(
        @"\binvit(e|ing|ation)\b.{0,40}\binterview\b|\binterview invitation\b|\b(schedule|book|arrange|set up) (an |a |your )?(interview|call|meeting)\b" +
        @"|\blike to (interview|meet|speak|chat) (with )?you\b|\b(you have|you've) been (shortlisted|selected for an interview)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Interview();

    [GeneratedRegex(@"\b(viewed|reviewed|looked at|opened) your (application|cv|profile)\b|\byour (application|cv) (was|has been) (viewed|seen|opened)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Viewed();

    [GeneratedRegex(
        @"\b(received|receipt of) your application\b|\bthank(s| you) for (applying|your application|taking the time to apply|your interest)\b" +
        @"|\byour application (has been|was) (successfully )?(sent|submitted|received)\b|\bapplication (submitted|received)\b|\bsuccessfully applied\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Received();

    [GeneratedRegex(@"\bjob alert\b|\bjobs? (matching|recommended|for you)\b|\bnew jobs?\b|\brecommended (jobs|for you)\b|\bjobs you (might|may) (like|be interested)\b", RegexOptions.IgnoreCase)]
    private static partial Regex JobAlert();

    [GeneratedRegex(@"\b(application|applied|apply|applying|interview|vacancy|position|role|candidate|recruit)", RegexOptions.IgnoreCase)]
    private static partial Regex JobWords();
}
