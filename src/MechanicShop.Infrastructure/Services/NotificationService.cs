using MechanicShop.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.Services;

public sealed class NotificationService(
    ILogger<NotificationService> logger

) : INotificationService
{
    private const string Message = "Your vehicle service is complete. You may collect it from the shop at your earliest convenience.";

    private readonly ILogger<NotificationService> _logger = logger;

    public async Task SendEmailAsync(string to, CancellationToken cancellationToken = default)
    {
        var at = to.IndexOf('@');
        var maskedEmail = at > 1
        ? to[0] + new string('*', at - 2) + to[at - 1] + to[at..] : "****";

        _logger.LogInformation("[Email] To {Email} | Message: {Message}", maskedEmail, Message);

        await Task.CompletedTask;
    }

    public async Task SendSmsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var masked = phoneNumber.Length >= 4
        ? new string('*', phoneNumber.Length - 4) + phoneNumber[^4..] : "****";

        _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", masked, Message);

        await Task.CompletedTask;
    }
}