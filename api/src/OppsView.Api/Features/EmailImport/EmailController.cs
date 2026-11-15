using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.EmailImport.Gmail;
using OppsView.Api.Infrastructure;

namespace OppsView.Api.Features.EmailImport;

[ApiController]
[Authorize]
[Route("email")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class EmailController(
    GmailSyncService gmail,
    ReviewService review,
    ICurrentUser currentUser,
    IOptions<GoogleOptions> options) : ControllerBase
{
    /// <summary>Is Gmail connected, when was the last sync, how many emails need review.</summary>
    [HttpGet("status")]
    public Task<EmailStatusResponse> Status(CancellationToken ct) => gmail.GetStatusAsync(currentUser.Id, ct);

    /// <summary>Returns the Google consent URL; the web app sends the browser there.</summary>
    [HttpPost("google/connect")]
    [ProducesResponseType<ConnectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Connect()
    {
        if (!gmail.IsAvailable) return NotConfigured();
        return Ok(new ConnectResponse(gmail.StartConnect(currentUser.Id)));
    }

    /// <summary>
    /// Google redirects the browser here after the consent screen. It's a
    /// plain browser visit with no JWT, so it's anonymous; the encrypted
    /// state says which user it belongs to. Ends by sending the browser back
    /// to the web app with ?gmail=connected (or an error code).
    /// </summary>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Callback(string? code, string? state, string? error, CancellationToken ct)
    {
        var result = await gmail.CompleteConnectAsync(code, state, error, ct);
        return Redirect($"{options.Value.WebAppUrl.TrimEnd('/')}/?gmail={Uri.EscapeDataString(result)}");
    }

    [HttpDelete("google")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await gmail.DisconnectAsync(currentUser.Id, ct);
        return NoContent();
    }

    /// <summary>Read new job emails and update applications.</summary>
    [HttpPost("sync")]
    [ProducesResponseType<ImportSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        if (!gmail.IsAvailable) return NotConfigured();
        try
        {
            return Ok(await gmail.SyncAsync(currentUser.Id, ct));
        }
        catch (GmailNotConnectedException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Gmail isn't connected. Please connect it again.");
        }
        catch (SyncInProgressException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "A sync is already running.");
        }
    }

    [HttpGet("review")]
    public Task<IReadOnlyList<ReviewItemResponse>> Review(CancellationToken ct) => review.ListAsync(currentUser.Id, ct);

    [HttpPost("review/{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct) =>
        await review.DismissAsync(currentUser.Id, id, ct) ? NoContent() : NotFound();

    /// <summary>Create an application from a reviewed email with the details the user confirmed.</summary>
    [HttpPost("review/{id:guid}/application")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateApplication(Guid id, SaveApplicationRequest request, CancellationToken ct)
    {
        var created = await review.CreateApplicationAsync(currentUser.Id, id, request, ct);
        return created is null
            ? NotFound()
            : CreatedAtAction(nameof(ApplicationsController.Get), "Applications", new { id = created.Id }, created);
    }

    private ObjectResult NotConfigured() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Gmail import isn't set up on this server. See the README for the Google Cloud steps.");
}
