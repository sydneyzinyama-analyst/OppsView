using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace OppsView.Api.Features.Auth;

public static class AuthRateLimit
{
    public const string Policy = "auth";
}

[ApiController]
[Route("auth")]
[EnableRateLimiting(AuthRateLimit.Policy)] // see Program.cs: 10 per minute per IP by default
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Create an account and get a token straight away.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = await auth.RegisterAsync(request, ct);
        return response is null
            ? Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.")
            : Ok(response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await auth.LoginAsync(request, ct);
        // Same message for both failure cases, so this can't be used to check
        // whether an email is registered.
        return response is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Incorrect email or password.")
            : Ok(response);
    }
}
