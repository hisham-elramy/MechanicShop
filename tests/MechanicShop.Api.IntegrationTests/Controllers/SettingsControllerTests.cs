using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Contracts.Responses;
using MechanicShop.Infrastructure.Settings;
using MechanicShop.Tests.Common.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class SettingsControllerTests(WebAppFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/settings/operating-hours";

    // Pins current behavior: the endpoint is public. Flip it if you add [Authorize].
    [Fact]
    public async Task GetOperatingHours_WithoutAuthentication_ShouldReturnOk()
    {
        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOperatingHours_AsLabor_ShouldReturnOk()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOperatingHours_ShouldReturnTheConfiguredHours()
    {
        var configured = Factory.Services.GetRequiredService<IOptions<AppSettings>>().Value;

        var dto = await (await Client.GetAsync(Url)).Content.ReadFromJsonAsync<OperatingHoursResponse>();

        Assert.Equal(configured.OpeningTime, dto!.OpeningTime);
        Assert.Equal(configured.ClosingTime, dto.ClosingTime);
    }

    [Fact]
    public async Task GetOperatingHours_ShouldOpenBeforeItCloses()
    {
        var dto = await (await Client.GetAsync(Url)).Content.ReadFromJsonAsync<OperatingHoursResponse>();

        Assert.True(dto!.OpeningTime < dto.ClosingTime);
    }

    // Needs PostConfigure<AppSettings> in the integration WebAppFactory (it is missing there today)
    [Fact]
    public async Task GetOperatingHours_ShouldReturnTheTestHours()
    {
        var dto = await (await Client.GetAsync(Url)).Content.ReadFromJsonAsync<OperatingHoursResponse>();

        Assert.Equal(new TimeOnly(9, 0), dto!.OpeningTime);
        Assert.Equal(new TimeOnly(18, 0), dto.ClosingTime);
    }
}