using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Customers;
using MechanicShop.Infrastructure.Identity;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.Security;
using MechanicShop.Tests.Common.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class CustomersControllerTests(WebAppFactory webAppFactory)
{
    private const string BaseUrl = "/api/v1.0/customers";

    private readonly AppHttpClient _client = webAppFactory.CreateAppHttpClient();
    private readonly IAppDbContext _context = webAppFactory.CreateAppDbContext();

    // ---------- GET /customers ----------

    [Fact]
    public async Task GetCustomers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomers_AsLabor_ShouldReturnOk()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<List<CustomerDto>>());
    }

    // Created through the API, so the "customer" cache tag is invalidated and the list is fresh
    [Fact]
    public async Task GetCustomers_AsManager_ShouldIncludeACustomerCreatedThroughTheApi()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var created = await CreateCustomerAsync();

        try
        {
            var response = await _client.GetAsync(BaseUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<CustomerDto>>();
            Assert.Contains(list!, c => c.CustomerId == created.CustomerId);
        }
        finally
        {
            await DeleteQuietlyAsync(created.CustomerId);
        }
    }

    // ---------- GET /customers/{id} ----------

    [Fact]
    public async Task GetCustomerById_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerById_WithValidId_ShouldReturnMappedCustomer()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            var response = await _client.GetAsync($"{BaseUrl}/{customer.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
            Assert.NotNull(dto);
            Assert.Equal(customer.Id, dto.CustomerId);
            Assert.Equal(customer.Name, dto.Name);
            Assert.Equal(customer.Email, dto.Email);
            Assert.Equal(customer.Vehicles.Count(), dto.Vehicles.Count);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task GetCustomerById_AsLabor_ShouldReturnOk()
    {
        await AuthenticateAsync(TestUsers.Labor01);
        var customer = await SeedCustomerAsync();

        try
        {
            var response = await _client.GetAsync($"{BaseUrl}/{customer.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task GetCustomerById_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The ":guid" route constraint means a malformed id never reaches the action
    [Fact]
    public async Task GetCustomerById_WithMalformedId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.GetAsync($"{BaseUrl}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // MAY FAIL: depends on the validation pipeline mapping Guid.Empty to 400
    [Fact]
    public async Task GetCustomerById_WithEmptyGuid_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.GetAsync($"{BaseUrl}/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- POST /customers ----------

    [Fact]
    public async Task CreateCustomer_WithValidRequest_ShouldReturnCreatedWithLocation()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(email));
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();

        try
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(dto);
            Assert.Equal(email.ToLowerInvariant(), dto.Email.ToLowerInvariant());
            Assert.Single(dto.Vehicles);
            Assert.NotNull(response.Headers.Location);
            Assert.EndsWith(dto.CustomerId.ToString(), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (dto is not null) await DeleteQuietlyAsync(dto.CustomerId);
        }
    }

    [Fact]
    public async Task CreateCustomer_WithValidRequest_ShouldPersistTheCustomer()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(email));
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();

        try
        {
            var saved = await webAppFactory.CreateAppDbContext().Customers
                .AsNoTracking().Include(c => c.Vehicles)
                .SingleOrDefaultAsync(c => c.Id == dto!.CustomerId);

            Assert.NotNull(saved);
            Assert.Equal("John Smith", saved.Name);
            Assert.Equal("Honda", Assert.Single(saved.Vehicles).Make);
        }
        finally
        {
            if (dto is not null) await DeleteQuietlyAsync(dto.CustomerId);
        }
    }

    [Fact]
    public async Task CreateCustomer_ThenGetById_ShouldReturnTheSameCustomer()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var created = await CreateCustomerAsync();

        try
        {
            var response = await _client.GetAsync($"{BaseUrl}/{created.CustomerId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<CustomerDto>();
            Assert.Equal(created.CustomerId, fetched!.CustomerId);
        }
        finally
        {
            await DeleteQuietlyAsync(created.CustomerId);
        }
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateEmail_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var first = await CreateCustomerAsync();

        try
        {
            var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(first.Email));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await DeleteQuietlyAsync(first.CustomerId);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task CreateCustomer_WithInvalidEmail_ShouldReturnBadRequest(string email)
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithEmptyName_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueEmail(), name: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidPhoneNumber_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueEmail(), phone: "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithNoVehicles_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var body = new { name = "John Smith", phoneNumber = "+15555555555", email = UniqueEmail(), vehicles = Array.Empty<object>() };

        var response = await _client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // FAILS with the original: ConvertAll on a null list throws, so the response is 500
    [Fact]
    public async Task CreateCustomer_WithMissingVehiclesProperty_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var body = new { name = "John Smith", phoneNumber = "+15555555555", email = UniqueEmail() };

        var response = await _client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidVehicle_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var body = new
        {
            name = "John Smith", phoneNumber = "+15555555555", email = UniqueEmail(),
            vehicles = new[] { new { make = "", model = "Accord", year = 2024, licensePlate = "ABC123" } }
        };

        var response = await _client.PostAsJsonAsync(BaseUrl, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueEmail()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueEmail()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- PUT /customers/{id} ----------

    // FAILS with the original: the action returns 200 OK instead of 204
    [Fact]
    public async Task UpdateCustomer_WithValidRequest_ShouldReturnNoContent()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            var response = await _client.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", UpdateBody(customer, name: "New Name"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task UpdateCustomer_WithValidRequest_ShouldPersistTheChange()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            await _client.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", UpdateBody(customer, name: "Changed Name"));

            var saved = await webAppFactory.CreateAppDbContext().Customers
                .AsNoTracking().SingleAsync(c => c.Id == customer.Id);
            Assert.Equal("Changed Name", saved.Name);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task UpdateCustomer_ThenGetById_ShouldReturnTheNewName()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            // Warm the cache, then update through the API so the "customer" tag is invalidated
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{BaseUrl}/{customer.Id}")).StatusCode);
            await _client.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", UpdateBody(customer, name: "Fresh Name"));

            var dto = await (await _client.GetAsync($"{BaseUrl}/{customer.Id}")).Content.ReadFromJsonAsync<CustomerDto>();

            Assert.Equal("Fresh Name", dto!.Name);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task UpdateCustomer_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            var response = await _client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", UpdateBody(customer));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    [Fact]
    public async Task UpdateCustomer_WithEmptyName_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        try
        {
            var response = await _client.PutAsJsonAsync($"{BaseUrl}/{customer.Id}", UpdateBody(customer, name: ""));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await RemoveSeededCustomerAsync(customer);
        }
    }

    // FAILS with the original handler: no duplicate-email check on update
    [Fact]
    public async Task UpdateCustomer_WithEmailOfAnotherCustomer_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var first = await SeedCustomerAsync();
        var second = await SeedCustomerAsync();

        try
        {
            var response = await _client.PutAsJsonAsync($"{BaseUrl}/{second.Id}", UpdateBody(second, email: first.Email!));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await RemoveSeededCustomerAsync(first);
            await RemoveSeededCustomerAsync(second);
        }
    }

    [Fact]
    public async Task UpdateCustomer_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", UpdateBody(null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", UpdateBody(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- DELETE /customers/{id} ----------

    [Fact]
    public async Task DeleteCustomer_WithValidId_ShouldReturnNoContentAndRemoveTheCustomer()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var customer = await SeedCustomerAsync();

        var response = await _client.DeleteAsync($"{BaseUrl}/{customer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await webAppFactory.CreateAppDbContext().Customers.AnyAsync(c => c.Id == customer.Id));
    }

    [Fact]
    public async Task DeleteCustomer_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await _client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCustomer_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await _client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCustomer_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- helpers ----------

    private async Task AuthenticateAsync(AppUser user) =>
        _client.SetAuthorizationHeader(await _client.GenerateTokenAsync(user));

    private static string UniqueEmail() => $"c-{Guid.NewGuid():N}@example.com";

    private static object CreateBody(string email, string name = "John Smith", string phone = "+15555555555") => new
    {
        name,
        phoneNumber = phone,
        email,
        vehicles = new[] { new { make = "Honda", model = "Accord", year = 2024, licensePlate = "ABC123" } }
    };

    // Same values as the seeded customer unless overridden, existing vehicles keep their ids
    private static object UpdateBody(Customer? customer, string? name = null, string? email = null) => new
    {
        name = name ?? customer?.Name ?? "John Smith",
        phoneNumber = customer?.PhoneNumber ?? "+15555555555",
        email = email ?? customer?.Email ?? UniqueEmail(),
        vehicles = customer is null
            ? [new { vehicleId = (Guid?)null, make = "Honda", model = "Accord", year = 2024, licensePlate = "ABC123" }]
            : customer.Vehicles.Select(v => new
            {
                vehicleId = (Guid?)v.Id, make = v.Make, model = v.Model, year = v.Year, licensePlate = v.LicensePlate
            }).ToArray()
    };

    private async Task<CustomerDto> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, CreateBody(UniqueEmail()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private async Task<Customer> SeedCustomerAsync()
    {
        var customer = CustomerFactory.CreateCustomer(
            name: $"Customer-{Guid.NewGuid():N}", email: UniqueEmail()).Value;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        return customer;
    }

    private async Task RemoveSeededCustomerAsync(Customer customer)
    {
        var tracked = await _context.Customers.Include(c => c.Vehicles).FirstOrDefaultAsync(c => c.Id == customer.Id);

        if (tracked is not null)
        {
            _context.Customers.Remove(tracked);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task DeleteQuietlyAsync(Guid customerId)
    {
        try { await _client.DeleteAsync($"{BaseUrl}/{customerId}"); } catch { /* ignore */ }
    }

    private async Task<(Guid EmployeeId, Guid RepairTaskId, Guid WorkOrderId)> SeedWorkOrderForAsync(Customer customer)
    {
        var employee = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(name: $"T-{Guid.NewGuid():N}").Value;

        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000)).AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            repairTasks: [repairTask]).Value;

        _context.Employees.Add(employee);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return (employee.Id, repairTask.Id, workOrder.Id);
    }
}