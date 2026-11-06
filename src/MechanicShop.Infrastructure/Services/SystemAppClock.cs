using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Infrastructure.Services;

public sealed class SystemAppClock(TimeProvider timeProvider) : IAppClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}