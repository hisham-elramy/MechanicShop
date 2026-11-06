using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.EventHandlers;

public sealed class SendWorkOrderCompletedEmailHandler(
    INotificationService notificationService,
    IAppDbContext context,
    ILogger<SendWorkOrderCompletedEmailHandler> logger

) : INotificationHandler<WorkOrderCompleted>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly IAppDbContext _context = context;
    private readonly ILogger<SendWorkOrderCompletedEmailHandler> _logger = logger;

    public async Task Handle(WorkOrderCompleted notification, CancellationToken cancellationToken)
    {
        var workOrder = await _context.WorkOrders
            .Include(wo => wo.Vehicle!)
                .ThenInclude(v => v.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                wo => wo.Id == notification.WorkOrderId,
                cancellationToken
            );

        if (workOrder?.Vehicle?.Customer is not { } customer)
        {
            _logger.LogWarning(
                "Customer information not found for WorkOrder {WorkOrderId}.",
                notification.WorkOrderId
            );

            return;
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            await _notificationService.SendEmailAsync(
                customer.Email,
                cancellationToken
            );
        }

        if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            await _notificationService.SendSmsAsync(
                customer.PhoneNumber,
                cancellationToken
            );
        }
    }
}