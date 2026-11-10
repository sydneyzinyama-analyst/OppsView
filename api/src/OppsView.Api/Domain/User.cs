namespace OppsView.Api.Domain;

/// <summary>An account. Each user only ever sees their own applications.</summary>
public class User
{
    public const int EmailMaxLength = 254;

    public Guid Id { get; private set; }

    /// <summary>Stored trimmed and lower-cased so lookups are case-insensitive.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>PBKDF2 hash produced by ASP.NET Core's PasswordHasher. Never the password itself.</summary>
    public string PasswordHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static User Create(string email, DateTime nowUtc)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0)
            throw new DomainException(nameof(Email), "Email is required.");
        if (normalized.Length > EmailMaxLength)
            throw new DomainException(nameof(Email), $"Email must be at most {EmailMaxLength} characters.");

        return new User { Id = Guid.NewGuid(), Email = normalized, CreatedAt = nowUtc };
    }

    /// <summary>
    /// The hash is computed outside the entity (by an injected hasher) and handed
    /// in, so the domain doesn't depend on a crypto library.
    /// </summary>
    public void SetPasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Password hash is required.", nameof(hash));
        PasswordHash = hash;
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
