using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(
    IAppDbContext context,
    ILogger<UpdateCustomerCommandHandler> logger,
    HybridCache cache

) : IRequestHandler<UpdateCustomerCommand, Result<Updated>>

{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<UpdateCustomerCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Updated>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Include(rt => rt.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Customer {CustomerId} not found for update.", request.CustomerId);

            return ApplicationErrors.CustomerNotFound;
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await _context.Customers
            .AnyAsync(c => c.Email == email && c.Id != request.CustomerId, cancellationToken);

        if (emailTaken)
        {
            return CustomerErrors.CustomerExists;
        }

        var existingIds = customer.Vehicles.Select(v => v.Id).ToHashSet();
        var givenIds = request.Vehicles.Where(v => v.VehicleId.HasValue).Select(v => v.VehicleId!.Value).ToList();

        if (givenIds.Any(id => !existingIds.Contains(id)) || givenIds.Distinct().Count() != givenIds.Count)
        {
            return Error.Validation("Customer.VehicleInvalid", "A vehicle id is unknown or duplicated.");
        }

        var validatedVehicles = new List<Vehicle>();

        foreach (var v in request.Vehicles)
        {
            var vehicleId = v.VehicleId ?? Guid.NewGuid();

            var vehicleResult = Vehicle.Create
            (
                id: vehicleId,
                make: v.Make.Trim(),
                model: v.Model.Trim(),
                year: v.Year,
                licensePlate: v.LicensePlate.Trim()
            );

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }

            validatedVehicles.Add(vehicleResult.Value);
        }

        var updatedCustomerResult = customer.Update
        (
            request.Name.Trim(),
            email,
            request.PhoneNumber.Trim()
        );

        if (updatedCustomerResult.IsError)
        {
            return updatedCustomerResult.Errors;
        }

        var upsertPartsResult = customer.UpsertParts(validatedVehicles);

        if (upsertPartsResult.IsError)
        {
            return updatedCustomerResult.Errors;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["customer", "customers"], cancellationToken);

        return Result.Updated;
    }
}