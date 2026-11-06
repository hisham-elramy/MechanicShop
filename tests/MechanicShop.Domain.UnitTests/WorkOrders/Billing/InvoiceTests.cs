using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Tests.Common.Billing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MechanicShop.Domain.UnitTests.WorkOrders.Billing;

public class InvoiceTests
{
    [Fact]
    public void CreateInvoice_ShouldSuccess_WithValidData()
    {
        // Arrange
        var id = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var items = new List<InvoiceLineItem>
        {
            InvoiceLineItemFactory.CreateInvoiceLineItem().Value
        };

        var now = new DateTimeOffset(
            2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var time = new FakeTimeProvider(now);

        // Act
        var result = InvoiceFactory.CreateInvoice(
            id: id,
            workOrderId: workOrderId,
            items: items,
            discount: 10,
            taxAmount: 5,
            timeProvider: time);

        // Assert
        Assert.True(result.IsSuccess);

        var invoice = result.Value;

        Assert.Equal(id, invoice.Id);
        Assert.Equal(workOrderId, invoice.WorkOrderId);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Equal(10, invoice.DiscountAmount);
        Assert.Equal(5, invoice.TaxAmount);
        Assert.Equal(100, invoice.Subtotal);
        Assert.Equal(95, invoice.Total);
        Assert.Equal(now, invoice.IssuedAtUtc);
    }

    [Fact]
    public void CreateInvoice_ShouldFail_WithEmptyItems()
    {
        // Arrange
        List<InvoiceLineItem> items = [];

        // Act
        var result = InvoiceFactory.CreateInvoice(items: items);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            InvoiceErrors.LineItemsEmpty.Code,
            result.TopError.Code);
    }

    [Fact]
    public void ApplyDiscount_ShouldFail_WhenPaid()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;

        var paidAt = new DateTimeOffset(
            2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.True(invoice.MarkAsPaid(paidAt).IsSuccess);

        // Act
        var result = invoice.ApplyDiscount(10);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            InvoiceErrors.InvoiceLocked.Code,
            result.TopError.Code);
    }

    [Fact]
    public void ApplyDiscount_ShouldFail_WithNegativeAmount()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;

        // Act
        var result = invoice.ApplyDiscount(-10);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            InvoiceErrors.DiscountNegative.Code,
            result.TopError.Code);
    }

    [Fact]
    public void ApplyDiscount_ShouldFail_GreaterThanSubtotal()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;
        var excessiveDiscount = invoice.Subtotal + 1;

        // Act
        var result = invoice.ApplyDiscount(excessiveDiscount);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            InvoiceErrors.DiscountExceedsSubtotal.Code,
            result.TopError.Code);
    }

    [Fact]
    public void ApplyDiscount_ShouldSucceed_ValidAmount()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;
        const decimal validDiscount = 20m;

        // Act
        var result = invoice.ApplyDiscount(validDiscount);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(validDiscount, invoice.DiscountAmount);
    }

    [Fact]
    public void MarkAsPaid_ShouldSucceed_WhenUnpaid()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;

        var now = new DateTimeOffset(
            2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var time = new FakeTimeProvider(now);

        // Act
        var result = invoice.MarkAsPaid(time.GetUtcNow());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(now, invoice.PaidAt);
    }

    [Fact]
    public void MarkAsPaid_ShouldFail_WhenAlreadyPaid()
    {
        // Arrange
        var invoice = InvoiceFactory.CreateInvoice().Value;

        var firstPaidAt = new DateTimeOffset(
            2024, 1, 1, 9, 0, 0, TimeSpan.Zero);

        var secondPaidAt = firstPaidAt.AddHours(5);

        Assert.True(invoice.MarkAsPaid(firstPaidAt).IsSuccess);

        // Act
        var result = invoice.MarkAsPaid(secondPaidAt);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(
            InvoiceErrors.InvoiceLocked.Code,
            result.TopError.Code);

        Assert.Equal(firstPaidAt, invoice.PaidAt);
    }
}