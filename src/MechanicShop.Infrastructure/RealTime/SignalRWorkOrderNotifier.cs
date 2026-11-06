using MechanicShop.Application.Features.WorkOrders.EventHandlers;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

public sealed class SignalRWorkOrderNotifier(
    IHubContext<WorkOrderHub> hubContext

) : IWorkOrderNotifier
{
    private readonly IHubContext<WorkOrderHub> _hubContext = hubContext;

    public Task NotifyWorkOrdersChangeAsync(CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("WorkOrdersChanged", cancellationToken);

}