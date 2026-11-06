using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler(
    IAppDbContext context,
    ILogger<CreateCustomerCommandHandler> logger,
    HybridCache cache

) : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateCustomerCommandHandler> _logger = logger;
    private readonly HybridCache _cache = cache;

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _context.Customers
            .AnyAsync(c => c.Email!.ToLower() == email, cancellationToken);

        if (exists)
        {
            _logger.LogWarning("Customer creation aborted. Email already exists.");

            return CustomerErrors.CustomerExists;
        }

        List<Vehicle> vehicles = [];

        foreach(var v in request.Vehicles)
        {
            var vehicleResult = Vehicle.Create
            (
                id: Guid.NewGuid(),
                make: v.Make.Trim(),
                model: v.Model.Trim(),
                year: v.Year,
                licensePlate: v.LicensePlate.Trim()
            );

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }

            vehicles.Add(vehicleResult.Value);
        }

        var createCustomerResult = Customer.Create
        (
            id: Guid.NewGuid(),
            name: request.Name.Trim(),
            phoneNumber: request.PhoneNumber.Trim(),
            email: email,
            vehicles: vehicles
        );

        if (createCustomerResult.IsError)
        {
            return createCustomerResult.Errors;
        }

        var customer = createCustomerResult.Value;

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(["customer", "customers"], cancellationToken);

        _logger.LogInformation("Customer created successfully. Id: {customerId}", customer.Id);

        return customer.ToDto();
    }
}