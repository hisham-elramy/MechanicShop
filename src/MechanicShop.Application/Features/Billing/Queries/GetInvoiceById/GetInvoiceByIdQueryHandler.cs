using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Application.Features.Billing.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;

public sealed class GetInvoicePdfQueryHandler(
    IAppDbContext context,
    ILogger<GetInvoicePdfQueryHandler> logger

) : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetInvoicePdfQueryHandler> _logger = logger;

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.LineItems)
            .Include(i => i.WorkOrder!)
                .ThenInclude(wo => wo.Vehicle!)
                    .ThenInclude(v => v.Customer)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice not found. InvoiceId: {InvoiceId}", request.InvoiceId);
            return ApplicationErrors.InvoiceNotFound;
        }

        return invoice.ToDto();
    }
}