using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RepairTasksControllerTests(WebAppFactory factory) : ApiTestBase(factory)
{
    private const string BaseUrl = "/api/v1.0/repair-tasks";
    private static readonly int Min30 = (int)RepairDurationInMinutes.Min30;

    // ---------- GET /repair-tasks ----------

    [Fact]
    public async Task GetRepairTasks_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRepairTasks_AsLabor_ShouldReturnOk()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<List<RepairTaskDto>>());
    }

    // FAILS with the original: create invalidates "repair-task", not "repair-tasks", so the cached list is stale
    [Fact]
    public async Task GetRepairTasks_AfterCreate_ShouldIncludeTheNewTask()
    {
        await AuthenticateAsync(TestUsers.Manager);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(BaseUrl)).StatusCode);   // warm the cache

        var created = await CreateAsync(UniqueName());

        var list = await (await Client.GetAsync(BaseUrl)).Content.ReadFromJsonAsync<List<RepairTaskDto>>();
        Assert.Contains(list!, t => t.RepairTaskId == created.RepairTaskId);
    }

    // FAILS with the original: same tag mismatch on delete
    [Fact]
    public async Task GetRepairTasks_AfterDelete_ShouldNotIncludeTheRemovedTask()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var created = await CreateAsync(UniqueName());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(BaseUrl)).StatusCode);   // warm the cache

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"{BaseUrl}/{created.RepairTaskId}")).StatusCode);

        var list = await (await Client.GetAsync(BaseUrl)).Content.ReadFromJsonAsync<List<RepairTaskDto>>();
        Assert.DoesNotContain(list!, t => t.RepairTaskId == created.RepairTaskId);
    }

    // ---------- GET /repair-tasks/{id} ----------

    [Fact]
    public async Task GetRepairTaskById_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRepairTaskById_WithValidId_ShouldReturnMappedTask()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var task = await SeedTaskAsync();

        var response = await Client.GetAsync($"{BaseUrl}/{task.Id}");
        var dto = await response.Content.ReadFromJsonAsync<RepairTaskDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(task.Id, dto!.RepairTaskId);
        Assert.Equal(task.Name, dto.Name);
        Assert.Equal(task.LaborCost, dto.LaborCost);
        Assert.Equal(task.Parts.Count(), dto.Parts.Count);
    }

    [Fact]
    public async Task GetRepairTaskById_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRepairTaskById_WithMalformedId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // MAY FAIL: depends on the validation pipeline mapping Guid.Empty to 400
    [Fact]
    public async Task GetRepairTaskById_WithEmptyGuid_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- POST /repair-tasks ----------

    [Fact]
    public async Task CreateRepairTask_WithValidRequest_ShouldReturnCreatedWithLocation()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName()));
        var dto = await response.Content.ReadFromJsonAsync<RepairTaskDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(dto);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(dto.RepairTaskId.ToString(), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRepairTask_ShouldPersistTaskAndParts()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var name = UniqueName();

        var created = await CreateAsync(name);

        var saved = await Factory.CreateAppDbContext().RepairTasks
            .AsNoTracking().Include(r => r.Parts).SingleAsync(r => r.Id == created.RepairTaskId);
        Assert.Equal(name, saved.Name);
        Assert.Equal(100m, saved.LaborCost);
        Assert.Equal("Brake Pad", Assert.Single(saved.Parts).Name);
    }

    [Fact]
    public async Task CreateRepairTask_ThenFollowLocation_ShouldReturnTheTask()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName()));

        var follow = await Client.GetAsync(response.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
    }

    // FAILS with the original: the name is not trimmed
    [Fact]
    public async Task CreateRepairTask_ShouldTrimTheName()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var name = UniqueName();

        var created = await CreateAsync($"  {name}  ");

        Assert.Equal(name, created.Name);
    }

    [Fact]
    public async Task CreateRepairTask_WithDuplicateName_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var name = UniqueName();
        await CreateAsync(name);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(name));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // MAY FAIL: depends on a case-insensitive collation
    [Fact]
    public async Task CreateRepairTask_WithNameDifferingOnlyByCase_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var name = UniqueName();
        await CreateAsync(name);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(name.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // FAILS with the original: LIKE treats '%' as a wildcard, so this reports a false duplicate
    [Fact]
    public async Task CreateRepairTask_WithWildcardInName_ShouldNotBeTreatedAsDuplicate()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var prefix = UniqueName();
        await CreateAsync($"{prefix}-A");

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody($"{prefix}%"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRepairTask_WithEmptyName_ShouldReturnBadRequest(string name)
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithNameLongerThan100_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(new string('a', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateRepairTask_WithNonPositiveLaborCost_ShouldReturnBadRequest(int cost)
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName(), laborCost: cost));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithoutDuration_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var body = new { name = UniqueName(), laborCost = 100, parts = new[] { new { name = "Pad", cost = 50, quantity = 1 } } };

        var response = await Client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithInvalidDuration_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName(), duration: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithNoParts_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var body = new { name = UniqueName(), laborCost = 100, estimatedDurationInMins = Min30, parts = Array.Empty<object>() };

        var response = await Client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Must not be a 500; a 400 comes either from model binding or from the validator
    [Fact]
    public async Task CreateRepairTask_WithMissingPartsProperty_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var body = new { name = UniqueName(), laborCost = 100, estimatedDurationInMins = Min30 };

        var response = await Client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithInvalidPart_ShouldReturnBadRequestAndPersistNothing()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var name = UniqueName();
        var body = new
        {
            name, laborCost = 100, estimatedDurationInMins = Min30,
            parts = new[] { new { name = "Pad", cost = 0, quantity = 1 } }
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await Factory.CreateAppDbContext().RepairTasks.AnyAsync(r => r.Name == name));
    }

    [Fact]
    public async Task CreateRepairTask_WithMalformedBody_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync(BaseUrl, "not-an-object");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepairTask_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueName()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- PUT /repair-tasks/{id} ----------

    // FAILS with the original: the action returns 200 OK instead of 204
    [Fact]
    public async Task UpdateRepairTask_WithValidRequest_ShouldReturnNoContent()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, name: UniqueName()));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTask_ShouldPersistTheChange()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();
        var newName = UniqueName();

        await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, name: newName, laborCost: 250));

        var saved = await Factory.CreateAppDbContext().RepairTasks.AsNoTracking().SingleAsync(r => r.Id == task.Id);
        Assert.Equal(newName, saved.Name);
        Assert.Equal(250m, saved.LaborCost);
    }

    // "repair-task" is invalidated on update, so the single-task query is fresh even with the original
    [Fact]
    public async Task UpdateRepairTask_ThenGetById_ShouldReturnTheNewName()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"{BaseUrl}/{task.Id}")).StatusCode);   // warm
        var newName = UniqueName();

        await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, name: newName));

        var dto = await (await Client.GetAsync($"{BaseUrl}/{task.Id}")).Content.ReadFromJsonAsync<RepairTaskDto>();
        Assert.Equal(newName, dto!.Name);
    }

    // FAILS with the original: "repair-tasks" is never invalidated on update
    [Fact]
    public async Task UpdateRepairTask_ThenGetList_ShouldShowTheNewName()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var created = await CreateAsync(UniqueName());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(BaseUrl)).StatusCode);   // warm
        var newName = UniqueName();
        var task = await Factory.CreateAppDbContext().RepairTasks.AsNoTracking()
            .Include(r => r.Parts).SingleAsync(r => r.Id == created.RepairTaskId);

        await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, name: newName));

        var list = await (await Client.GetAsync(BaseUrl)).Content.ReadFromJsonAsync<List<RepairTaskDto>>();
        Assert.Contains(list!, t => t.Name == newName);
    }

    [Fact]
    public async Task UpdateRepairTask_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", UpdateBody(task));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // FAILS with the original handler: no duplicate-name check on update
    [Fact]
    public async Task UpdateRepairTask_WithNameOfAnotherTask_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var first = await SeedTaskAsync();
        var second = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{second.Id}", UpdateBody(second, name: first.Name));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTask_WithSameName_ShouldNotConflictWithItself()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task UpdateRepairTask_WithLaborCostOutOfRange_ShouldReturnBadRequest(int cost)
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, laborCost: cost));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTask_WithInvalidDuration_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task, duration: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTask_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var task = await SeedTaskAsync();

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{task.Id}", UpdateBody(task));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRepairTask_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", UpdateBody(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- DELETE /repair-tasks/{id} ----------

    [Fact]
    public async Task DeleteRepairTask_WhenNotInUse_ShouldReturnNoContentAndRemoveIt()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();

        var response = await Client.DeleteAsync($"{BaseUrl}/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await Factory.CreateAppDbContext().RepairTasks.AnyAsync(r => r.Id == task.Id));
    }

    [Fact]
    public async Task DeleteRepairTask_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRepairTask_WhenUsedByAWorkOrder_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Scheduled);
        var taskId = workOrder.RepairTasks.First().Id;

        var response = await Client.DeleteAsync($"{BaseUrl}/{taskId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await Factory.CreateAppDbContext().RepairTasks.AnyAsync(r => r.Id == taskId));
    }

    [Fact]
    public async Task DeleteRepairTask_Twice_ShouldReturnNotFoundTheSecondTime()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var task = await SeedTaskAsync();
        await Client.DeleteAsync($"{BaseUrl}/{task.Id}");

        var second = await Client.DeleteAsync($"{BaseUrl}/{task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task DeleteRepairTask_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRepairTask_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- helpers ----------

    private static string UniqueName() => $"Task-{Guid.NewGuid():N}";

    private static object CreateBody(string name, int laborCost = 100, int? duration = null) => new
    {
        name,
        laborCost,
        estimatedDurationInMins = duration ?? Min30,
        parts = new[] { new { name = "Brake Pad", cost = 50, quantity = 2 } }
    };

    // Same values as the seeded task unless overridden; existing parts keep their ids
    private static object UpdateBody(RepairTask? task, string? name = null, int? laborCost = null, int? duration = null) => new
    {
        name = name ?? task?.Name ?? UniqueName(),
        laborCost = laborCost ?? (int)(task?.LaborCost ?? 100m),
        estimatedDurationInMins = duration ?? Min30,
        parts = task is null
            ? [new { partId = (Guid?)null, name = "Pad", cost = 50, quantity = 1 }]
            : task.Parts.Select(p => new { partId = (Guid?)p.Id, name = p.Name, cost = (int)p.Cost, quantity = p.Quantity }).ToArray()
    };

    private async Task<RepairTaskDto> CreateAsync(string name)
    {
        var response = await Client.PostAsJsonAsync(BaseUrl, CreateBody(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<RepairTaskDto>())!;
    }

    private async Task<RepairTask> SeedTaskAsync()
    {
        var task = RepairTaskFactory.CreateRepairTask(name: UniqueName()).Value;

        Context.RepairTasks.Add(task);
        await Context.SaveChangesAsync(CancellationToken.None);

        return task;
    }
}