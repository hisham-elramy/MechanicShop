using MechanicShop.Application.Features.Billing.Mappers;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class InvoiceMapperTests
{
    private static Invoice CreateInvoiceWithGraph(Guid? invoiceItemId = null)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicle = customer.Vehicles.First();
        vehicle.Customer = customer;

        var labor = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        var lineItem = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1,
            description: "Oil Change",
            quantity: 2,
            unitPrice: 50m).Value;

        var invoice = InvoiceFactory.CreateInvoice(
            workOrderId: workOrder.Id,
            items: [lineItem]).Value;

        invoice.WorkOrder = workOrder;
        workOrder.Invoice = invoice;

        return invoice;
    }

    [Fact]
    public void ToDto_Invoice_ShouldMapCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithGraph();
        var customer = invoice.WorkOrder!.Vehicle!.Customer!;
        var vehicle = invoice.WorkOrder.Vehicle;

        // Act
        var dto = invoice.ToDto();

        // Assert
        Assert.Equal(invoice.Id, dto.InvoiceId);
        Assert.Equal(invoice.WorkOrderId, dto.WorkOrderId);
        Assert.Equal(invoice.IssuedAtUtc, dto.IssuedAtUtc);
        Assert.Equal(invoice.Subtotal, dto.Subtotal);
        Assert.Equal(invoice.TaxAmount, dto.TaxAmount);
        Assert.Equal(invoice.DiscountAmount, dto.DiscountAmount);
        Assert.Equal(invoice.Total, dto.Total);
        Assert.Equal(invoice.Status.ToString(), dto.PaymentStatus);

        Assert.NotNull(dto.Customer);
        Assert.Equal(customer.Id, dto.Customer.CustomerId);
        Assert.Equal(customer.Name, dto.Customer.Name);
        Assert.Equal(customer.Email, dto.Customer.Email);

        Assert.NotNull(dto.Vehicle);
        Assert.Equal(vehicle.Id, dto.Vehicle.VehicleId);
        Assert.Equal(vehicle.LicensePlate, dto.Vehicle.LicensePlate);

        Assert.Single(dto.Items);
    }

    [Fact]
    public void ToDto_Invoice_ShouldMapLineItems()
    {
        // Arrange
        var invoice = CreateInvoiceWithGraph();
        var lineItem = invoice.LineItems.Single();

        // Act
        var dto = invoice.ToDto();

        // Assert
        var itemDto = Assert.Single(dto.Items);
        Assert.Equal(lineItem.InvoiceId, itemDto.InvoiceId);
        Assert.Equal(lineItem.LineNumber, itemDto.LineNumber);
        Assert.Equal(lineItem.Description, itemDto.Description);
        Assert.Equal(lineItem.Quantity, itemDto.Quantity);
        Assert.Equal(lineItem.UnitPrice, itemDto.UnitPrice);
        Assert.Equal(lineItem.LineTotal, itemDto.LineTotal);
    }

    [Fact]
    public void ToDto_Invoice_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        Invoice invoice = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => invoice.ToDto());
    }

    [Fact]
    public void ToDto_Invoice_WhenWorkOrderNotLoaded_ShouldThrowNullReferenceException()
    {
        var invoice = CreateInvoiceWithGraph();
        invoice.WorkOrder = null;

        Assert.Throws<NullReferenceException>(() => invoice.ToDto());
    }

    [Fact]
    public void ToDtos_Invoices_ShouldMapListCorrectly()
    {
        // Arrange
        var invoice1 = CreateInvoiceWithGraph();
        var invoice2 = CreateInvoiceWithGraph();
        var invoices = new List<Invoice> { invoice1, invoice2 };

        // Act
        var dtos = invoices.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(invoice1.Id, dtos[0].InvoiceId);
        Assert.Equal(invoice2.Id, dtos[1].InvoiceId);
    }

    [Fact]
    public void ToDtos_Invoices_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var invoices = new List<Invoice>();

        // Act
        var dtos = invoices.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDto_InvoiceLineItem_ShouldMapCorrectly()
    {
        // Arrange
        var lineItem = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 3,
            description: "Brake Pads",
            quantity: 4,
            unitPrice: 25m).Value;

        // Act
        var dto = lineItem.ToDto();

        // Assert
        Assert.Equal(lineItem.InvoiceId, dto.InvoiceId);
        Assert.Equal(3, dto.LineNumber);
        Assert.Equal("Brake Pads", dto.Description);
        Assert.Equal(4, dto.Quantity);
        Assert.Equal(25m, dto.UnitPrice);
        Assert.Equal(lineItem.LineTotal, dto.LineTotal);
    }

    [Fact]
    public void ToDtos_InvoiceLineItems_ShouldMapListCorrectly()
    {
        // Arrange
        var item1 = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1, description: "Oil Change", quantity: 1, unitPrice: 40m).Value;
        var item2 = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 2, description: "Brake Pads", quantity: 2, unitPrice: 30m).Value;
        var items = new List<InvoiceLineItem> { item1, item2 };

        // Act
        var dtos = items.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(1, dtos[0].LineNumber);
        Assert.Equal("Oil Change", dtos[0].Description);
        Assert.Equal(2, dtos[1].LineNumber);
        Assert.Equal("Brake Pads", dtos[1].Description);
    }

    [Fact]
    public void ToDtos_InvoiceLineItems_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var items = new List<InvoiceLineItem>();

        // Act
        var dtos = items.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }
}