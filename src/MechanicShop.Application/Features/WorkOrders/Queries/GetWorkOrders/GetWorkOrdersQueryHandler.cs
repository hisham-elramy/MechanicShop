using System.Linq.Expressions;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

public sealed class GetWorkOrdersQueryHandler(
    IAppDbContext context

) : IRequestHandler<GetWorkOrdersQuery, Result<PaginatedList<WorkOrderListItemDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<WorkOrderListItemDto>>> Handle(GetWorkOrdersQuery query, CancellationToken cancellationToken)
    {
        var workOrdersQuery = _context.WorkOrders
            .AsNoTracking()
            .Include(wo => wo.Vehicle!)
                .ThenInclude(v => v.Customer)
            .Include(wo => wo.Labor)
            .Include(wo => wo.RepairTasks)
                .ThenInclude(rt => rt.Parts)
            .Include(wo => wo.Invoice)
            .AsQueryable();

        workOrdersQuery = ApplyFilters(workOrdersQuery, query);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            workOrdersQuery = ApplySearchTerm(workOrdersQuery, query.SearchTerm);
        }

        workOrdersQuery = ApplySorting(workOrdersQuery, query.SortColumn, query.SortDirection);

        var count = await workOrdersQuery.CountAsync(cancellationToken);

        var items = await workOrdersQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select
            (
                wo => new WorkOrderListItemDto
                {
                    WorkOrderId = wo.Id,
                    InvoiceId = wo.Invoice == null ? null : wo.Invoice.Id,
                    Spot = wo.Spot,
                    StartAtUtc = wo.StartAtUtc,
                    EndAtUtc = wo.EndAtUtc,
                    Vehicle = wo.Vehicle!.ToDto(),
                    Customer = wo.Vehicle!.Customer!.Name,
                    Labor = wo.Labor != null
                    ? wo.Labor.FirstName + " " + wo.Labor.LastName
                    : null,
                    State = wo.State,
                    RepairTasks = wo.RepairTasks.Select(rt => rt.Name).ToList()
                }
            )
            .ToListAsync(cancellationToken);

        return new PaginatedList<WorkOrderListItemDto>
        {
            Items = items,
            PageNumber = query.Page,
            PageSize = query.PageSize,
            TotalCount = count,
            TotalPages = (int)Math.Ceiling(count / (double)query.PageSize)
        };
    }

    private static IQueryable<WorkOrder> ApplyFilters(IQueryable<WorkOrder> query, GetWorkOrdersQuery searchQuery)
    {
        if (searchQuery.State.HasValue)
        {
            query = query.Where(wo => wo.State == searchQuery.State.Value);
        }

        if (searchQuery.VehicleId.HasValue && searchQuery.VehicleId != Guid.Empty)
        {
            query = query.Where(wo => wo.VehicleId == searchQuery.VehicleId.Value);
        }

        if (searchQuery.LaborId.HasValue && searchQuery.LaborId != Guid.Empty)
        {
            query = query.Where(wo => wo.LaborId == searchQuery.LaborId.Value);
        }

        if (searchQuery.StartDateFrom.HasValue)
        {
            query = query.Where(wo => wo.StartAtUtc >= searchQuery.StartDateFrom.Value);
        }

        if (searchQuery.StartDateTo.HasValue)
        {
            query = query.Where(wo => wo.StartAtUtc <= searchQuery.StartDateTo.Value);
        }

        if (searchQuery.EndDateFrom.HasValue)
        {
            query = query.Where(wo => wo.EndAtUtc >= searchQuery.EndDateFrom.Value);
        }

        if (searchQuery.EndDateTo.HasValue)
        {
            query = query.Where(wo => wo.EndAtUtc <= searchQuery.EndDateTo.Value);
        }

        if (searchQuery.Spot.HasValue)
        {
            query = query.Where(wo => wo.Spot == searchQuery.Spot.Value);
        }

        return query;
    }

    private static IQueryable<WorkOrder> ApplySearchTerm(IQueryable<WorkOrder> query, string searchTerm)
    {
        var normalized = searchTerm.Trim().ToLower();

        return query.Where
        (
            wo => (wo.Vehicle != null &&
            (
                wo.Vehicle.Make.ToLower().Contains(normalized) ||
                wo.Vehicle.Model.ToLower().Contains(normalized) ||
                wo.Vehicle.LicensePlate.ToLower().Contains(normalized)
            )) ||
            (wo.Labor != null &&
            (
                wo.Labor.FirstName.ToLower().Contains(normalized) ||
                wo.Labor.LastName.ToLower().Contains(normalized) ||
                (wo.Labor.FirstName + " " + wo.Labor.LastName).ToLower().Contains(normalized)
            )) ||
            wo.RepairTasks.Any(rt => rt.Name.ToLower().Contains(normalized)) ||
            wo.Id.ToString().ToLower().Contains(normalized)
        );
    }

    private static IOrderedQueryable<WorkOrder> Order<TKey>(IQueryable<WorkOrder> query, Expression<Func<WorkOrder, TKey>> key, bool desc)
    {
        return desc ? query.OrderByDescending(key) : query.OrderBy(key);
    }

    private static IQueryable<WorkOrder> ApplySorting(IQueryable<WorkOrder> query, string sortColumn, string sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var ordered = (sortColumn ?? string.Empty).ToLowerInvariant() switch
        {
            "createdat" => Order(query, wo => wo.CreatedAtUtc, desc),
            "updatedat" => Order(query, wo => wo.LastModifiedUtc, desc),
            "startat"   => Order(query, wo => wo.StartAtUtc, desc),
            "endat"     => Order(query, wo => wo.EndAtUtc, desc),
            "state"     => Order(query, wo => wo.State, desc),
            "spot"      => Order(query, wo => wo.Spot, desc),
            "total"     => Order(query, wo =>
                            wo.RepairTasks.Sum(rt => rt.LaborCost) +
                            wo.RepairTasks.SelectMany(rt => rt.Parts).Sum(p => p.Cost), desc),
            "vehicleid" => Order(query, wo => wo.VehicleId, desc),
            "laborid"   => Order(query, wo => wo.LaborId, desc),
            _           => Order(query, wo => wo.CreatedAtUtc, true)
        };

        return ordered.ThenBy(o => o.Id);
    }
}