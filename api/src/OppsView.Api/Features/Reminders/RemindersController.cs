using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OppsView.Api.Features.Reminders;

[ApiController]
[Authorize]
[Route("reminders")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class RemindersController(ReminderService reminders) : ControllerBase
{
    /// <summary>Reminders to show under the bell, newest first.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ReminderResponse>> List(CancellationToken ct) => reminders.ListAsync(ct);

    /// <summary>Just the number, for the bell's badge.</summary>
    [HttpGet("count")]
    public async Task<ReminderCountResponse> Count(CancellationToken ct) => new(await reminders.CountAsync(ct));

    [HttpPost("{id:guid}/done")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Done(Guid id, CancellationToken ct) =>
        await reminders.MarkDoneAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct) =>
        await reminders.DismissAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Hide it for a few days ("remind me later").</summary>
    [HttpPost("{id:guid}/snooze")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Snooze(Guid id, SnoozeRequest request, CancellationToken ct) =>
        await reminders.SnoozeAsync(id, request.Days, ct) ? NoContent() : NotFound();

    [HttpGet("settings")]
    public Task<ReminderSettingsResponse> Settings(CancellationToken ct) => reminders.GetSettingsAsync(ct);

    [HttpPut("settings")]
    [ProducesResponseType<ReminderSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ReminderSettingsResponse> UpdateSettings(ReminderSettingsRequest request, CancellationToken ct) =>
        reminders.UpdateSettingsAsync(request, ct);
}
