using Asp.Versioning;
using MechanicShop.Application.Features.Billing.Commands.IssueInvoice;
using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;
using MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Controllers;


[Route("api/v{version:apiVersion}/invoices")]
[ApiVersion("1.0")]
[ResponseCache(NoStore = true)]
[Authorize]
public sealed class InvoicesController(ISender sender) : ApiController
{

    [HttpPost("workorders/{workOrderId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Issues an invoice for a work order.")]
    [EndpointDescription("Creates a new invoice for the specified work order and returns the created invoice resource.")]
    [EndpointName("IssueInvoiceForWorkOrder")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> IssueInvoice(Guid workOrderId, CancellationToken cancellationToken)
    {
        var command = new IssueInvoiceCommand(workOrderId);

        var result = await sender.Send(command, cancellationToken);

        return result.Match
        (
            response => CreatedAtAction
            (
                nameof(GetInvoice),
                new { version = "1.0", invoiceId = response.InvoiceId },
                response
            ),
            Problem
        );
    }


    [HttpGet("{invoiceId:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Retrieves an invoice by ID.")]
    [EndpointDescription("Returns detailed information about the specified invoice. Only users with the Manager role are authorized")]
    [EndpointName("GetInvoice")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetInvoice(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(invoiceId), cancellationToken);

        return result.Match(response => Ok(response), Problem);
    }


    [HttpGet("{invoiceId:guid}/pdf")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [Produces("application/pdf")] 
    [EndpointSummary("Downloads the invoice as a PDF file.")]
    [EndpointDescription("Returns the invoice PDF file for the specified invoice ID. Only users with the Manager role are authorized.")]
    [EndpointGroupName("GetInvoicePdf")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetInvoicePdf(Guid invoiceId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoicePdfQuery(invoiceId), cancellationToken);

        return result.Match(response => File(response.Content!, "application/pdf", response.FileName), Problem);
    }


    [HttpPut("{invoiceId:guid}/payments")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [EndpointSummary("Marks an invoice as paid.")]
    [EndpointDescription("Settle the specified invoice. Only users with the Manager role are authorized to perform this operation.")]
    [EndpointGroupName("SettleInvoice")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SettleInvoice(Guid invoiceId, CancellationToken cancellationToken)
    {
        var command = new SettleInvoiceCommand(invoiceId);

        var result = await sender.Send(command, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}