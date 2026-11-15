namespace OppsView.Api.Domain;

/// <summary>
/// The user's CV, as plain text (one per user). The uploaded file itself is
/// not kept: only the text the matcher read from it, which is all matching needs.
/// </summary>
public class Cv
{
    public const int FileNameMaxLength = 255;
    public const int TextMaxLength = 100_000;

    public Guid UserId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string Text { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }

    private Cv() { }

    public static Cv Create(Guid userId, string fileName, string text, DateTime nowUtc)
    {
        var cv = new Cv { UserId = userId };
        cv.Replace(fileName, text, nowUtc);
        return cv;
    }

    public void Replace(string fileName, string text, DateTime nowUtc)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0)
            throw new DomainException("File", "The CV has no text.");
        if (text.Length > TextMaxLength)
            throw new DomainException("File", "That CV is too long.");

        fileName = Path.GetFileName((fileName ?? "").Trim());
        FileName = fileName.Length == 0 ? "cv" : fileName[..Math.Min(fileName.Length, FileNameMaxLength)];
        Text = text;
        UploadedAt = nowUtc;
    }
}
