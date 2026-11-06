using System.Net;
using System.Text.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.Security;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class LaborsControllerTests(WebAppFactory factory) : ApiTestBase(factory)
{
    private const string Url = "/api/v1.0/labors";

    [Fact]
    public async Task GetLabors_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLabors_AsManager_ShouldReturnAJsonArray()
    {
        await AuthenticateAsync(TestUsers.Manager);
        await ClearCacheAsync();

        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await ReadAsync(response)).ValueKind);
    }

    // DECISION TEST: the description says manager-only. FAILS with the original (a labor gets 200).
    [Fact]
    public async Task GetLabors_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLabors_ShouldIncludeANewlyAddedLabor()
    {
        await AuthenticateAsync(TestUsers.Manager);
        await ClearCacheAsync();
        var before = await CountAsync();

        await AddEmployeeAsync(EmployeeFactory.CreateLabor().Value);
        await ClearCacheAsync();

        Assert.Equal(before + 1, await CountAsync());
    }

    [Fact]
    public async Task GetLabors_ShouldExcludeManagers()
    {
        await AuthenticateAsync(TestUsers.Manager);
        await ClearCacheAsync();
        var before = await CountAsync();

        await AddEmployeeAsync(EmployeeFactory.CreateManager().Value);
        await ClearCacheAsync();

        Assert.Equal(before, await CountAsync());
    }

    // MAY FAIL: assumes the caching behavior wraps ICachedQuery requests
    [Fact]
    public async Task GetLabors_ShouldServeTheCachedList_UntilTheTagIsRemoved()
    {
        await AuthenticateAsync(TestUsers.Manager);
        await ClearCacheAsync();
        var first = await CountAsync();

        await AddEmployeeAsync(EmployeeFactory.CreateLabor().Value);   // direct insert: no invalidation
        Assert.Equal(first, await CountAsync());

        await ClearCacheAsync();
        Assert.Equal(first + 1, await CountAsync());
    }

    // ---------- helpers ----------

    private Task ClearCacheAsync() =>
        Factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("labors").AsTask();

    private async Task AddEmployeeAsync(MechanicShop.Domain.Employees.Employee employee)
    {
        Context.Employees.Add(employee);
        await Context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<int> CountAsync() =>
        (await ReadAsync(await Client.GetAsync(Url))).GetArrayLength();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}