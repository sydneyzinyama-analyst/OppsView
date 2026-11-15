using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OppsView.Api.Features.Matching;

[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
public sealed class MatchingController(MatchingService matching) : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    [HttpGet("cv")]
    [ProducesResponseType<CvResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCv(CancellationToken ct) =>
        await matching.GetCvAsync(ct) is { } cv ? Ok(cv) : NotFound();

    /// <summary>Upload (or replace) your CV: PDF, .docx or .txt, up to 5 MB. Only its text is kept.</summary>
    [HttpPut("cv")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)] // the file plus the form's own overhead
    [ProducesResponseType<CvResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCv(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return FileError("Choose a CV file to upload.");
        if (file.Length > MaxUploadBytes) return FileError("That file is over 5 MB. Please upload a smaller CV.");

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await matching.UploadCvAsync(file.FileName, stream, ct));
        }
        catch (MatcherRejectedException ex)
        {
            return FileError(ex.Message);
        }
        catch (MatcherUnavailableException)
        {
            return MatcherDown();
        }
    }

    [HttpDelete("cv")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCv(CancellationToken ct)
    {
        await matching.DeleteCvAsync(ct);
        return NoContent();
    }

    /// <summary>Match your CV against a job ad; with applicationId, the result is saved on that application.</summary>
    [HttpPost("match")]
    [ProducesResponseType<MatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Match(MatchRequest request, CancellationToken ct)
    {
        try
        {
            return await matching.MatchAsync(request, ct) is { } result ? Ok(result) : NotFound();
        }
        catch (NoCvException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }
        catch (MatcherRejectedException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["jobAd"] = [ex.Message] }));
        }
        catch (MatcherUnavailableException)
        {
            return MatcherDown();
        }
    }

    [HttpGet("applications/{id:guid}/match")]
    [ProducesResponseType<SavedMatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SavedMatch(Guid id, CancellationToken ct) =>
        await matching.GetSavedMatchAsync(id, ct) is { } saved ? Ok(saved) : NotFound();

    private BadRequestObjectResult FileError(string message) =>
        BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = [message] })
        {
            Status = StatusCodes.Status400BadRequest,
        });

    private ObjectResult MatcherDown() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "The CV matcher isn't running. Start it (see matcher/README.md) and try again.");
}
