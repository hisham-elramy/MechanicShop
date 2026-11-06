using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MechanicShop.Infrastructure.Services;

public class WorkOrderPolicy(IOptions<AppSettings> options, IAppDbContext context) : IWorkOrderPolicy
{
    private readonly AppSettings _appSettings = options.Value;
    private readonly IAppDbContext _context = context;

    public async Task<Result<Success>> CheckSpotAvailabilityAsync(Spot spot, DateTimeOffset startAt, DateTimeOffset endAt, Guid? excludedWorkOrderId = null, CancellationToken cancellationToken = default)
    {
        var isOccupied = await _context.WorkOrders
            .AnyAsync
            (
                wo => wo.Spot == spot &&
                wo.StartAtUtc < endAt &&
                wo.EndAtUtc > startAt &&
                (!excludedWorkOrderId.HasValue || wo.Id != excludedWorkOrderId),
                cancellationToken
            );

        return isOccupied
            ? Error.Conflict("MechanicShop_Spot_Full", "The selected time slot is unavailable for the requested services.")
            : Result.Success;
    }

    public async Task<bool> IsLaborOccupied(Guid laborId, Guid excludedWorkOrderId, DateTimeOffset startAt, DateTimeOffset endAt)
    {
        var isOccupied = await _context.WorkOrders
            .AnyAsync
            (
                wo => wo.LaborId == laborId &&
                wo.StartAtUtc < endAt &&
                wo.EndAtUtc > startAt &&
                wo.Id != excludedWorkOrderId
            );

        return isOccupied;
    }

    public bool IsOutsideOperatingHours(DateTimeOffset startAt, TimeSpan duration)
    {
        var dayStart = new DateTimeOffset(startAt.Date, startAt.Offset);
        var opening = dayStart + _appSettings.OpeningTime.ToTimeSpan();
        var closing = dayStart + _appSettings.ClosingTime.ToTimeSpan();
        var endAt = startAt + duration;

        return startAt < opening || endAt > closing;
}
    public async Task<bool> IsVehicleAlreadyScheduled(Guid vehicleId, DateTimeOffset startAt, DateTimeOffset endAt, Guid? excludedWorkOrderId = null)
    {
        return await _context.WorkOrders
            .AnyAsync(wo => wo.VehicleId == vehicleId &&
                            wo.StartAtUtc < endAt &&
                            wo.EndAtUtc > startAt &&
                            (!excludedWorkOrderId.HasValue || wo.Id != excludedWorkOrderId));
    }
    public Result<Success> ValidateMinimumRequirements(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (endAt - startAt < TimeSpan.FromMinutes(_appSettings.MinimumAppointmentDurationInMinutes))
        {
            return Error.Conflict
            (
                "WorkOrder_TooShort",
                $"WorkOrder duration must be at least {_appSettings.MinimumAppointmentDurationInMinutes} minutes."
            );
        }

        return Result.Success;
    }
}