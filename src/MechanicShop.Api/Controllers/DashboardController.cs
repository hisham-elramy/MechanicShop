using Asp.Versioning;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace MechanicShop.Api.Controllers;

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(ISender sender) : ApiController
{

    [HttpGet("stats")]
    [ProducesResponseType(typeof(TodayWorkOrderStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTodayStats([FromQuery] DateOnly? date, [FromServices] IAppClock clock, CancellationToken cancellationToken)
    {
        var statsDate = date ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var result = await sender.Send(new GetWorkOrderStatsQuery(statsDate), cancellationToken);

        return result.Match(response => Ok(response), Problem);
    }
}