using System.Text.RegularExpressions;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.EmailImport.Parsing;

/// <summary>
/// Decides what kind of email this is from the phrases in it.
///
/// Rules that keep it from jumping to conclusions:
///   - Sentences that describe what *might* happen ("If you do not meet the
///     requirements, we will let you know that your application was
///     unsuccessful") are skipped, because they appear in confirmation emails.
///   - Checks run from the most specific outcome to the least: a rejection
///     often also says "thank you for applying", so rejection comes first.
///   - Job sites (Pnet, Indeed, LinkedIn...) send confirmations but also a
///     lot of marketing ("recommended for you", "apply now"). From them, only
///     an unmistakable confirmation or status update counts.
/// </summary>
public static partial class EmailClassifier
{
    public static EmailKind Classify(string subject, IReadOnlyList<string> sentences, bool fromJobBoard = false)
    {
        // Visas, loans, accounts...: "application", but not for a job.
        if (NotAJob().IsMatch(subject)) return EmailKind.NotRelevant;

        var factual = sentences.Where(s => !IsHypothetical(s)).Prepend(subject).ToList();
        bool Any(Regex pattern) => factual.Any(pattern.IsMatch);

        if (Any(Rejection())) return EmailKind.Rejection;
        if (Any(Offer())) return EmailKind.Offer;
        if (Any(Interview())) return EmailKind.InterviewInvite;
        if (Any(Viewed())) return EmailKind.ApplicationViewed;

        // "Thank you for applying" / "your application has been sent":
        // unmistakable, even if the email also lists recommended jobs.
        if (Any(StrongReceived())) return EmailKind.ApplicationReceived;

        // Recommendations, alerts, "apply now": about jobs, not an application.
        if (Any(Recommendation())) return EmailKind.NotRelevant;
        if (fromJobBoard) return EmailKind.NotRelevant;

        // Softer wording ("thank you for your interest"), trusted only from employers.
        if (Any(WeakReceived())) return EmailKind.ApplicationReceived;

        // Only ask the user about emails that are clearly about one of *their*
        // applications. Generic job words ("role", "career") also appear in
        // course adverts and newsletters, which aren't worth a review.
        return factual.Any(ApplicationSignal().IsMatch) ? EmailKind.Unknown : EmailKind.NotRelevant;
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
        @"|\b(other|another) candidates? (whose|who|that)\b|\bposition has (now )?been filled\b|\bno longer (being )?consider" +
        @"|\bapplication\b.{0,120}\bunsuccessful\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Rejection();

    [GeneratedRegex(@"\b(pleased|delighted|happy) to (offer|extend an offer)\b|\boffer of employment\b|\bjob offer\b|\bemployment offer\b", RegexOptions.IgnoreCase)]
    private static partial Regex Offer();

    [GeneratedRegex(
        @"\binvit(e|ing|ation)\b.{0,40}\binterview\b|\binterview invitation\b|\b(schedule|book|arrange|set up) (an |a |your )?(interview|call|meeting)\b" +
        @"|\blike to (interview|meet|speak|chat) (with )?you\b|\b(you have|you've) been (shortlisted|selected for an interview)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Interview();

    // About the *application*. "A recruiter viewed your profile/CV" is a
    // job-site notification about the profile, not an application update.
    [GeneratedRegex(@"\b(viewed|reviewed|looked at|opened) your application\b|\byour application (was|has been) (viewed|seen|opened)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Viewed();

    [GeneratedRegex(
        @"\b(received|receipt of) your (job )?application\b|\bthank(s| you) for (applying|your application|taking the time to apply|submitting your application)\b" +
        @"|\byour application (for .{1,100}? )?(has been|was) (successfully )?(sent|submitted|received|delivered)\b" +
        @"|\bsuccessfully applied\b|\byou('ve| have) (successfully )?applied (for|to)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex StrongReceived();

    [GeneratedRegex(@"\bthank(s| you) for your interest\b|\bapplication (submitted|received)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeakReceived();

    [GeneratedRegex(
        @"\brecommend(ed|ation|ations)?\b|\bjob alerts?\b|\bjobs? (matching|for you|you (might|may)|near you|like this)\b" +
        @"|\bsimilar (jobs|roles|positions|vacancies)\b|\bapply now\b|\bnew jobs?\b|\b(new|latest|top) (vacancies|opportunities|positions)\b" +
        @"|\bbased on your (profile|search|cv|preferences|activity)\b|\b(you|candidates) (might|may) (also )?(like|be interested|be a (good|great) fit)\b" +
        @"|\bare you (still )?(interested|looking|open)\b|\bdon't miss\b|\b(still|now|actively) hiring\b|\btalent (community|pool|network)\b" +
        @"|\b(people|others|candidates) (also )?(applied|viewed)\b|\b(daily|weekly) (jobs|digest|alert)\b|\bcomplete your profile\b|\bupdate your (cv|profile)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Recommendation();

    [GeneratedRegex(
        @"\byour (job )?application\b|\byou applied\b|\bapplication (for|to)\b|\bcandidacy\b|\binterview\b|\bassessment\b" +
        @"|\bshortlist|\bnext (step|round|stage)s?\b|\bone step closer\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ApplicationSignal();

    [GeneratedRegex(@"\b(visa|work permit|permit application|passport|loan|credit card|insurance|bank account|account application|tender)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotAJob();
}
