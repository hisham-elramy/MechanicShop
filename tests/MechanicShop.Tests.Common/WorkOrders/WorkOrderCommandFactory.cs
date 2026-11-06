using MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Tests.Common.WorkOrders;

public static class WorkOrderCommandFactory
{
    public static CreateWorkOrderCommand CreateCreateWorkOrderCommand(Spot? spot = null, Guid? vehicleId = null, DateTimeOffset? startAt = null, List<Guid>? repairTaskIds = null, Guid? laborId = null)
    {
        return new CreateWorkOrderCommand
        (
            spot ?? Spot.A,
            vehicleId ?? Guid.NewGuid(),
            startAt ?? new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(9), TimeSpan.Zero),
            repairTaskIds ?? [Guid.NewGuid()],
            laborId ?? Guid.NewGuid()
        );
    }
}