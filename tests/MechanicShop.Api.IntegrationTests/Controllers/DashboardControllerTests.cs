using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Security;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class DashboardControllerTests(WebAppFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/v1.0/dashboard/stats";

    [Fact]
    public async Task GetStats_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync($"{Url}?date=2031-01-01");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // DECISION TEST: revenue and profit are manager data. FAILS with the original (labor gets 200).
    [Fact]
    public async Task GetStats_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync($"{Url}?date={UniqueDate():yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_ForEmptyDay_ShouldReturnZeros()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var date = UniqueDate();

        var response = await Client.GetAsync($"{Url}?date={date:yyyy-MM-dd}");
        var dto = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(date, dto!.Date);
        Assert.Equal(0, dto.Total);
        Assert.Equal(0m, dto.TotalRevenue);
    }

    [Fact]
    public async Task GetStats_ShouldCountSeededWorkOrdersByState()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var date = UniqueDate();
        await SeedWorkOrderAsync(WorkOrderState.Scheduled, At(date, 9));
        await SeedWorkOrderAsync(WorkOrderState.InProgress, At(date, 10));
        await SeedWorkOrderAsync(WorkOrderState.Completed, At(date, 11));
        await SeedWorkOrderAsync(WorkOrderState.Cancelled, At(date, 12));

        var response = await Client.GetAsync($"{Url}?date={date:yyyy-MM-dd}");
        var dto = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, dto!.Total);
        Assert.Equal(1, dto.Scheduled);
        Assert.Equal(1, dto.InProgress);
        Assert.Equal(1, dto.Completed);
        Assert.Equal(1, dto.Cancelled);
    }

    [Fact]
    public async Task GetStats_ShouldIgnoreWorkOrdersOnOtherDays()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var date = UniqueDate();
        await SeedWorkOrderAsync(WorkOrderState.Scheduled, At(date.AddDays(1), 10));

        var dto = await (await Client.GetAsync($"{Url}?date={date:yyyy-MM-dd}"))
            .Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>();

        Assert.Equal(0, dto!.Total);
    }

    [Fact]
    public async Task GetStats_WithoutDate_ShouldDefaultToToday()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync(Url);
        var dto = await response.Content.ReadFromJsonAsync<TodayWorkOrderStatsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), dto!.Date);   // flaky for a few ms at midnight
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2031-13-45")]
    public async Task GetStats_WithInvalidDate_ShouldReturnBadRequest(string date)
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{Url}?date={date}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // FAILS with the original: the validator is internal and DateOnly.MaxValue.AddDays(1) throws (500)
    [Fact]
    public async Task GetStats_WithMaxDate_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{Url}?date=9999-12-31");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}