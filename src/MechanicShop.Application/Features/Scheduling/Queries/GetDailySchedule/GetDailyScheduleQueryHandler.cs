using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Mappers;
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Application.Features.Scheduling.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.WorkOrders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Scheduling.Queries.GetDailySchedule;

public class GetDailyScheduleQueryHandler(
    IAppDbContext context,
    IAppClock clock

) : IRequestHandler<GetDailyScheduleQuery, Result<ScheduleDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly IAppClock _clock = clock;

    public async Task<Result<ScheduleDto>> Handle(GetDailyScheduleQuery request, CancellationToken cancellationToken)
    {
        var tz = request.TimeZone;

        var localStart = DateTime.SpecifyKind(request.ScheduleDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        var utcStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, tz), TimeSpan.Zero);
        var utcEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), tz), TimeSpan.Zero);

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where
            (
                wo => wo.StartAtUtc < utcEnd &&
                wo.EndAtUtc > utcStart &&
                (request.LaborId == null || wo.LaborId == request.LaborId)
            )
            .Include(wo => wo.RepairTasks)
            .Include(wo => wo.Vehicle)
            .Include(wo => wo.Labor)
            .ToListAsync(cancellationToken);

        var nowUtc = _clock.UtcNow;

        var result = new ScheduleDto
        {
            OnDate = request.ScheduleDate,
            EndOfDay = utcEnd < nowUtc,
            Spots = []
        };

        foreach (var spot in Enum.GetValues<Spot>())
        {
            var slots = new List<AvailabilitySlotDto>();

            var woBySpot = workOrders
                .Where(wo => wo.Spot == spot)
                .OrderBy(wo => wo.StartAtUtc)
                .ToList();

            for (var startUtc = utcStart; startUtc < utcEnd; startUtc = startUtc.AddMinutes(15))
            {
                var endUtc = startUtc.AddMinutes(15);
                var wo = woBySpot.FirstOrDefault(w => w.StartAtUtc < endUtc && w.EndAtUtc > startUtc);

                if (wo is not null)
                {
                    if (slots.All(s => s.WorkOrderId != wo.Id))
                    {
                        slots.Add(new AvailabilitySlotDto
                        {
                            WorkOrderId = wo.Id,
                            Spot = spot,
                            StartAt = wo.StartAtUtc,
                            EndAt = wo.EndAtUtc,
                            Vehicle = FormatVehicleInfo(wo.Vehicle),
                            Labor = wo.Labor?.ToDto(),
                            IsOccupied = true,
                            RepairTasks = [.. wo.RepairTasks.Select(rt => rt.ToDto())],
                            WorkOrderLocked = !wo.IsEditable,
                            State = wo.State,
                            IsAvailable = false
                        });
                    }
                }
                else
                {
                    slots.Add(new AvailabilitySlotDto
                    {
                        Spot = spot,
                        StartAt = startUtc,
                        EndAt = endUtc,
                        WorkOrderLocked = false,
                        IsAvailable = startUtc >= nowUtc
                    });
                }
            }

            result.Spots.Add
            (
                new SpotDto
                {
                    Spot = spot,
                    Slots = slots
                }
            );
        }

        return result;
    }

    private static string? FormatVehicleInfo(Vehicle? vehicle) =>
        vehicle != null ? $"{vehicle.Make} | {vehicle.LicensePlate}" : null;
}