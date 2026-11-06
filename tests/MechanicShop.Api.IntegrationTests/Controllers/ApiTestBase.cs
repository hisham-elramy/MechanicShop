using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Infrastructure.Identity;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

public abstract class ApiTestBase(WebAppFactory factory)
{
    protected readonly WebAppFactory Factory = factory;
    protected readonly AppHttpClient Client = factory.CreateAppHttpClient();
    protected readonly IAppDbContext Context = factory.CreateAppDbContext();

    protected async Task AuthenticateAsync(AppUser user) =>
        Client.SetAuthorizationHeader(await Client.GenerateTokenAsync(user));

    protected static DateOnly UniqueDate() =>
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000)));

    protected static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.ToDateTime(TimeOnly.MinValue).AddHours(hour), TimeSpan.Zero);

    // Own customer, labor and task per call; unique ids and dates, so no cleanup is needed
    protected async Task<WorkOrder> SeedWorkOrderAsync(
        WorkOrderState state = WorkOrderState.Scheduled, DateTimeOffset? start = null)
    {
        var customer = CustomerFactory.CreateCustomer(email: $"c-{Guid.NewGuid():N}@example.com").Value;
        var employee = EmployeeFactory.CreateLabor().Value;
        var task = RepairTaskFactory.CreateRepairTask(name: $"T-{Guid.NewGuid():N}").Value;
        var begin = start ?? At(UniqueDate(), 10);

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: customer.Vehicles.First().Id, startAt: begin, endAt: begin.AddMinutes(30),
            laborId: employee.Id, spot: Spot.A, repairTasks: [task]).Value;

        switch (state)
        {
            case WorkOrderState.InProgress:
                Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
                break;
            case WorkOrderState.Completed:
                Assert.True(workOrder.UpdateState(WorkOrderState.InProgress).IsSuccess);
                Assert.True(workOrder.UpdateState(WorkOrderState.Completed).IsSuccess);
                break;
            case WorkOrderState.Cancelled:
                Assert.True(workOrder.Cancel().IsSuccess);
                break;
        }

        Context.Customers.Add(customer);
        Context.Employees.Add(employee);
        Context.RepairTasks.Add(task);
        Context.WorkOrders.Add(workOrder);
        await Context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
}