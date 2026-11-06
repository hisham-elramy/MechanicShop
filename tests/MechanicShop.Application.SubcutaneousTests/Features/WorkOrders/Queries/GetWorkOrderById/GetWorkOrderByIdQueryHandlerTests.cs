using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrderById;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrderByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenWorkOrderExists()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(new GetWorkOrderByIdQuery(workOrder.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new GetWorkOrderByIdQuery(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldServeCachedResult_UntilWorkOrderTagIsRemoved()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync();
        
        // Act
        var query = new GetWorkOrderByIdQuery(workOrder.Id);

        // Assert
        Assert.True((await _mediator.Send(query)).IsSuccess);

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.True((await _mediator.Send(query)).IsSuccess);

        await factory.Services.GetRequiredService<HybridCache>().RemoveByTagAsync("work-order");

        var fresh = await _mediator.Send(query);
        Assert.True(fresh.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, fresh.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldNotCacheNotFound_WhenWorkOrderIsCreatedAfterwards()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        var first = await _mediator.Send(new GetWorkOrderByIdQuery(id));
        Assert.True(first.IsError);

        await SeedWorkOrderAsync(id);

        var second = await _mediator.Send(new GetWorkOrderByIdQuery(id));
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenWorkOrderHasNoInvoice()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(new GetWorkOrderByIdQuery(workOrder.Id));

        // Assert
        Assert.True(result.IsSuccess);
    }

    private async Task<WorkOrder> SeedWorkOrderAsync(Guid? id = null)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var day = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            id: id,
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            spot: Spot.A,
            repairTasks: [repairTask]
        ).Value;

        _context.Customers.Add(customer);
        _context.Employees.Add(employee);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
}