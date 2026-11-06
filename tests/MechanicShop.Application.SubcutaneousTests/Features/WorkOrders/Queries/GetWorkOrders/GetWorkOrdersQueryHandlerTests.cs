using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
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

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrders;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrdersQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldReturnOnlyMatchingLabor_WhenFilteringByLaborId()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, laborA) = await SeedPeopleAsync();
        var laborB = await SeedLaborAsync();
        var a1 = await SeedWorkOrderAsync(vehicleId, laborA, Spot.A, At(day, 9));
        var a2 = await SeedWorkOrderAsync(vehicleId, laborA, Spot.A, At(day, 10));
        await SeedWorkOrderAsync(vehicleId, laborB, Spot.B, At(day, 9));

        // Act
        var result = await _mediator.Send(Query(laborA));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(
            new[] { a1.Id, a2.Id }.OrderBy(x => x),
            result.Value.Items!.Select(i => i.WorkOrderId).OrderBy(x => x));
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNothingMatches()
    {
        // Arrange & Act
        var result = await _mediator.Send(Query(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items!);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.TotalPages);
    }

    [Fact]
    public async Task Handle_ShouldFilterByState()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        var inProgress = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10), WorkOrderState.InProgress);

        // Act
        var result = await _mediator.Send(Query(labor, state: WorkOrderState.InProgress));

        // Assert
        Assert.Equal(inProgress.Id, Assert.Single(result.Value.Items!).WorkOrderId);
    }

    [Fact]
    public async Task Handle_ShouldFilterBySpot()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        var spotB = await SeedWorkOrderAsync(vehicleId, labor, Spot.B, At(day, 10));

        // Act
        var result = await _mediator.Send(Query(labor, spot: Spot.B));

        // Assert
        Assert.Equal(spotB.Id, Assert.Single(result.Value.Items!).WorkOrderId);
    }

    [Fact]
    public async Task Handle_ShouldFilterByStartDateRange_Inclusively()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        var inside1 = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10));
        var inside2 = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 12));
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 15));

        // Act
        var result = await _mediator.Send(Query(labor, startFrom: At(day, 10).UtcDateTime, startTo: At(day, 12).UtcDateTime));

        // Assert
        Assert.Equal
        (
            new[] { inside1.Id, inside2.Id }.OrderBy(x => x),
            result.Value.Items?.Select(i => i.WorkOrderId).OrderBy(x => x)
        );
    }

    [Fact]
    public async Task Handle_ShouldMatchRepairTaskName_WhenSearching()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var name = $"Tires-{Guid.NewGuid():N}"[..14];
        var match = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9), taskName: name);
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10));

        // Act
        var result = await _mediator.Send(Query(labor, search: name.ToUpperInvariant()));

        // Assert
        Assert.Equal(match.Id, Assert.Single(result.Value.Items!).WorkOrderId);
    }

    [Fact]
    public async Task Handle_ShouldMatchVehicleMake_WhenSearching()
    {
        // Arrange
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(UniqueDay(), 9));

        // Act
        var result = await _mediator.Send(Query(labor, search: "honda"));

        // Assert
        Assert.Single(result.Value.Items!);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenSearchMatchesNothing()
    {
        // Arrange
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(UniqueDay(), 9));

        // Act
        var result = await _mediator.Send(Query(labor, search: "no-such-thing-zzz"));

        // Assert
        Assert.Empty(result.Value.Items!);
    }

    [Fact]
    public async Task Handle_ShouldPage_WhenPageSizeIsSmallerThanTotal()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var first = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        var second = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10));
        var third = await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 11));

        // Act
        var page1 = await _mediator.Send(Query(labor, page: 1, pageSize: 2, sortColumn: "startAt", sortDirection: "asc"));
        var page2 = await _mediator.Send(Query(labor, page: 2, pageSize: 2, sortColumn: "startAt", sortDirection: "asc"));

        // Assert
        Assert.Equal(3, page1.Value.TotalCount);
        Assert.Equal(2, page1.Value.TotalPages);
        Assert.Equal(new[] { first.Id, second.Id }, page1.Value.Items?.Select(i => i.WorkOrderId));
        Assert.Equal(new[] { third.Id }, page2.Value.Items?.Select(i => i.WorkOrderId));
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyItems_WhenPageIsBeyondLastPage()
    {
        // Arrange
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(UniqueDay(), 9));

        // Act
        var result = await _mediator.Send(Query(labor, page: 5, pageSize: 10));

        // Assert
        Assert.Empty(result.Value.Items!);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task Handle_ShouldNotRepeatOrSkipRows_WhenSortingByColumnWithTies()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        for (var h = 9; h < 12; h++)
        {
            await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, h));   // all Scheduled = all tied
        }

        // Act
        var ids = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var r = await _mediator.Send(Query(labor, page: page, pageSize: 1, sortColumn: "state", sortDirection: "asc"));
            ids.AddRange(r.Value.Items!.Select(i => i.WorkOrderId));
        }

        // Assert
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task Handle_ShouldSortByStartAt(string direction)
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 11));
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10));

        // Act
        var result = await _mediator.Send(Query(labor, sortColumn: "startAt", sortDirection: direction));

        // Assert
        var starts = result.Value.Items?.Select(i => i.StartAtUtc).ToList();
        Assert.Equal(direction == "asc" ? starts?.OrderBy(s => s) : starts?.OrderByDescending(s => s), starts);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenSortingByTotal()
    {
        // Arrange
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(UniqueDay(), 9));

        // Act
        var result = await _mediator.Send(Query(labor, sortColumn: "total"));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToDefaultSort_WhenSortColumnIsUnknown()
    {
        // Arrange
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(UniqueDay(), 9));

        // Act
        var result = await _mediator.Send(Query(labor, sortColumn: "not-a-column"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items!);
    }

    [Fact]
    public async Task Handle_ShouldMapListItemFields()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        var name = $"Brakes-{Guid.NewGuid():N}"[..14];
        var workOrder = await SeedWorkOrderAsync(vehicleId, labor, Spot.C, At(day, 9), taskName: name);

        // Act
        var item = Assert.Single((await _mediator.Send(Query(labor))).Value.Items!);

        // Assert
        Assert.Equal(workOrder.Id, item.WorkOrderId);
        Assert.Equal(Spot.C, item.Spot);
        Assert.Equal(WorkOrderState.Scheduled, item.State);
        Assert.Equal(workOrder.StartAtUtc, item.StartAtUtc);
        Assert.Equal(workOrder.EndAtUtc, item.EndAtUtc);
        Assert.Equal("Customer #1", item.Customer);
        Assert.Equal("John Labor", item.Labor);
        Assert.Null(item.InvoiceId);
        Assert.Equal(name, Assert.Single(item.RepairTasks));
    }

    [Fact]
    public async Task Handle_ShouldServeCachedResult_UntilWorkOrderTagIsRemoved()
    {
        // Arrange
        var day = UniqueDay();
        var (vehicleId, labor) = await SeedPeopleAsync();
        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 9));
        var query = Query(labor);

        // Act & Assert
        Assert.Equal(1, (await _mediator.Send(query)).Value.TotalCount);

        await SeedWorkOrderAsync(vehicleId, labor, Spot.A, At(day, 10));
        Assert.Equal(1, (await _mediator.Send(query)).Value.TotalCount);

        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("work-order");
        Assert.Equal(2, (await _mediator.Send(query)).Value.TotalCount);
    }

    private static GetWorkOrdersQuery Query(
        Guid laborId, int page = 1, int pageSize = 10, string? search = null,
        string sortColumn = "createdAt", string sortDirection = "desc",
        WorkOrderState? state = null, Spot? spot = null,
        DateTime? startFrom = null, DateTime? startTo = null)
    {
        return new(page, pageSize, search, sortColumn, sortDirection,
            State: state, LaborId: laborId, Spot: spot,
            StartDateFrom: startFrom, StartDateTo: startTo);
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

    private async Task<WorkOrder> SeedWorkOrderAsync(
        Guid vehicleId, Guid laborId, Spot spot, DateTimeOffset start,
        WorkOrderState state = WorkOrderState.Scheduled, string? taskName = null)
    {
        var repairTask = RepairTaskFactory.CreateRepairTask(name: taskName).Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: laborId,
            spot: spot,
            repairTasks: [repairTask]
        ).Value;

        if (state == WorkOrderState.InProgress)
        {
            Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
        }

        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
}