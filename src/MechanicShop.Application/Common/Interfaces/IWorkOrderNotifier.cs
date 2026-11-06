namespace MechanicShop.Application.Features.WorkOrders.EventHandlers;

public interface IWorkOrderNotifier
{
    Task NotifyWorkOrdersChangeAsync(CancellationToken cancellationToken = default);
}