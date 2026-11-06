using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Billing.Commands.SettleInvoice;

public sealed class SettleInvoiceCommandHandler(
    IAppDbContext context,
    ILogger<SettleInvoiceCommandHandler> logger,
    HybridCache cache,
    IAppClock clock
)
: IRequestHandler<SettleInvoiceCommand, Result<Success>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<SettleInvoiceCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;
    private readonly IAppClock _clock = clock;

    public async Task<Result<Success>> Handle(SettleInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found.", request.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        var payInvoiceResult = invoice.MarkAsPaid(_clock.UtcNow);

        if (payInvoiceResult.IsError)
        {
            _logger.LogWarning
            (
               "Invoice payment failed for InvoiceId: {InvoiceId}. Errors: {Errors}",
               invoice.Id,
               payInvoiceResult.Errors
            );

            return payInvoiceResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync("invoice", cancellationToken);

        _logger.LogInformation("Invoice {InvoiceId} successfully paid.", invoice.Id);

        return Result.Success;
    }
}