using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Dashboard.Queries.GetWorkOrderState;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrderStatsQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldReturnZeros_WhenDayHasNoWorkOrders()
    {
        // Arrange
        var date = UniqueDate();

        // Act
        var result = await _mediator.Send(new GetWorkOrderStatsQuery(date));

        // Assert
        Assert.True(result.IsSuccess);
        var s = result.Value;
        Assert.Equal(date, s.Date);
        Assert.Equal(0, s.Total);
        Assert.Equal(0, s.Scheduled + s.InProgress + s.Completed + s.Cancelled);
        Assert.Equal(0m, s.TotalRevenue);
        Assert.Equal(0m, s.NetProfit);
        Assert.Equal(0, s.UniqueVehicles);
        Assert.Equal(0, s.UniqueCustomers);
        Assert.Equal(0m, s.CompletionRate);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyOrdersStartingOnTheRequestedDate()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 10, vehicles[0], labor);
        await SeedOrderAsync(date.AddDays(1), 10, vehicles[0], labor);
        await SeedOrderAsync(date.AddDays(-1), 10, vehicles[0], labor);

        // Act
        var result = await _mediator.Send(new GetWorkOrderStatsQuery(date));

        // Assert
        Assert.Equal(1, result.Value.Total);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreOrderStartingYesterdayAndEndingToday()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date.AddDays(-1), 23, vehicles[0], labor, minute: 45, minutes: 60);

        // Act
        var result = await _mediator.Send(new GetWorkOrderStatsQuery(date));

        // Assert
        Assert.Equal(0, result.Value.Total);
    }


    [Fact]
    public async Task Handle_ShouldCountEachState()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor, WorkOrderState.Scheduled);
        await SeedOrderAsync(date, 10, vehicles[0], labor, WorkOrderState.InProgress);
        await SeedOrderAsync(date, 11, vehicles[0], labor, WorkOrderState.Completed);
        await SeedOrderAsync(date, 12, vehicles[0], labor, WorkOrderState.Cancelled);
        await SeedOrderAsync(date, 13, vehicles[0], labor, WorkOrderState.Completed);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(5, s.Total);
        Assert.Equal(1, s.Scheduled);
        Assert.Equal(1, s.InProgress);
        Assert.Equal(2, s.Completed);
        Assert.Equal(1, s.Cancelled);
        Assert.Equal(s.Total, s.Scheduled + s.InProgress + s.Completed + s.Cancelled);
    }

    [Fact]
    public async Task Handle_ShouldCalculateCompletionAndCancellationRates()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor, WorkOrderState.Scheduled);
        await SeedOrderAsync(date, 10, vehicles[0], labor, WorkOrderState.InProgress);
        await SeedOrderAsync(date, 11, vehicles[0], labor, WorkOrderState.Completed);
        await SeedOrderAsync(date, 12, vehicles[0], labor, WorkOrderState.Cancelled);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(25m, s.CompletionRate);
        Assert.Equal(25m, s.CancellationRate);
    }

    [Fact]
    public async Task Handle_ShouldCountUniqueVehiclesAndCustomers()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor);
        await SeedOrderAsync(date, 10, vehicles[0], labor);
        await SeedOrderAsync(date, 11, vehicles[1], labor);
        await SeedOrderAsync(date, 12, vehicles[1], labor);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(2, s.UniqueVehicles);
        Assert.Equal(1, s.UniqueCustomers);
        Assert.Equal(2m, s.OrdersPerVehicle);
    }

    [Fact]
    public async Task Handle_ShouldCountTwoCustomers_WhenVehiclesBelongToDifferentCustomers()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehiclesA) = await SeedBaseAsync();
        var (_, vehiclesB) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehiclesA[0], labor);
        await SeedOrderAsync(date, 10, vehiclesB[0], labor);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(2, s.UniqueCustomers);
    }

    [Fact]
    public async Task Handle_ShouldCalculateMoneyFields_FromInvoicedOrdersOnly()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor, WorkOrderState.Completed, invoiced: true);
        await SeedOrderAsync(date, 10, vehicles[0], labor, WorkOrderState.InProgress, invoiced: true);
        await SeedOrderAsync(date, 11, vehicles[0], labor, WorkOrderState.Scheduled);
        await SeedOrderAsync(date, 12, vehicles[0], labor, WorkOrderState.Scheduled);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(200m, s.TotalRevenue);
        Assert.Equal(100m, s.TotalPartsCost);
        Assert.Equal(200m, s.TotalLaborCost);
        Assert.Equal(-100m, s.NetProfit);
        Assert.Equal(-50m, s.ProfitMargin);
        Assert.Equal(50m, s.PartsCostRatio);
        Assert.Equal(100m, s.LaborCostRatio);
        Assert.Equal(50m, s.AverageRevenuePerOrder);
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroRatios_WhenNoOrderIsInvoiced()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(0m, s.TotalRevenue);
        Assert.Equal(0m, s.ProfitMargin);
        Assert.Equal(0m, s.PartsCostRatio);
        Assert.Equal(0m, s.LaborCostRatio);
        Assert.Equal(0m, s.NetProfit);
    }

    [Fact]
    public async Task Handle_ShouldIncludeTaxInRevenue()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor, invoiced: true, tax: 10m);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(110m, s.TotalRevenue);
    }

    [Fact]
    public async Task Handle_ShouldIncludeCancelledOrdersInvoiceInRevenue()
    {
        // Arrange
        var date = UniqueDate();
        var (labor, vehicles) = await SeedBaseAsync();
        await SeedOrderAsync(date, 9, vehicles[0], labor, WorkOrderState.Cancelled, invoiced: true);

        // Act
        var s = (await _mediator.Send(new GetWorkOrderStatsQuery(date))).Value;

        // Assert
        Assert.Equal(100m, s.TotalRevenue);
    }

    private static DateOnly UniqueDate()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 50000)));
    }

    private static DateTimeOffset At(DateOnly date, int hour, int minute = 0)
    {
        return new(date.ToDateTime(TimeOnly.MinValue).AddHours(hour).AddMinutes(minute), TimeSpan.Zero);
    }

    private async Task<(Guid LaborId, List<Guid> VehicleIds)> SeedBaseAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (employee.Id, customer.Vehicles.Select(v => v.Id).ToList());
    }

    private async Task<WorkOrder> SeedOrderAsync(
        DateOnly date, int hour, Guid vehicleId, Guid laborId,
        WorkOrderState state = WorkOrderState.Scheduled,
        bool invoiced = false, decimal tax = 0m, int minute = 0, int minutes = 30)
    {
        // Part cost 50 x quantity 1, labor 100: the parts cost is 50 with or without quantity
        var repairTask = RepairTaskFactory.CreateRepairTask
        (
            name: $"Task-{Guid.NewGuid():N}",
            laborCost: 100m,
            parts: [PartFactory.CreatePart(cost: 50m, quantity: 1).Value]
        ).Value;

        var start = At(date, hour, minute);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: vehicleId,
            startAt: start,
            endAt: start.AddMinutes(minutes),
            laborId: laborId,
            spot: Spot.A,
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

        if (invoiced)
        {
            var invoice = InvoiceFactory.CreateInvoice
            (
                workOrderId: workOrder.Id,
                items: [InvoiceLineItemFactory.CreateInvoiceLineItem().Value],   // subtotal 100
                discount: 0,
                taxAmount: tax
            ).Value;

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(CancellationToken.None);
        }

        return workOrder;
    }
}