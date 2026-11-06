using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Scheduling.Queries.GetDailySchedule;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Scheduling.Queries.GetDailySchedule;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetDailyScheduleQueryHandlerTests
{
    private readonly WebAppFactory _factory;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _context;

    public GetDailyScheduleQueryHandlerTests(WebAppFactory factory)
    {
        _factory = factory;
        _mediator = factory.CreateMediator();
        _context = factory.CreateAppDbContext();

        factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
    }


    [Fact]
    public async Task Handle_ShouldReturnAllSpotsWith96Slots_WhenDayHasNoWorkOrders()
    {
        // Arrange & Act
        var result = await _mediator.Send(Query(UniqueDate(), laborId: Guid.NewGuid()));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Enum.GetValues<Spot>().Length, result.Value.Spots.Count);
        Assert.All(result.Value.Spots, s => Assert.Equal(96, s.Slots.Count));
        Assert.All(result.Value.Spots, s => Assert.All(s.Slots, slot => Assert.False(slot.IsOccupied)));
    }

    [Fact]
    public async Task Handle_ShouldReturnRequestedDate()
    {
        // Arrange
        var date = UniqueDate();

        // Act
        var result = await _mediator.Send(Query(date, laborId: Guid.NewGuid()));

        // Assert
        Assert.Equal(date, result.Value.OnDate);
    }

    [Fact]
    public async Task Handle_ShouldReturnContiguousNonOverlappingSlots()
    {
        // Arrange & Act
        var result = await _mediator.Send(Query(UniqueDate(), laborId: Guid.NewGuid()));

        // Assert
        var slots = result.Value.Spots.First().Slots;
        for (var i = 1; i < slots.Count; i++)
        {
            Assert.Equal(slots[i - 1].EndAt, slots[i].StartAt);
        }
    }


    [Fact]
    public async Task Handle_ShouldCollapseWorkOrderIntoOneSlot_WhenItSpansSeveralQuarterHours()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var workOrder = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 10), minutes: 60);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        var spotA = result.Value.Spots.Single(s => s.Spot == Spot.A);
        var occupied = Assert.Single(spotA.Slots, s => s.IsOccupied);
        Assert.Equal(workOrder.Id, occupied.WorkOrderId);
        Assert.Equal(workOrder.StartAtUtc, occupied.StartAt);
        Assert.Equal(workOrder.EndAtUtc, occupied.EndAt);
        Assert.False(occupied.IsAvailable);
        Assert.False(occupied.WorkOrderLocked);
        Assert.Equal(WorkOrderState.Scheduled, occupied.State);
        Assert.Equal(96 - 4 + 1, spotA.Slots.Count);
    }

    [Fact]
    public async Task Handle_ShouldNotAffectOtherSpots_WhenOneSpotIsOccupied()
    {
        // Arrabge
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 10), minutes: 60);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        foreach (var spot in result.Value.Spots.Where(s => s.Spot != Spot.A))
        {
            Assert.Equal(96, spot.Slots.Count);
            Assert.All(spot.Slots, s => Assert.False(s.IsOccupied));
        }
    }

    [Fact]
    public async Task Handle_ShouldKeepBackToDayOrdersSeparate_WhenTheyTouch()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicle1, labor) = await SeedPeopleAsync();
        var (vehicle2, _) = await SeedPeopleAsync();
        var first = await SeedWorkOrderAsync(vehicle1, labor, Spot.A, At(date, 10), minutes: 30);
        var second = await SeedWorkOrderAsync(vehicle2, labor, Spot.A, At(date, 10, 30), minutes: 30);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        var occupiedIds = result.Value.Spots.Single(s => s.Spot == Spot.A)
            .Slots.Where(s => s.IsOccupied).Select(s => s.WorkOrderId).ToList();
        Assert.Equal(new Guid?[] { first.Id, second.Id }, occupiedIds);
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldMarkWorkOrderLocked_WhenNotEditable(WorkOrderState state)
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 10), minutes: 30, state: state);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        var occupied = Assert.Single(result.Value.Spots.Single(s => s.Spot == Spot.A).Slots, s => s.IsOccupied);
        Assert.True(occupied.WorkOrderLocked);
        Assert.Equal(state, occupied.State);
    }


    [Fact]
    public async Task Handle_ShouldReturnOnlyRequestedLaborsOrders_WhenFilteringByLabor()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, laborA) = await SeedPeopleAsync();
        var (otherVehicle, laborB) = await SeedPeopleAsync();
        var mine = await SeedWorkOrderAsync(vehicleId, laborA, Spot.A, At(date, 10), minutes: 30);
        await SeedWorkOrderAsync(otherVehicle, laborB, Spot.B, At(date, 10), minutes: 30);

        // Act
        var result = await _mediator.Send(Query(date, laborA));

        // Assert
        var occupied = result.Value.Spots.SelectMany(s => s.Slots).Where(s => s.IsOccupied).ToList();
        Assert.Equal(mine.Id, Assert.Single(occupied).WorkOrderId);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOrdersOnOtherDays()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date.AddDays(1), 10), minutes: 30);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        Assert.All(result.Value.Spots, s => Assert.All(s.Slots, slot => Assert.False(slot.IsOccupied)));
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOrder_WhenItEndsExactlyAtMidnightStart()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 0).AddMinutes(-30), minutes: 30);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        Assert.All(result.Value.Spots, s => Assert.All(s.Slots, slot => Assert.False(slot.IsOccupied)));
    }

    [Fact]
    public async Task Handle_ShouldIncludeOrder_WhenItSpansMidnightIntoRequestedDay()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var overnight = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 0).AddMinutes(-30), minutes: 60);

        // Act
        var result = await _mediator.Send(Query(date, labor));

        // Assert
        var occupied = Assert.Single(result.Value.Spots.Single(s => s.Spot == Spot.A).Slots, s => s.IsOccupied);
        Assert.Equal(overnight.Id, occupied.WorkOrderId);
        Assert.Equal(overnight.StartAtUtc, occupied.StartAt);   // start is on the previous day (see error 6)
    }


    [Fact]
    public async Task Handle_ShouldMarkAllSlotsUnavailableAndEndOfDay_WhenDayIsInThePast()
    {
        // Arrange & Act
        var result = await _mediator.Send(Query(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)), Guid.NewGuid()));

        // Assert
        Assert.True(result.Value.EndOfDay);
        Assert.All(result.Value.Spots, s => Assert.All(s.Slots, slot => Assert.False(slot.IsAvailable)));
    }

    [Fact]
    public async Task Handle_ShouldMarkAllSlotsAvailableAndNotEndOfDay_WhenDayIsInTheFuture()
    {
        // Arrange & Act
        var result = await _mediator.Send(Query(UniqueDate(), Guid.NewGuid()));

        // Assert
        Assert.False(result.Value.EndOfDay);
        Assert.All(result.Value.Spots, s => Assert.All(s.Slots, slot => Assert.True(slot.IsAvailable)));
    }

    [Fact]
    public async Task Handle_ShouldMarkOnlyFutureSlotsAvailable_WhenDayIsToday()
    {
        // Arrange
        var date = UniqueDate();
        _factory.FakeTime.SetUtcNow(At(date, 12));

        // Act
        var result = await _mediator.Send(Query(date, Guid.NewGuid()));

        // Assert
        var slots = result.Value.Spots.First().Slots;
        Assert.All(slots.Where(s => s.StartAt < At(date, 12)), s => Assert.False(s.IsAvailable));
        Assert.All(slots.Where(s => s.StartAt >= At(date, 12)), s => Assert.True(s.IsAvailable));
        Assert.False(result.Value.EndOfDay);
    }

    [Fact]
    public async Task Handle_ShouldSetEndOfDay_WhenClockPassesMidnight()
    {
        // Arrange
        var date = UniqueDate();
        _factory.FakeTime.SetUtcNow(At(date.AddDays(1), 0).AddMinutes(1));

        // Act
        var result = await _mediator.Send(Query(date, Guid.NewGuid()));

        // Assert
        Assert.True(result.Value.EndOfDay);
    }


    [Fact]
    public async Task Handle_ShouldNotThrow_OnSpringForwardDay()
    {
        // Arrange
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var springForward = new DateOnly(2031, 3, 9);

        // Act
        var result = await _mediator.Send(new GetDailyScheduleQuery(newYork, springForward, Guid.NewGuid()));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Spots, s => Assert.Equal(92, s.Slots.Count));
    }

    [Fact]
    public async Task Handle_ShouldReturn100Slots_OnFallBackDay()
    {
        // Arrange
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var fallBack = new DateOnly(2030, 11, 3);

        // Act
        var result = await _mediator.Send(new GetDailyScheduleQuery(newYork, fallBack, Guid.NewGuid()));

        // Assert
        Assert.All(result.Value.Spots, s => Assert.Equal(100, s.Slots.Count));
    }

    [Fact]
    public async Task Handle_ShouldUseLocalDayBoundaries_WhenTimeZoneIsNotUtc()
    {
        // Arrange
        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var date = new DateOnly(2032, 1, 2);
        var (vehicleId, labor) = await SeedPeopleAsync();
        var order = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, new DateTimeOffset(2032, 1, 1, 23, 0, 0, TimeSpan.Zero), minutes: 30);

        // Act
        var result = await _mediator.Send(new GetDailyScheduleQuery(cairo, date, labor));

        // Assert
        var occupied = Assert.Single(result.Value.Spots.Single(s => s.Spot == Spot.A).Slots, s => s.IsOccupied);
        Assert.Equal(order.Id, occupied.WorkOrderId);
    }


    [Fact]
    public async Task Handle_ShouldServeCachedSchedule_UntilWorkOrderTagIsRemoved()
    {
        // Arrange
        var date = UniqueDate();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var query = Query(date, labor);

        // Act
        Assert.All((await _mediator.Send(query)).Value.Spots,
            s => Assert.All(s.Slots, slot => Assert.False(slot.IsOccupied)));

        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(date, 10), minutes: 30);

        Assert.All((await _mediator.Send(query)).Value.Spots,
            s => Assert.All(s.Slots, slot => Assert.False(slot.IsOccupied)));

            await _factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("work-order");

        // Assert
        Assert.Contains((await _mediator.Send(query)).Value.Spots.SelectMany(s => s.Slots), s => s.IsOccupied);
    }

    private static GetDailyScheduleQuery Query(DateOnly date, Guid? laborId)
    {
        return new(TimeZoneInfo.Utc, date, laborId);
    }

    private static DateOnly UniqueDate()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000)));
    }

    private static DateTimeOffset At(DateOnly date, int hour, int minute = 0)
    {
        return new(date.ToDateTime(TimeOnly.MinValue).AddHours(hour).AddMinutes(minute), TimeSpan.Zero);
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

    private async Task<WorkOrder> SeedWorkOrderAsync(
        Guid vehicleId, Guid laborId, Spot spot, DateTimeOffset start, int minutes,
        WorkOrderState state = WorkOrderState.Scheduled)
    {
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes(minutes),
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
}