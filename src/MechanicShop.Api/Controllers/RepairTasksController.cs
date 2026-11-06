using Asp.Versioning;
using MechanicShop.Application.Features.RepairTasks.Commands.CreateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;
using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTaskById;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Contracts.Requests.RepairTasks;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace MechanicShop.Api.Controllers;


[Route("api/v{version:apiVersion}/repair-tasks")]
[ApiVersion("1.0")]
[Authorize]
public sealed class RepairTasksController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<RepairTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves all repair tasks.")]
    [EndpointDescription("Returns a list of all repair tasks available in the system.")]
    [EndpointName("GetRepairTasks")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRepairTasksQuery(), cancellationToken);

        return result.Match(response => Ok(response), Problem);
    }

    [HttpGet("{repairTaskId:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves a repair tasks by ID.")]
    [EndpointDescription("Returns detailed information for the specified repair task if it exists.")]
    [EndpointName("GetRepairTaskById")]
    [MapToApiVersion("1.0")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetById(Guid repairTaskId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRepairTaskByIdQuery(repairTaskId), cancellationToken);

        return result.Match(response => Ok(response), Problem);
    }


    [HttpPost]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Creates a new repair task.")]
    [EndpointDescription("Creates a repair task and optionally includes parts.")]
    [EndpointName("CreateRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Create([FromBody] CreateRepairTaskRequest request, CancellationToken cancellationToken)
    {
        var parts = request.Parts.ConvertAll(p => new CreateRepairTaskPartCommand(p.Name, p.Cost, p.Quantity));

        var command = new CreateRepairTaskCommand
        (
            request.Name,
            request.LaborCost,
            request.EstimatedDurationInMins is not null ? (RepairDurationInMinutes)request.EstimatedDurationInMins : null,
            parts
        );

        var result = await sender.Send(command, cancellationToken);

        return result.Match
        (
            response => CreatedAtAction
            (
                nameof(GetById),
                new { repairTaskId = response.RepairTaskId },
                response
            ),
            Problem
        );
    }


    [HttpPut("{repairTaskId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Updates an existing repair task.")]
    [EndpointDescription("Updates a repair task and its associated parts.")]
    [EndpointName("UpdateRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Update(Guid repairTaskId, [FromBody] UpdateRepairTaskRequest request, CancellationToken cancellationToken)
    {
        var parts = request.Parts.ConvertAll(p => new UpdateRepairTaskPartCommand(p.PartId, p.Name, p.Cost, p.Quantity));

        var command = new UpdateRepairTaskCommand
        (
            repairTaskId,
            request.Name,
            request.LaborCost,
            (RepairDurationInMinutes)request.EstimatedDurationInMins,
            parts
        );

        var result = await sender.Send(command, cancellationToken);

        return result.Match(response => NoContent(), Problem);
    }



    [HttpDelete("{repairTaskId:guid}")]
    [Authorize(Roles = nameof(Role.Manager))]
    [ProducesResponseType(typeof(RepairTaskDto), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Removes a repair task.")]
    [EndpointDescription("Deletes the specified repair task from the system.")]
    [EndpointName("RemoveRepairTask")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> Delete(Guid repairTaskId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveRepairTaskCommand(repairTaskId), cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

}