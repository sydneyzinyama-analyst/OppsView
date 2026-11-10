using Microsoft.IdentityModel.JsonWebTokens;

namespace OppsView.Api.Infrastructure;

/// <summary>
/// Who is making the request, read from the validated JWT. Services depend on
/// this interface instead of HttpContext, so they stay easy to test.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
}

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException("No authenticated user. Is the endpoint missing [Authorize]?");
        }
    }
}
