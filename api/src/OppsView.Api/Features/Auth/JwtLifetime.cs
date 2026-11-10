namespace OppsView.Api.Features.Auth;

public static class JwtLifetime
{
    /// <summary>
    /// A token is valid from notBefore until expires, give or take the clock
    /// skew allowed between servers. Tokens we issue always carry both claims,
    /// so a token missing "exp" is rejected rather than treated as eternal.
    /// </summary>
    public static bool IsValid(DateTime? notBefore, DateTime? expires, DateTime nowUtc, TimeSpan clockSkew)
    {
        if (expires is null) return false;
        if (notBefore is not null && notBefore.Value.ToUniversalTime() > nowUtc + clockSkew) return false;
        return expires.Value.ToUniversalTime() > nowUtc - clockSkew;
    }
}
