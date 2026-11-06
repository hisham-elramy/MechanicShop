using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateWorkOrderRepairTasksCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldSucceed_WhenTasksAreReplaced()
    {
        // Arrange
        var start = At(UniqueDay(), 10);
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, start);
        var newTask1 = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);
        var newTask2 = await SeedRepairTaskAsync(RepairDurationInMinutes.Min30);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [newTask1.Id, newTask2.Id]));

        // Assert
        Assert.True(result.IsSuccess);

        var saved = await ReloadAsync(workOrder.Id);
        Assert.Equal(start, saved.StartAtUtc);
        Assert.Equal(start.AddMinutes(90), saved.EndAtUtc);
        Assert.Equal(
            new[] { newTask1.Id, newTask2.Id }.OrderBy(x => x),
            saved.RepairTasks.Select(t => t.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNewEndTimeEqualsClosingTime()
    {
        // Arrange
        var start = At(UniqueDay(), 17);
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, start);
        var newTask = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);


        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [newTask.Id]));

        // Assert
        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var task = await SeedRepairTaskAsync(RepairDurationInMinutes.Min30);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [task.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRepairTaskIdsIsEmpty()
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, []));

        // Assert
        Assert.True(result.IsError);
        await AssertTasksUnchangedAsync(workOrder);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRepairTaskDoesNotExist()
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10));

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [Guid.NewGuid()]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(workOrder);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOnlySomeRepairTasksExist()
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10));
        var existing = await SeedRepairTaskAsync(RepairDurationInMinutes.Min30);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [existing.Id, Guid.NewGuid()]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.RepairTaskNotFound.Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(workOrder);
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldFail_WhenWorkOrderIsNotEditable(WorkOrderState state)
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10), state: state);
        var newTask = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [newTask.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.Readonly.Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(workOrder);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNewDurationEndsAfterClosingTime()
    {
        // Arrange
        var start = At(UniqueDay(), 17);
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, start);
        var task1 = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);
        var task2 = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(workOrder.Id, [task1.Id, task2.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderOutsideOperatingHour(start, start.AddMinutes(120)).Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(workOrder);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExtendedDurationOverlapsAnotherWorkOrderOnSameSpot()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, labor1) = await SeedPeopleAsync();
        var (vehicle2, labor2) = await SeedPeopleAsync();
        var target = await SeedWorkOrderAsync(vehicle1, labor1, Spot.A, At(day, 10));
        await SeedWorkOrderAsync(vehicle2, labor2, Spot.A, At(day, 10, 30));
        var longer = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(target.Id, [longer.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("MechanicShop_Spot_Full", result.TopError.Code);
        await AssertTasksUnchangedAsync(target);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExtendedDurationOverlapsSameLaborOnAnotherSpot()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, sharedLabor) = await SeedPeopleAsync();
        var (vehicle2, _) = await SeedPeopleAsync();
        var target = await SeedWorkOrderAsync(vehicle1, sharedLabor, Spot.A, At(day, 10));
        await SeedWorkOrderAsync(vehicle2, sharedLabor, Spot.B, At(day, 10, 30));
        var longer = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(target.Id, [longer.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.LaborOccupied.Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(target);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenExtendedDurationOverlapsSameVehicle()
    {
        // Arrange
        var day = UniqueDay();
        var (sharedVehicle, labor1) = await SeedPeopleAsync();
        var (_, labor2) = await SeedPeopleAsync();
        var target = await SeedWorkOrderAsync(sharedVehicle, labor1, Spot.A, At(day, 10));
        await SeedWorkOrderAsync(sharedVehicle, labor2, Spot.B, At(day, 10, 30));
        var longer = await SeedRepairTaskAsync(RepairDurationInMinutes.Min60);

        // Act
        var result = await _mediator.Send(new UpdateWorkOrderRepairTasksCommand(target.Id, [longer.Id]));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.VehicleSchedulingConflict.Code, result.TopError.Code);
        await AssertTasksUnchangedAsync(target);
    }




    private static DateTime UniqueDay()
    {
        return DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
    }

    private static DateTimeOffset At(DateTime day, int hour, int minute = 0)
    {
        return new(day.AddHours(hour).AddMinutes(minute), TimeSpan.Zero);
    }

    private async Task<(Guid VehicleId, Guid LaborId)> SeedPeopleAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateEmployee().Value;

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (customer.Vehicles.First().Id, employee.Id);
    }

    private async Task<RepairTask> SeedRepairTaskAsync(RepairDurationInMinutes duration)
    {
        var task = RepairTaskFactory.CreateRepairTask(repairDurationInMinutes: duration).Value;

        _context.RepairTasks.Add(task);
        await _context.SaveChangesAsync(CancellationToken.None);

        return task;
    }

    private async Task<WorkOrder> SeedWorkOrderAsync(Guid vehicleId, Guid laborId, Spot spot, DateTimeOffset start, RepairDurationInMinutes duration = RepairDurationInMinutes.Min30, WorkOrderState state = WorkOrderState.Scheduled)
    {
        var repairTask = RepairTaskFactory.CreateRepairTask(repairDurationInMinutes: duration).Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes((int)duration),
            laborId: laborId,
            spot: spot,
            repairTasks: [repairTask]).Value;


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

        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }

    private Task<WorkOrder> ReloadAsync(Guid id)
    {
        return factory.CreateAppDbContext().WorkOrders
                      .AsNoTracking()
                      .Include(w => w.RepairTasks)
                      .SingleAsync(w => w.Id == id);
    }

    private async Task AssertTasksUnchangedAsync(WorkOrder original)
    {
        var saved = await ReloadAsync(original.Id);
        Assert.Equal(original.StartAtUtc, saved.StartAtUtc);
        Assert.Equal(original.EndAtUtc, saved.EndAtUtc);
        Assert.Equal(
            original.RepairTasks.Select(t => t.Id).OrderBy(x => x),
            saved.RepairTasks.Select(t => t.Id).OrderBy(x => x));
    }
}