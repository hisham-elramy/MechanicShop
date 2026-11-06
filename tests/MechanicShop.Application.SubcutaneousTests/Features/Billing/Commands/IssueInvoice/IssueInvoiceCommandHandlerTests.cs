using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Commands.IssueInvoice;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Common.Constants;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.IssueInvoice;

[Collection(WebAppFactoryCollection.CollectionName)]
public class IssueInvoiceCommandHandlerTests
{
    private readonly WebAppFactory _factory;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _context;

    public IssueInvoiceCommandHandlerTests(WebAppFactory factory)
    {
        _factory = factory;
        _mediator = factory.CreateMediator();
        _context = factory.CreateAppDbContext();

        factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
    }


    [Fact]
    public async Task Handle_ShouldIssueInvoice_WhenWorkOrderIsCompleted()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.InvoiceId);
        Assert.Equal(workOrder.Id, result.Value.WorkOrderId);

        var saved = await FindInvoiceAsync(workOrder.Id);
        Assert.NotNull(saved);
        Assert.Equal(result.Value.InvoiceId, saved.Id);
    }

    [Fact]
    public async Task Handle_ShouldCreateUnpaidInvoice()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);

        // Act
        await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        Assert.Equal(InvoiceStatus.Unpaid, (await FindInvoiceAsync(workOrder.Id))!.Status);
    }

    [Fact]
    public async Task Handle_ShouldUseTheClockForIssuedAt()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);
        var now = new DateTimeOffset(2031, 5, 5, 12, 0, 0, TimeSpan.Zero);
        _factory.FakeTime.SetUtcNow(now);

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        Assert.Equal(now, result.Value.IssuedAtUtc);
    }

    [Fact]
    public async Task Handle_ShouldCalculateTotals_FromTasksPartsAndQuantity()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed, TwoTasks());

        var saved = await _factory.CreateAppDbContext().WorkOrders
            .AsNoTracking()
            .Include(w => w.RepairTasks).ThenInclude(t => t.Parts)
            .SingleAsync(w => w.Id == workOrder.Id);

        Assert.Equal(2, saved.RepairTasks.Count());
        Assert.All(saved.RepairTasks, t => Assert.Single(t.Parts));

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        var dto = result.Value;
        var expectedTax = 400m * MechanicShopConstants.TaxRate;
        Assert.Equal(new[] { 200m, 200m }, dto.Items.Select(i => i.UnitPrice));
        Assert.Equal(expectedTax, dto.TaxAmount);
        Assert.Equal(400m + expectedTax, dto.Total);
        Assert.Equal(400m, dto.Subtotal);
    }

    [Fact]
    public async Task Handle_ShouldCreateOneLineItemPerRepairTask_InOrder()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed, TwoTasks());

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        var items = result.Value.Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.LineNumber));
        Assert.All(items, i => Assert.Equal(1, i.Quantity));
        Assert.All(items, i => Assert.Equal(200m, i.UnitPrice));
        Assert.All(items, i => Assert.Equal(200m, i.LineTotal));
        Assert.All(items, i => Assert.Equal(result.Value.InvoiceId, i.InvoiceId));
    }

    [Fact]
    public async Task Handle_ShouldIncludeTaskNameInDescription()
    {
        // Arrange
        var name = $"Task-{Guid.NewGuid():N}";
        var task = RepairTaskFactory.CreateRepairTask(name: name, laborCost: 100m).Value;
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed, [task]);

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        Assert.Contains(name, Assert.Single(result.Value.Items).Description);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new IssueInvoiceCommand(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Theory]
    [InlineData(WorkOrderState.Scheduled)]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldFail_WhenWorkOrderIsNotCompleted(WorkOrderState state)
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(state);

        // Act
        var result = await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderMustBeCompletedForInvoicing.Code, result.TopError.Code);
        Assert.Null(await FindInvoiceAsync(workOrder.Id));
    }

    [Fact]
    public async Task Handle_ShouldNotIssueInvoiceForOtherWorkOrders()
    {
        // Arrange
        var target = await SeedWorkOrderAsync(WorkOrderState.Completed);
        var other = await SeedWorkOrderAsync(WorkOrderState.Completed);

        // Act
        await _mediator.Send(new IssueInvoiceCommand(target.Id));

        // Assert
        Assert.Null(await FindInvoiceAsync(other.Id));
    }

    [Fact]
    public async Task Handle_ShouldRefreshWorkOrderQuery_WhenInvoiceIsIssued()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);

        // Act & Assert
        Assert.True((await _mediator.Send(new GetWorkOrderByIdQuery(workOrder.Id))).IsSuccess);   // cached
        await _mediator.Send(new IssueInvoiceCommand(workOrder.Id));

        var dto = (await _mediator.Send(new GetWorkOrderByIdQuery(workOrder.Id))).Value;
        Assert.NotNull(dto);
    }

    private static List<RepairTask> TwoTasks()
    {
        return [
            RepairTaskFactory.CreateRepairTask
            (
                name: $"T-{Guid.NewGuid():N}",
                laborCost: 100m,
                parts: [PartFactory.CreatePart(cost: 50m, quantity: 2).Value]
            ).Value,

            RepairTaskFactory.CreateRepairTask
            (
                name: $"T-{Guid.NewGuid():N}",
                laborCost: 100m,
                parts: [PartFactory.CreatePart(cost: 50m, quantity: 2).Value]
            ).Value,
        ];
    }

    private async Task<WorkOrder> SeedWorkOrderAsync(WorkOrderState state, List<RepairTask>? tasks = null)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;
        tasks ??= [RepairTaskFactory.CreateRepairTask(name: $"T-{Guid.NewGuid():N}").Value];

        var day = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            spot: Spot.A,
            repairTasks: tasks
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
        _context.RepairTasks.AddRange(tasks);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }

    private Task<Invoice?> FindInvoiceAsync(Guid workOrderId)
    {
        return _factory.CreateAppDbContext().Invoices
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.WorkOrderId == workOrderId);
    }

    private Task<int> CountInvoicesAsync(Guid workOrderId)
    {
        return _factory.CreateAppDbContext().Invoices.CountAsync(i => i.WorkOrderId == workOrderId);
    }

}