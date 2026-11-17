using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OppsView.Api.Features.Analytics;

[ApiController]
[Authorize]
[Route("analytics")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>The dashboard numbers. <paramref name="weeks"/>: how many weeks the "per week" chart covers.</summary>
    [HttpGet]
    public Task<AnalyticsResponse> Get([FromQuery, Range(1, 52)] int weeks = 12, CancellationToken ct = default) =>
        analytics.GetAsync(weeks, ct);
}
