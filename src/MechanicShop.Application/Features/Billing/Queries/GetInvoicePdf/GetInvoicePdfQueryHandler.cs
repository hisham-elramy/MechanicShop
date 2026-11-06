using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;

public sealed class GetInvoicePdfQueryHandler(
    IAppDbContext context,
    ILogger<GetInvoicePdfQueryHandler> logger,
    IInvoicePdfGenerator pdfGenerator
)
: IRequestHandler<GetInvoicePdfQuery, Result<InvoicePdfDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<GetInvoicePdfQueryHandler> _logger = logger;
    private readonly IInvoicePdfGenerator _pdfGenerator = pdfGenerator;

    public async Task<Result<InvoicePdfDto>> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);;

        if (invoice is null)
        {
            _logger.LogWarning("Invoice not found. InvoiceId: {InvoiceId}", request.InvoiceId);

            return ApplicationErrors.InvoiceNotFound;
        }

        try
        {
            var pdfBytes = _pdfGenerator.Generate(invoice);

            var invoicePdf = new InvoicePdfDto
            {
                Content = pdfBytes,
                FileName = $"invoice-{invoice.Id}.pdf"
            };

            return invoicePdf;
        }
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "PDF generation failed for InvoiceId: {InvoiceId}",
        request.InvoiceId
    );

    throw;
}
    }
}