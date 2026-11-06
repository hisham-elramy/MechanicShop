using Asp.Versioning.Builder;
using MechanicShop.Api.Extensions;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var endpoints = app.MapGroup("/api/v{apiVersion:apiVersion}/dashboard")
            .WithApiVersionSet(apiVersionSet)
            .RequireAuthorization("ManagerOnly")
            .WithTags("Dashboard");

        endpoints.MapGet("/stats", GetWorkOrderStats)
            .WithSummary("Get today's work order stats")
            .Produces<TodayWorkOrderStatsDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .MapToApiVersion(1.0);
    }

    private static async Task<IResult> GetWorkOrderStats([AsParameters] DashboardStatsQuery queryParams, ISender sender, CancellationToken cancellationToken)
    {
        var date = queryParams.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await sender.Send(new GetWorkOrderStatsQuery(date), cancellationToken);

        return result.Match
        (
            value => Results.Ok(value),
            error => error.ToProblem()
        );
    }

    public sealed record DashboardStatsQuery([FromQuery] DateOnly? Date);

}