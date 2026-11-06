using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.Commands.DeleteWorkOrder;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.DeleteWorkOrder;

[Collection(WebAppFactoryCollection.CollectionName)]
public class DeleteWorkOrderCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldSucceed_WhenWorkOrderIsScheduled()
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(new DeleteWorkOrderCommand(workOrder.Id));

        // Assert
        Assert.True(result.IsSuccess);
        var exists = await factory.CreateAppDbContext().WorkOrders.AnyAsync(wo => wo.Id == workOrder.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkOrderDoesNotExists()
    {
        // Arrange & Act
        var result = await _mediator.Send(new DeleteWorkOrderCommand(Guid.NewGuid()));
    
        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound.Code, result.TopError.Code);
    }

    [Theory]
    [MemberData(nameof(NonScheduledStates))]
    public async Task Handle_ShouldFail_WhenWorkOrderIsNotScheduled(WorkOrderState state)
    {
        // Arrange
        var workOrder = await SeedWorkOrderAsync();
        await _context.WorkOrders.Where(wo => wo.Id == workOrder.Id)
                                 .ExecuteUpdateAsync(s => s.SetProperty(w => w.State, state));

        // Act
        var result = await _mediator.Send(new DeleteWorkOrderCommand(workOrder.Id));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.Readonly.Code, result.TopError.Code);
        
        var stillExists = await factory.CreateAppDbContext()
            .WorkOrders.AnyAsync(wo => wo.Id == workOrder.Id);
        Assert.True(stillExists);
    }

    [Fact]
    public async Task Handle_ShouldNotDeleteOtherWorkOrders_WhenDeletingOne()
    {
        // Arrange
        var target = await SeedWorkOrderAsync();
        var other = await SeedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(new DeleteWorkOrderCommand(target.Id));

        // Assert
        Assert.True(result.IsSuccess);
        var otherExists = await factory.CreateAppDbContext().WorkOrders.AnyAsync(wo => wo.Id == other.Id);
        Assert.True(otherExists);
    }

    public static IEnumerable<object[]> NonScheduledStates =>
        Enum.GetValues<WorkOrderState>()
            .Where(s => s != WorkOrderState.Scheduled)
            .Select(s => new object[] { s });

    private async Task<WorkOrder> SeedWorkOrderAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var employee = EmployeeFactory.CreateEmployee().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(18), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: customer.Vehicles.First().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            repairTasks: [repairTask]
        ).Value;

        await _context.Customers.AddAsync(customer);
        await _context.Employees.AddAsync(employee);
        await _context.RepairTasks.AddAsync(repairTask);
        await _context.WorkOrders.AddAsync(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);

        return workOrder;
    }
}