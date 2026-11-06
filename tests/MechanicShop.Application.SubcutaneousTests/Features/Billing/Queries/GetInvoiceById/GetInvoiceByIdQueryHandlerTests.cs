using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;
using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoiceById;


[Collection(WebAppFactoryCollection.CollectionName)]
public class GetInvoiceByIdQueryHandlerTests
{
    private readonly WebAppFactory _factory;
    private readonly IMediator _mediator;
    private readonly IAppDbContext _context;

    public GetInvoiceByIdQueryHandlerTests(WebAppFactory factory)
    {
        _factory = factory;
        _mediator = factory.CreateMediator();
        _context = factory.CreateAppDbContext();

        factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedInvoice_WhenInvoiceExists()
    {
        // Arrange
        var (invoice, workOrder, _) = await SeedAsync();

        // Act
        var result = await _mediator.Send(new GetInvoiceByIdQuery(invoice.Id));

        // Assert
        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(invoice.Id, dto.InvoiceId);
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(invoice.Subtotal, dto.Subtotal);
        Assert.Equal(invoice.TaxAmount, dto.TaxAmount);
        Assert.Equal(invoice.Total, dto.Total);
    }

    [Fact]
    public async Task Handle_ShouldReturnCustomerAndVehicle_FromTheWorkOrder()
    {
        // Arrange
        var (invoice, workOrder, customer) = await SeedAsync();
        var vehicle = customer.Vehicles.First(v => v.Id == workOrder.VehicleId);

        // Act
        var dto = (await _mediator.Send(new GetInvoiceByIdQuery(invoice.Id))).Value;

        // Assert
        Assert.NotNull(dto.Customer);
        Assert.Equal(customer.Id, dto.Customer.CustomerId);
        Assert.Equal(customer.Name, dto.Customer.Name);

        Assert.NotNull(dto.Vehicle);
        Assert.Equal(vehicle.Id, dto.Vehicle.VehicleId);
        Assert.Equal(vehicle.Make, dto.Vehicle.Make);
        Assert.Equal(vehicle.LicensePlate, dto.Vehicle.LicensePlate);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllLineItems()
    {
        // Arrange
        var (invoice, _, _) = await SeedAsync();

        // Act
        var dto = (await _mediator.Send(new GetInvoiceByIdQuery(invoice.Id))).Value;

        // Assert
        Assert.Equal(invoice.LineItems.Count(), dto.Items.Count);
        Assert.All(dto.Items, i => Assert.Equal(invoice.Id, i.InvoiceId));
    }

    [Fact]
    public async Task Handle_ShouldReturnUnpaidStatus_ForNewInvoice()
    {
        // Arrange
        var (invoice, _, _) = await SeedAsync();

        // Act
        var dto = (await _mediator.Send(new GetInvoiceByIdQuery(invoice.Id))).Value;

        // Assert
        Assert.Equal(InvoiceStatus.Unpaid.ToString(), dto.PaymentStatus);
    }

    [Fact]
    public async Task Handle_ShouldNotReturnOtherInvoicesData()
    {
        // Arrange
        var (first, _, _) = await SeedAsync();
        var (second, _, _) = await SeedAsync();

        // Act
        var dto = (await _mediator.Send(new GetInvoiceByIdQuery(first.Id))).Value;

        // Assert
        Assert.Equal(first.Id, dto.InvoiceId);
        Assert.NotEqual(second.Id, dto.InvoiceId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenInvoiceDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetInvoiceByIdQuery(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.InvoiceNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedResult_UntilInvoiceTagIsRemoved()
    {
        // Arrange
        var (invoice, _, _) = await SeedAsync();
        var query = new GetInvoiceByIdQuery(invoice.Id);

        // Act & Assert
        Assert.True((await _mediator.Send(query)).IsSuccess);

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.True((await _mediator.Send(query)).IsSuccess);

        await _factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("invoice");

        var fresh = await _mediator.Send(query);
        Assert.True(fresh.IsError);
        Assert.Equal(ApplicationErrors.InvoiceNotFound.Code, fresh.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldShowPaidStatus_AfterInvoiceIsSettled()
    {
        // Arrange
        var (invoice, _, _) = await SeedAsync();
        var query = new GetInvoiceByIdQuery(invoice.Id);

        // Act & Assert
        Assert.Equal(InvoiceStatus.Unpaid.ToString(), (await _mediator.Send(query)).Value.PaymentStatus);   // cached

        Assert.True((await _mediator.Send(new SettleInvoiceCommand(invoice.Id))).IsSuccess);

        Assert.Equal(InvoiceStatus.Paid.ToString(), (await _mediator.Send(query)).Value.PaymentStatus);
    }

    [Fact]
    public async Task Handle_ShouldNotCacheNotFound_WhenInvoiceIsCreatedAfterwards()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();

        // Act & Assert
        Assert.True((await _mediator.Send(new GetInvoiceByIdQuery(invoiceId))).IsError);

        await SeedAsync(invoiceId);

        Assert.True((await _mediator.Send(new GetInvoiceByIdQuery(invoiceId))).IsSuccess);
    }


    private async Task<(Invoice Invoice, WorkOrder WorkOrder, Customer Customer)> SeedAsync(Guid? invoiceId = null)
    {
        var customer = CustomerFactory.CreateCustomer
        (
            name: $"Customer-{Guid.NewGuid():N}",
            email: $"c-{Guid.NewGuid():N}@example.com"
        ).Value;

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
            id: invoiceId,
            workOrderId: workOrder.Id,
            items: [InvoiceLineItemFactory.CreateInvoiceLineItem().Value],
            discount: 0,
            taxAmount: 0
        ).Value;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (invoice, workOrder, customer);
    }
}