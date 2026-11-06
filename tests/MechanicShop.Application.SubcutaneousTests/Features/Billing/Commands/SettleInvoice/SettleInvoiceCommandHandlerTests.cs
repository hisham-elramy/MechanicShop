using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.SettleInvoice;


[Collection(WebAppFactoryCollection.CollectionName)]
public class SettleInvoiceCommandHandlerTests
{
    private readonly WebAppFactory _factory;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _context;

    public SettleInvoiceCommandHandlerTests(WebAppFactory factory)
    {
        _factory = factory;
        _mediator = factory.CreateMediator();
        _context = factory.CreateAppDbContext();

        factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldMarkInvoiceAsPaid_WhenInvoiceIsUnpaid()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, (await ReloadAsync(invoice.Id)).Status);
    }

    [Fact]
    public async Task Handle_ShouldSetPaidAtFromTheClock()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();
        var now = new DateTimeOffset(2031, 6, 6, 9, 30, 0, TimeSpan.Zero);
        _factory.FakeTime.SetUtcNow(now);

        // Act
        await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        // Assert
        Assert.Equal(now, (await ReloadAsync(invoice.Id)).PaidAt);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeAmounts_WhenInvoiceIsPaid()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        // Assert
        var saved = await ReloadAsync(invoice.Id);
        Assert.Equal(invoice.Subtotal, saved.Subtotal);
        Assert.Equal(invoice.Total, saved.Total);
    }

    [Fact]
    public async Task Handle_ShouldNotSettleOtherInvoices()
    {
        // Arrange
        var target = await SeedInvoiceAsync();
        var other = await SeedInvoiceAsync();

        // Act
        await _mediator.Send(new SettleInvoiceCommand(target.Id));

        // Assert
        var saved = await ReloadAsync(other.Id);
        Assert.Equal(InvoiceStatus.Unpaid, saved.Status);
        Assert.Null(saved.PaidAt);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenInvoiceDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new SettleInvoiceCommand(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.InvoiceNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenInvoiceIsAlreadyPaid()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act & Assert
        Assert.True((await _mediator.Send(new SettleInvoiceCommand(invoice.Id))).IsSuccess);
        var second = await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        Assert.True(second.IsError);
        Assert.Equal(InvoiceErrors.InvoiceLocked.Code, second.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldKeepOriginalPaidAt_WhenSettledTwice()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();
        var first = new DateTimeOffset(2031, 6, 6, 9, 0, 0, TimeSpan.Zero);
        _factory.FakeTime.SetUtcNow(first);

        // Act
        await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        _factory.FakeTime.SetUtcNow(first.AddHours(5));
        await _mediator.Send(new SettleInvoiceCommand(invoice.Id));

        // Assert
        Assert.Equal(first, (await ReloadAsync(invoice.Id)).PaidAt);
    }


    private async Task<Invoice> SeedInvoiceAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(name: $"T-{Guid.NewGuid():N}").Value;

        var day = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            spot: Spot.A,
            repairTasks: [repairTask]
        ).Value;

        Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
        Assert.True(workOrder.UpdateState(WorkOrderState.Completed).IsSuccess);

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        var invoice = InvoiceFactory.CreateInvoice
        (
            workOrderId: workOrder.Id,
            items: [InvoiceLineItemFactory.CreateInvoiceLineItem().Value],
            discount: 0,
            taxAmount: 0
        ).Value;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(CancellationToken.None);

        return invoice;
    }

    private Task<Invoice> ReloadAsync(Guid id)
    {
        return _factory.CreateAppDbContext().Invoices.AsNoTracking().SingleAsync(i => i.Id == id);
    }

}