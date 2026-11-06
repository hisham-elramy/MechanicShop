namespace MechanicShop.Application.Common.Interfaces;

public interface IAppClock
{
    DateTimeOffset UtcNow { get; }
}