
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Commands.RelocateWorkOrder;
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

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.RelocateWorkOrder;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RelocateWorkOrderCommandHandler(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNewSpotAndTimeAreFree()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, laborId) = await SeedPeopleAsync();

        var workOrder = await SeedWorkOrderAsync(
            vehicleId,
            laborId,
            Spot.A,
            At(day, 10),
            minutes: 30
        );

        var newStart = At(day, 14);

        // Act
        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(workOrder.Id, newStart, Spot.B)
        );

        // Assert
        Assert.True(result.IsSuccess);

        var saved = await ReloadAsync(workOrder.Id);

        Assert.Equal(Spot.B, saved.Spot);
        Assert.Equal(newStart, saved.StartAtUtc);
        Assert.Equal(newStart.AddMinutes(30), saved.EndAtUtc);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNewSlotOverlapsItsOwnCurrentSlot()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(day, 10), 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(workOrder.Id, At(day, 10, 15), Spot.A));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(At(day, 10, 15), (await ReloadAsync(workOrder.Id)).StartAtUtc);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenNewSlotStartsExactlyWhenAnotherEnds()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, labor1) = await SeedPeopleAsync();
        var (vehicle2, labor2) = await SeedPeopleAsync();

        await SeedWorkOrderAsync(vehicle1, labor1, Spot.B, At(day, 10), minutes: 60);
        var target = await SeedWorkOrderAsync(vehicle2, labor2, Spot.A, At(day, 14), minutes: 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(target.Id, At(day, 11), Spot.B));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExists()
    {
        // Arrange & Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(Guid.NewGuid(), At(UniqueDay(), 10), Spot.A));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNewSpotIsAlreadyTaken()
    {
        // Arrange 
        var day = UniqueDay();
        var (vehicle1, labor1) = await SeedPeopleAsync();
        var (vehicle2, labor2) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicle1, labor1, Spot.B, At(day, 10), minutes: 60);
        var target = await SeedWorkOrderAsync(vehicle2, labor2, Spot.A, At(day, 14), minutes: 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(target.Id, At(day, 10), Spot.B));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("MechanicShop_Spot_Full", result.TopError.Code);
        await AssertUnchangedAsync(target);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaborIsAlreadyOccupied()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicle1, sharedLabor) = await SeedPeopleAsync();
        var (vehicle2, _) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicle1, sharedLabor, Spot.B, At(day, 10), minutes: 60);
        var target = await SeedWorkOrderAsync(vehicle2, sharedLabor, Spot.A, At(day, 14), minutes: 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(target.Id, At(day, 10), Spot.C));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.LaborOccupied.Code, result.TopError.Code);
        await AssertUnchangedAsync(target);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenVehicleAlreadyHasOverlappingWorkOrder()
    {
        // Arrange
        var day = UniqueDay();
        var (sharedVehicle, labor1) = await SeedPeopleAsync();
        var (_, labor2) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(sharedVehicle, labor1, Spot.B, At(day, 10), minutes: 60);
        var target = await SeedWorkOrderAsync(sharedVehicle, labor2, Spot.A, At(day, 14), minutes: 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(target.Id, At(day, 10), Spot.C));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.VehicleSchedulingConflict.Code, result.TopError.Code);
        await AssertUnchangedAsync(target);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNewStartIsOutsideOperatingHours()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, laborId) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, laborId, Spot.A, At(day, 10), minutes: 60);

        // Act
        var result = await _mediator.Send(new RelocateWorkOrderCommand(workOrder.Id, At(day, 4), Spot.A));

        // Assert
        Assert.True(result.IsError);
        await AssertUnchangedAsync(workOrder);
    }


    private async Task AssertUnchangedAsync(WorkOrder original)
    {
        var saved = await ReloadAsync(original.Id);

        Assert.Equal(original.Spot, saved.Spot);
        Assert.Equal(original.StartAtUtc, saved.StartAtUtc);
        Assert.Equal(original.EndAtUtc, saved.EndAtUtc);
    }
    private Task<WorkOrder> ReloadAsync(Guid id)
    {
        return factory.CreateAppDbContext().WorkOrders.AsNoTracking().SingleAsync(wo => wo.Id == id);
    }
    private static DateTimeOffset At(DateTime day, int hour, int minutes = 0)
    {
        return new (day.AddHours(hour).AddMinutes(minutes), TimeSpan.Zero);
    }
    private async Task<WorkOrder> SeedWorkOrderAsync(Guid vehicleId, Guid laborId, Spot spot, DateTimeOffset start, int minutes)
    {
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes(minutes),
            laborId: laborId,
            spot: spot,
            repairTasks: [repairTask]
        ).Value;

        await _context.RepairTasks.AddAsync(repairTask);
        await _context.WorkOrders.AddAsync(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
    private async Task<(Guid VehicleId, Guid LaborId)> SeedPeopleAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateEmployee().Value;

        await _context.Customers.AddAsync(customer);
        await _context.Employees.AddAsync(employee);

        return (customer.Vehicles.First().Id, employee.Id);
    }
    private static DateTime UniqueDay()
    {
        return DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
    }
}