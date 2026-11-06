using System.Net;
using System.Net.Http.Json;
using System.Text;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Billing;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class InvoicesControllerTests(WebAppFactory factory) : ApiTestBase(factory)
{
    private const string BaseUrl = "/api/v1.0/invoices";

    // ---------- POST /invoices/workorders/{id} ----------

    [Fact]
    public async Task IssueInvoice_ForCompletedWorkOrder_ShouldReturnCreatedWithLocation()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{workOrder.Id}", new { });
        var dto = await response.Content.ReadFromJsonAsync<InvoiceDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(dto.InvoiceId.ToString(), response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueInvoice_ShouldPersistTheInvoice()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);

        var dto = await IssueAsync(workOrder.Id);

        Assert.True(await Factory.CreateAppDbContext().Invoices.AnyAsync(i => i.Id == dto.InvoiceId));
    }

    [Fact]
    public async Task IssueInvoice_ThenFollowLocation_ShouldReturnTheInvoice()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{workOrder.Id}", new { });

        var follow = await Client.GetAsync(response.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
    }

    [Theory]
    [InlineData(WorkOrderState.Scheduled)]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task IssueInvoice_ForNotCompletedWorkOrder_ShouldReturnConflict(WorkOrderState state)
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(state);

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{workOrder.Id}", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // FAILS with the original handler: a second invoice is created and the response is 201
    [Fact]
    public async Task IssueInvoice_Twice_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);
        await IssueAsync(workOrder.Id);

        var second = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{workOrder.Id}", new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task IssueInvoice_ForUnknownWorkOrder_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{Guid.NewGuid()}", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // MAY FAIL: depends on the validation pipeline mapping Guid.Empty to 400
    [Fact]
    public async Task IssueInvoice_WithEmptyGuid_ShouldReturnBadRequest()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{Guid.Empty}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueInvoice_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{Guid.NewGuid()}", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IssueInvoice_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{Guid.NewGuid()}", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- GET /invoices/{id} ----------

    [Fact]
    public async Task GetInvoice_WithValidId_ShouldReturnMappedInvoice()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var workOrder = await SeedWorkOrderAsync(WorkOrderState.Completed);
        var issued = await IssueAsync(workOrder.Id);

        var response = await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}");
        var dto = await response.Content.ReadFromJsonAsync<InvoiceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(issued.InvoiceId, dto!.InvoiceId);
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(issued.Total, dto.Total);
        Assert.NotEmpty(dto.Items);
    }

    [Fact]
    public async Task GetInvoice_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_WithMalformedId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // FAILS with the original: no Cache-Control header on financial data
    [Fact]
    public async Task GetInvoice_ShouldNotBeCacheable()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);

        var response = await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}");

        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    // ---------- GET /invoices/{id}/pdf ----------

    [Fact]
    public async Task GetInvoicePdf_WithValidId_ShouldReturnAPdfFile()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);

        var response = await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task GetInvoicePdf_ShouldNameTheFileAfterTheInvoice()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);

        var response = await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}/pdf");

        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.Equal($"invoice-{issued.InvoiceId}.pdf", name);
    }

    [Fact]
    public async Task GetInvoicePdf_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoicePdf_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/pdf");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoicePdf_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/pdf");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- PUT /invoices/{id}/payments ----------

    [Fact]
    public async Task SettleInvoice_WithUnpaidInvoice_ShouldReturnNoContent()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{issued.InvoiceId}/payments", new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SettleInvoice_ShouldPersistPaidStatus()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);

        await Client.PutAsJsonAsync($"{BaseUrl}/{issued.InvoiceId}/payments", new { });

        var saved = await Factory.CreateAppDbContext().Invoices.AsNoTracking().SingleAsync(i => i.Id == issued.InvoiceId);
        Assert.Equal(InvoiceStatus.Paid, saved.Status);
    }

    // The query cache is keyed by tag "invoice", which settle invalidates
    [Fact]
    public async Task SettleInvoice_ThenGetInvoice_ShouldShowPaidStatus()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}")).StatusCode);   // warm cache

        await Client.PutAsJsonAsync($"{BaseUrl}/{issued.InvoiceId}/payments", new { });

        var dto = await (await Client.GetAsync($"{BaseUrl}/{issued.InvoiceId}")).Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal("Paid", dto!.PaymentStatus);
    }

    // Assumes InvoiceLocked is Error.Conflict
    [Fact]
    public async Task SettleInvoice_Twice_ShouldReturnConflict()
    {
        await AuthenticateAsync(TestUsers.Manager);
        var issued = await IssueAsync((await SeedWorkOrderAsync(WorkOrderState.Completed)).Id);
        await Client.PutAsJsonAsync($"{BaseUrl}/{issued.InvoiceId}/payments", new { });

        var second = await Client.PutAsJsonAsync($"{BaseUrl}/{issued.InvoiceId}/payments", new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task SettleInvoice_WithUnknownId_ShouldReturnNotFound()
    {
        await AuthenticateAsync(TestUsers.Manager);

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/payments", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SettleInvoice_AsLabor_ShouldReturnForbidden()
    {
        await AuthenticateAsync(TestUsers.Labor01);

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/payments", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SettleInvoice_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/payments", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- helpers ----------

    private async Task<InvoiceDto> IssueAsync(Guid workOrderId)
    {
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/workorders/{workOrderId}", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }
}