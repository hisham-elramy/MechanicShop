using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Tests.Common.Billing;
using Xunit;

namespace MechanicShop.Domain.UnitTests.WorkOrders.Billing;

public class InvoiceLineItemTests
{
    [Fact]
    public void CreateInvoiceLineItem_ShouldSucceed_WithValidData()
    {
        var invoiceId = Guid.NewGuid();
        const int lineNumber = 1;
        const string description = "Brake Pad";
        const int quantity = 2;
        const decimal unitPrice = 50m;

        var result = InvoiceLineItemFactory.CreateInvoiceLineItem
        (
            id: invoiceId,
            lineNumber: lineNumber,
            description: description,
            quantity: quantity,
            unitPrice: unitPrice
        );

        Assert.True(result.IsSuccess);

        var item = result.Value;
        Assert.Equal(invoiceId, item.InvoiceId);
        Assert.Equal(lineNumber, item.LineNumber);
        Assert.Equal(description, item.Description);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(unitPrice, item.UnitPrice);
        Assert.Equal(100m, item.LineTotal);
    }

    [Fact]
    public void CreateInvoiceLineItem_ShouldFail_WithEmptyInvoiceId()
    {
        var result = InvoiceLineItemFactory.CreateInvoiceLineItem(id: Guid.Empty);

        Assert.True(result.IsError);
        Assert.Equal(InvoiceLineItemErrors.InvoiceIdRequired.Code, result.TopError.Code);
        Assert.Equal(InvoiceLineItemErrors.InvoiceIdRequired.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateInvoiceLineItem_ShouldFail_WithInvalidLineNumber()
    {
        var result = InvoiceLineItemFactory.CreateInvoiceLineItem(lineNumber: 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(InvoiceLineItemErrors.LineNumberInvalid.Code, result.TopError.Code);
        Assert.Equal(InvoiceLineItemErrors.LineNumberInvalid.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateInvoiceLineItem_ShouldFail_WithEmptyDescription()
    {
        var result = InvoiceLineItemFactory.CreateInvoiceLineItem(description: " ");

        Assert.True(result.IsError);
        Assert.Equal(InvoiceLineItemErrors.DescriptionRequired.Code, result.TopError.Code);
        Assert.Equal(InvoiceLineItemErrors.DescriptionRequired.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateInvoiceLineItem_ShouldFail_WithInvalidQuantity()
    {
        var result = InvoiceLineItemFactory.CreateInvoiceLineItem(quantity: 0);

        Assert.True(result.IsError);
        Assert.Equal(InvoiceLineItemErrors.QuantityInvalid.Code, result.TopError.Code);
        Assert.Equal(InvoiceLineItemErrors.QuantityInvalid.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateInvoiceLineItem_ShouldFail_WithInvalidUnitPrice()
    {
        var result = InvoiceLineItemFactory.CreateInvoiceLineItem(unitPrice: 0m);

        Assert.True(result.IsError);
        Assert.Equal(InvoiceLineItemErrors.UnitPriceInvalid.Code, result.TopError.Code);
        Assert.Equal(InvoiceLineItemErrors.UnitPriceInvalid.Description, result.TopError.Description);
    }

}