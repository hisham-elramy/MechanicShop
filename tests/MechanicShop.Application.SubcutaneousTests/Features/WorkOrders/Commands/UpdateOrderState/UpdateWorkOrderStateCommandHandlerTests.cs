using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateOrderState;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateOrderState;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateWorkOrderStateCommandHandlerTests
{
    private readonly WebAppFactory _factory;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _context;

    public UpdateWorkOrderStateCommandHandlerTests(WebAppFactory factory)
    {
        _factory = factory;
        _mediator = factory.CreateMediator();
        _context = factory.CreateAppDbContext();

        factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
    }

    public static TheoryData<WorkOrderState, WorkOrderState> ValidTransitions =>
    new()
    {
        { WorkOrderState.Scheduled,  WorkOrderState.InProgress },
        { WorkOrderState.InProgress, WorkOrderState.Completed },
        { WorkOrderState.Scheduled,  WorkOrderState.Cancelled },
        { WorkOrderState.InProgress, WorkOrderState.Cancelled },
        { WorkOrderState.Cancelled,  WorkOrderState.Cancelled },
    };

    public static TheoryData<WorkOrderState, WorkOrderState> InvalidTransitions
    {
        get
        {
            var valid = ValidTransitions.Select(row => ((WorkOrderState)row[0], (WorkOrderState)row[1])).ToHashSet();
            var data = new TheoryData<WorkOrderState, WorkOrderState>();

            foreach (var from in Enum.GetValues<WorkOrderState>())
            {
                foreach (var to in Enum.GetValues<WorkOrderState>())
                {
                    if (!valid.Contains((from, to)))
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }


    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public async Task Handle_ShouldSucceed_WhenTransitionIsValidAndStartTimeHasPassed(WorkOrderState from, WorkOrderState to)
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(from);
        _factory.FakeTime.SetUtcNow(workOrder.StartAtUtc.AddMinutes(1));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(workOrder.Id, to));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(to, (await ReloadAsync(workOrder.Id)).State);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExist()
    {
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(Guid.NewGuid(), WorkOrderState.InProgress));

        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public async Task Handle_ShouldFail_WhenTransitionIsInvalid(WorkOrderState from, WorkOrderState to)
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(from);
        _factory.FakeTime.SetUtcNow(workOrder.StartAtUtc.AddMinutes(1));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(workOrder.Id, to));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.InvalidStateTransition(from, to).Code, result.TopError.Code);
        Assert.Equal(from, (await ReloadAsync(workOrder.Id)).State);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStartTimeIsInFuture()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Scheduled);
        _factory.FakeTime.SetUtcNow(workOrder.StartAtUtc.AddMinutes(-1));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(workOrder.Id, WorkOrderState.InProgress));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc).Code, result.TopError.Code);
        Assert.Equal(WorkOrderState.Scheduled, (await ReloadAsync(workOrder.Id)).State);
    }

    [Fact]
    public async Task Handle_ShouldReturnTimeError_WhenStartTimeIsInFutureEvenIfTransitionIsInvalid()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Scheduled);
        _factory.FakeTime.SetUtcNow(workOrder.StartAtUtc.AddMinutes(-1));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(workOrder.Id, WorkOrderState.Completed));

        // Assert
        Assert.Equal(WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc).Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCancellingBeforeStartTime()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Scheduled);
        _factory.FakeTime.SetUtcNow(workOrder.StartAtUtc.AddMinutes(-1));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderStateCommand(workOrder.Id, WorkOrderState.Cancelled));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc).Code, result.TopError.Code);
    }


    private async Task<WorkOrder> SeedWorkOrderAsync(WorkOrderState state)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateEmployee().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var day = _factory.FakeTime.GetUtcNow().UtcDateTime.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            repairTasks: [repairTask]).Value;

        MoveTo(workOrder, state);

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
    private Task<WorkOrder> ReloadAsync(Guid id)
    {
        return _factory.CreateAppDbContext().WorkOrders.AsNoTracking().SingleAsync(w => w.Id == id);
    }
    private static void MoveTo(WorkOrder workOrder, WorkOrderState target)
    {
        switch (target)
        {
            case WorkOrderState.Scheduled:
                break;
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
    }
}