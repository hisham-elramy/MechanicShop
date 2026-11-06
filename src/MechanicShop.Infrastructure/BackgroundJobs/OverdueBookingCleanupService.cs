using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MechanicShop.Infrastructure.BackgroundJobs;

public class OverdueBookingCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<OverdueBookingCleanupService> logger,
    IOptions<AppSettings> options,
    IAppClock clock

) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<OverdueBookingCleanupService> _logger = logger;
    private readonly AppSettings _appSettings = options.Value;
    private readonly IAppClock _clock = clock;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_appSettings.OverdueBookingCleanupFrequencyMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("Checking overdue work orders at {Now}", _clock.UtcNow);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                var cutoff =_clock.UtcNow.AddMinutes(-_appSettings.BookingCancellationThresholdMinutes);
                var overdue = await db.WorkOrders
                    .Where(wo => wo.State == WorkOrderState.Scheduled && wo.StartAtUtc <= cutoff)
                    .ToListAsync();

                if (overdue.Count > 0)
                {
                    foreach (var wo in overdue)
                    {
                        var result = wo.Cancel();

                        if (result.IsError)
                        {
                            _logger.LogWarning("Failed to cancel WorkOrder {Id}: {Error}", wo.Id, result.Errors);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Cancelled {Count} overdue work orders", overdue.Count);
                }
                else
                {
                    _logger.LogInformation("No overdue work orders found.");
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up overdue work orders.");
            }
        }
    }
}