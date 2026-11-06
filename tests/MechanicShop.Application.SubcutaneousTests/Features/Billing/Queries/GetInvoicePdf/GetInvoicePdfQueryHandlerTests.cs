using System.Text;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoicePdf;


[Collection(WebAppFactoryCollection.CollectionName)]
public class GetInvoicePdfQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldReturnPdf_WhenInvoiceExists()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id));


Assert.True(
    result.IsSuccess,
    $"Expected success, but got: {result.TopError.Code} - {result.TopError.Description}"
);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Content);
        Assert.NotEmpty(result.Value.Content);
    }

    [Fact]
    public async Task Handle_ShouldReturnBytesStartingWithPdfHeader()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id));

        // Assert
        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.Value.Content!, 0, 4));
    }

    [Fact]
    public async Task Handle_ShouldNameTheFileAfterTheInvoice()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id));

        // Assert
        Assert.Equal($"invoice-{invoice.Id}.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task Handle_ShouldReturnPdfContentType()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id));

        // Assert
        Assert.Equal("application/pdf", result.Value.ContentType);
    }

    [Fact]
    public async Task Handle_ShouldReturnDifferentFileNames_ForDifferentInvoices()
    {
        // Arrange
        var first = await SeedInvoiceAsync();
        var second = await SeedInvoiceAsync();

        // Act
        var a = await _mediator.Send(new GetInvoicePdfQuery(first.Id));
        var b = await _mediator.Send(new GetInvoicePdfQuery(second.Id));

        // Assert
        Assert.NotEqual(a.Value.FileName, b.Value.FileName);
    }

    [Fact]
    public async Task Handle_ShouldReturnPdf_ForUnpaidInvoice()
    {
        // Arrange
        var invoice = await SeedInvoiceAsync();

        // Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id));

        // Assert
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.True(result.IsSuccess);
    }


    [Fact]
    public async Task Handle_ShouldFail_WhenInvoiceDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetInvoicePdfQuery(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.InvoiceNotFound.Code, result.TopError.Code);
    }


    private async Task<Invoice> SeedInvoiceAsync()
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
            workOrderId: workOrder.Id,
            items: [InvoiceLineItemFactory.CreateInvoiceLineItem().Value],
            discount: 0,
            taxAmount: 0
        ).Value;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(CancellationToken.None);

        return invoice;
    }
}