using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.RemoveRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RemoveRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();



    [Fact]
    public async Task Handle_ShouldSucceedAndDelete_WhenTaskIsNotInUse()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();

        // Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(task.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(await ExistsAsync(task.Id));
    }


    [Fact]
    public async Task Handle_ShouldNotDeleteOtherTasks_WhenDeletingOne()
    {
        // Arrange
        var target = await SeedRepairTaskAsync();
        var other = await SeedRepairTaskAsync();

        // Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(target.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(await ExistsAsync(other.Id));
    }


    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskIsUsedByAWorkOrder()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();
        await SeedWorkOrderWithTaskAsync(task);

        // Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(task.Id));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(RepairTaskErrors.InUse.Code, result.TopError.Code);
        Assert.True(await ExistsAsync(task.Id));
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldFail_WhenTaskIsUsedByAWorkOrderInAnyState(WorkOrderState state)
    {
        // Arrange
        var task = await SeedRepairTaskAsync();
        await SeedWorkOrderWithTaskAsync(task, state);

        // Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(task.Id));

        // Assert
        Assert.Equal(RepairTaskErrors.InUse.Code, result.TopError.Code);
        Assert.True(await ExistsAsync(task.Id));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDeletingTheSameTaskTwice()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();

        // Act & Assert
        Assert.True((await _mediator.Send(new RemoveRepairTaskCommand(task.Id))).IsSuccess);

        var second = await _mediator.Send(new RemoveRepairTaskCommand(task.Id));

        Assert.True(second.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, second.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldDeleteTask_WhenAnotherTaskIsUsedByAWorkOrder()
    {
        // Arrange
        var used = await SeedRepairTaskAsync();
        var unused = await SeedRepairTaskAsync();
        await SeedWorkOrderWithTaskAsync(used);

        // Act
        var result = await _mediator.Send(new RemoveRepairTaskCommand(unused.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(await ExistsAsync(used.Id));
    }


    [Fact]
    public async Task Handle_ShouldRefreshRepairTaskList_WhenTaskIsDeleted()
    {
        // Arrange
        var task = await SeedRepairTaskAsync();
        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("repair-tasks");

        // Act & Assert
        var before = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.True((await _mediator.Send(new RemoveRepairTaskCommand(task.Id))).IsSuccess);

        var after = (await _mediator.Send(new GetRepairTasksQuery())).Value.Count;
        Assert.True(after < before);
    }


    private async Task<RepairTask> SeedRepairTaskAsync()
    {
        var task = RepairTaskFactory.CreateRepairTask(name: $"Task-{Guid.NewGuid():N}").Value;

        _context.RepairTasks.Add(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        return task;
    }

    private async Task SeedWorkOrderWithTaskAsync(RepairTask task, WorkOrderState state = WorkOrderState.Scheduled)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;

        var day = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            spot: Spot.A,
            repairTasks: [task]
        ).Value;

        switch (state)
        {
            case WorkOrderState.InProgress:
                Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
                break;
            case WorkOrderState.Completed:
                Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
                Assert.True(workOrder.UpdateState(WorkOrderState.Completed).IsSuccess);
                break;
            case WorkOrderState.Cancelled:
                Assert.True(workOrder.Cancel().IsSuccess);
                break;
        }

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);
    }

    private Task<bool> ExistsAsync(Guid id)
    {
        return factory.CreateAppDbContext().RepairTasks.AnyAsync(rt => rt.Id == id);
    }
}