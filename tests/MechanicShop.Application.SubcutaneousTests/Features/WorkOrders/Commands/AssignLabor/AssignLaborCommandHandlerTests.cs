using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;
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

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.AssignLabor;

[Collection(WebAppFactoryCollection.CollectionName)]
public class AssignLaborCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenLaborIsFree()
    {
        var (vehicleId, oldLabor) = await SeedPeopleAsync();
        var newLabor = await SeedLaborAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, oldLabor, Spot.A, At(UniqueDay(), 10));

        var result = await _mediator.Send(new AssignLaborCommand(workOrder.Id, newLabor));

        Assert.True(result.IsSuccess);
        Assert.Equal(newLabor, (await ReloadAsync(workOrder.Id)).LaborId);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNewLaborIsBusyBackToBack()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, labor1) = await SeedPeopleAsync();
        var (vehicle2, busyLabor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicle2, busyLabor, Spot.B, At(day, 9));
        var target = await SeedWorkOrderAsync(vehicle1, labor1, Spot.A, At(day, 9, 30));

        // Act
        var result = await _mediator.Send(new AssignLaborCommand(target.Id, busyLabor));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExist()
    {
        // Arrange
        var labor = await SeedLaborAsync();

        // Act
        var result = await _mediator.Send(new AssignLaborCommand(Guid.NewGuid(), labor));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaborDoesNotExist()
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10));

        // Act
        var result = await _mediator.Send(new AssignLaborCommand(workOrder.Id, Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.LaborNotFound.Code, result.TopError.Code);
        Assert.Equal(laborId, (await ReloadAsync(workOrder.Id)).LaborId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaborIsAlreadyOccupied()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, labor1) = await SeedPeopleAsync();
        var (vehicle2, busyLabor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicle2, busyLabor, Spot.B, At(day, 10));
        var target = await SeedWorkOrderAsync(vehicle1, labor1, Spot.A, At(day, 10));

        // Act
        var result = await _mediator.Send(new AssignLaborCommand(target.Id, busyLabor));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.LaborOccupied.Code, result.TopError.Code);
        Assert.Equal(labor1, (await ReloadAsync(target.Id)).LaborId);
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldFail_WhenWorkOrderIsNotEditable(WorkOrderState state)
    {
        // Arrange
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var newLabor = await SeedLaborAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(UniqueDay(), 10), state);

        // Act
        var result = await _mediator.Send(new AssignLaborCommand(workOrder.Id, newLabor));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.Readonly.Code, result.TopError.Code);
        Assert.Equal(laborId, (await ReloadAsync(workOrder.Id)).LaborId);
    }

    private static DateTime UniqueDay()
    {
        return DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
    }
    private static DateTimeOffset At(DateTime day, int hour, int minute = 0)
    {
        return new(day.AddHours(hour).AddMinutes(minute), TimeSpan.Zero);
    }
    private async Task<Guid> SeedLaborAsync()
    {
        var employee = EmployeeFactory.CreateLabor().Value;
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(CancellationToken.None);
        return employee.Id;
    }
    private async Task<(Guid VehicleId, Guid LaborId)> SeedPeopleAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (customer.Vehicles.First().Id, employee.Id);
    }
    private async Task<WorkOrder> SeedWorkOrderAsync(Guid vehicleId, Guid laborId, Spot spot, DateTimeOffset start, WorkOrderState state = WorkOrderState.Scheduled)
    {
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: laborId,
            spot: spot,
            repairTasks: [repairTask]
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

        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
    private Task<WorkOrder> ReloadAsync(Guid id)
    {
        return factory.CreateAppDbContext().WorkOrders.AsNoTracking().SingleAsync(w => w.Id == id);
    }

}