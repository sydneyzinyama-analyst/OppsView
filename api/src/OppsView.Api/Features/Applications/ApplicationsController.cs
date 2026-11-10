using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OppsView.Api.Features.Applications;

/// <summary>
/// Deliberately thin: HTTP in, HTTP out. Business rules live in the domain
/// entity and data access in <see cref="ApplicationService"/>.
/// [ApiController] returns 400 automatically when the request DTO fails validation.
/// </summary>
[ApiController]
[Authorize]
[Route("applications")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class ApplicationsController(IApplicationService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ApplicationResponse>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } application ? Ok(application) : NotFound();

    [HttpPost]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(SaveApplicationRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        // 201 + a Location header pointing at GET /applications/{id}.
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, SaveApplicationRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct) is { } updated ? Ok(updated) : NotFound();

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Move the application to a new status (see StatusTransitions for the rules).</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        await service.ChangeStatusAsync(id, request.Status!.Value, ct) is { } updated ? Ok(updated) : NotFound();

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<IReadOnlyList<StatusChangeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        await service.GetHistoryAsync(id, ct) is { } history ? Ok(history) : NotFound();
}
