using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;
using OppsView.Api.Domain;

namespace OppsView.Api.Features.Auth;

public interface IAuthService
{
    /// <returns>null if the email is already registered.</returns>
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <returns>null if the email or password is wrong.</returns>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    TimeProvider time) : IAuthService
{
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return null;

        var user = User.Create(email, time.GetUtcNow().UtcDateTime);
        user.SetPasswordHash(hasher.HashPassword(user, request.Password));
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two registrations raced past the check above and the unique
            // index on Email rejected the second one. Anything else is a real
            // error, so rethrow it.
            db.Entry(user).State = EntityState.Detached;
            if (await db.Users.AnyAsync(u => u.Email == email, ct)) return null;
            throw;
        }

        return tokens.CreateToken(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            // Hash anyway so "unknown email" and "wrong password" take about
            // the same time; otherwise response timing reveals which emails
            // have accounts.
            hasher.HashPassword(DummyUser, request.Password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The hasher's defaults got stronger since this hash was made
            // (e.g. more PBKDF2 iterations): upgrade it while we have the password.
            user.SetPasswordHash(hasher.HashPassword(user, request.Password));
            await db.SaveChangesAsync(ct);
        }

        return tokens.CreateToken(user);
    }

    private static readonly User DummyUser = User.Create("dummy@oppsview.invalid", DateTime.UnixEpoch);
}
