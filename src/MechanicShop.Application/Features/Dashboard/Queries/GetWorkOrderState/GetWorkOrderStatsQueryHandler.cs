using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Dashboard.Queries.GetWorkOrderState;

public sealed class GetWorkOrderStatsQueryHandler(
    IAppDbContext context

) : IRequestHandler<GetWorkOrderStatsQuery, Result<TodayWorkOrderStatsDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<TodayWorkOrderStatsDto>> Handle(GetWorkOrderStatsQuery request, CancellationToken cancellationToken)
    {
        var start = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        var stats = await _context.WorkOrders
            .AsNoTracking()
            .Include(wo => wo.Vehicle)
            .Include(wo => wo.RepairTasks)
                .ThenInclude(rt => rt.Parts)
            .Include(wo => wo.Invoice)
            .Where(wo => wo.StartAtUtc >= start && wo.EndAtUtc < end)
            .ToListAsync(cancellationToken);

        var total = stats.Count;

        if (total == 0)
        {
            return new TodayWorkOrderStatsDto
            {
                Date = request.Date,
                Total = 0,
                Scheduled = 0,
                InProgress = 0,
                Completed = 0,
                Cancelled = 0,
                TotalRevenue = 0,
                TotalPartsCost = 0,
                TotalLaborCost = 0,
                UniqueVehicles = 0,
                UniqueCustomers = 0
            };
        }

        var completed = stats.Count(x => x.State == WorkOrderState.Completed);
        var cancelled = stats.Count(x => x.State == WorkOrderState.Cancelled);

        var totalRevenue = stats.Sum(x => x.Invoice?.Total ?? 0);
        var totalPartCost = stats.Where(x => x.Invoice != null).Sum(x => x.TotalPartsCost ?? 0);
        var totalLaborCost = stats.Where(x => x.Invoice != null).Sum(x => x.TotalLaborCost ?? 0);
        var uniqueVehicles = stats.Select(x => x.VehicleId).Distinct().Count();
        var uniqueCustomers = stats.Where(x => x.Vehicle != null).Select(x => x.Vehicle!.CustomerId).Distinct().Count();

        var netProfit = totalRevenue - totalPartCost - totalLaborCost;

        return new TodayWorkOrderStatsDto
        {
            Date = request.Date,
            Total = total,
            Scheduled = stats.Count(x => x.State == WorkOrderState.Scheduled),
            InProgress = stats.Count(x => x.State == WorkOrderState.InProgress),
            Completed = completed,
            Cancelled = cancelled,
            TotalRevenue = totalRevenue,
            TotalPartsCost = totalPartCost,
            TotalLaborCost = totalLaborCost,
            UniqueVehicles = uniqueVehicles,
            UniqueCustomers = uniqueCustomers,
            NetProfit = netProfit,
            ProfitMargin = totalRevenue > 0 ? netProfit / totalRevenue * 100 : 0,
            CompletionRate = (decimal)completed / total * 100,
            AverageRevenuePerOrder = totalRevenue / total,
            OrdersPerVehicle = uniqueVehicles > 0 ? (decimal)total / uniqueVehicles : 0,
            PartsCostRatio = totalRevenue > 0 ? totalPartCost / totalRevenue * 100 : 0,
            LaborCostRatio = totalRevenue > 0 ? totalLaborCost / totalRevenue * 100 : 0,
            CancellationRate = (decimal)cancelled / total * 100
        };
    }
}