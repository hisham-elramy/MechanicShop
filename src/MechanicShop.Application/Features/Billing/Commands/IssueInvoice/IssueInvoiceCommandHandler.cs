using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Application.Features.Billing.Mappers;
using MechanicShop.Domain.Common.Constants;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Billing.Commands.IssueInvoice;

public sealed class IssueInvoiceCommandHandler(
    IAppDbContext context,
    ILogger<IssueInvoiceCommandHandler> logger,
    HybridCache cache,
    IAppClock clock

) : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<IssueInvoiceCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IAppClock _clock = clock;

    public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var alreadyIssued = await _context.Invoices
            .AnyAsync(i => i.WorkOrderId == request.WorkOrderId, cancellationToken);

        if (alreadyIssued)
        {
            return ApplicationErrors.InvoiceAlreadyIssued;
        }
        var workOrder = await _context.WorkOrders
            .Include(wo => wo.Vehicle!)
                .ThenInclude(v => v.Customer)
            .Include(wo => wo.RepairTasks)
                .ThenInclude(rt => rt.Parts)
            .FirstOrDefaultAsync(wo => wo.Id == request.WorkOrderId);

        if (workOrder is null)
        {
            _logger.LogWarning("Invoice issuance failed. WorkOrder {WorkOrderId} not found.", request.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        if (workOrder.State != WorkOrderState.Completed)
        {
            _logger.LogWarning("Invoice issuance rejected. WorkOrder {WorkOrderId} is not in completed.", request.WorkOrderId);

            return ApplicationErrors.WorkOrderMustBeCompletedForInvoicing;
        }

        var invoiceId = Guid.NewGuid();

        var lineItems = new List<InvoiceLineItem>();

        var lineNumber = 1;

        foreach(var (task, taskIndex) in workOrder.RepairTasks.Select((t, i) => (t, i + 1)))
        {
            var partsSummary = task.Parts.Any() ?
                string.Join
                (
                    Environment.NewLine,
                    task.Parts.Select
                    (
                        p =>  $"    • {p.Name} x{p.Quantity} @ {p.Cost:C}"
                    )
                ) : "    • No parts";

            var lineDescription =
                $"{taskIndex}: {task.Name}{Environment.NewLine}" +
                $"  Labor = {task.LaborCost:C}{Environment.NewLine}" +
                $"  Parts:{Environment.NewLine}{partsSummary}";

            var totalPartsCost = task.Parts.Sum(p => p.Cost * p.Quantity);
            var totalTaskCost = task.LaborCost + totalPartsCost;

            var lineItemResult = InvoiceLineItem.Create
            (
                invoiceId: invoiceId,
                lineNumber: lineNumber,
                description: lineDescription,
                quantity: 1,
                unitPrice: totalTaskCost
            );

            if (lineItemResult.IsError)
            {
                return lineItemResult.Errors;
            }

            lineItems.Add(lineItemResult.Value);
            lineNumber++;
        }

        var subTotal = lineItems.Sum(li => li.LineTotal);

        var taxAmount = subTotal * MechanicShopConstants.TaxRate;

        var discountAmount = workOrder.Discount ?? 0m;

        var createInvoiceResult = Invoice.Create
        (
            id: invoiceId,
            workOrderId: workOrder.Id,
            items: lineItems,
            discountAmount: discountAmount,
            taxAmount: taxAmount,
            datetime: _clock.UtcNow
        );

        if (createInvoiceResult.IsError)
        {
            _logger.LogWarning
            (
                "Invoice creation failed for WorkOrderId: {WorkOrderId}. Errors: {@Errors}",
                request.WorkOrderId,
                createInvoiceResult.Errors
            );

            return createInvoiceResult.Errors;
        }

        var invoice = createInvoiceResult.Value;

        await _context.Invoices.AddAsync(invoice);

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["invoice", "work-order"], cancellationToken);

        _logger.LogInformation("Invoice {InvoiceId} issued for WorkOrder {WorkOrderId}.", invoice.Id, workOrder.Id);

        return invoice.ToDto();
    }
}