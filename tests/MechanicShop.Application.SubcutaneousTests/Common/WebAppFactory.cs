using MechanicShop.Api;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.BackgroundJobs;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public class WebAppFactory : WebApplicationFactory<IAssemblyMarker>, IAsyncLifetime
{
    private const string _sqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    public SettableTimeProvider FakeTime { get; } = new();
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder(_sqlServerImage).Build();
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        _ = Services;
    }
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    public IMediator CreateMediator()
    {
        var serviceScope = Services.CreateScope();

        return serviceScope.ServiceProvider.GetRequiredService<IMediator>();
    }

    public IAppDbContext CreateAppDbContext()
    {
        var serviceScope = Services.CreateScope();

        return serviceScope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices
        (
            services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<OverdueBookingCleanupService>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();

                services.RemoveAll<IAppClock>();
                services.AddSingleton<IAppClock>(FakeTime);

                services.AddDbContext<AppDbContext>((sp, options) =>
                {
                    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                    options.UseSqlServer(_dbContainer.GetConnectionString());
                });

                services.RemoveAll<AppSettings>();

                services.PostConfigure<AppSettings>
                (
                    options =>
                    {
                        options.OpeningTime = new TimeOnly(9, 0);
                        options.ClosingTime = new TimeOnly(18, 0);
                    }
                );
            }
        );
    }

}


public sealed class SettableTimeProvider : TimeProvider, IAppClock
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow => GetUtcNow();

    public void SetUtcNow(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan delta) => _now += delta;

    public void Reset() => _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}