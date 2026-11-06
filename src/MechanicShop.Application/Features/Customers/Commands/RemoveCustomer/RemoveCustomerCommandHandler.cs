using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public sealed class RemoveCustomerCommandHandler(
    IAppDbContext context,
    ILogger<RemoveCustomerCommandHandler> logger,
    HybridCache cache

) : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveCustomerCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Customer with id {CustomerId} not found for deletion.", request.CustomerId);

            return ApplicationErrors.CustomerNotFound;
        }

        var hasAssociatedWorkOrders = await _context.WorkOrders
            .AsNoTracking()
            .AnyAsync(wo => wo.Vehicle!.CustomerId == request.CustomerId, cancellationToken);

        if (hasAssociatedWorkOrders)
        {
            _logger.LogWarning
            (
                "Customer {CustomerId} cannot be deleted because they have associated work orders (past, scheduled, or in-progress).",
                request.CustomerId
            );

            return CustomerErrors.CannotDeleteCustomerWithWorkOrders;
        }

        _context.Customers.Remove(customer);

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["customer", "customers"], cancellationToken);

        _logger.LogInformation
        (
            "Customer {CustomerId} deleted successfully.",
            request.CustomerId
        );

        return Result.Deleted;
    }
}