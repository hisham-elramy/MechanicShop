using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.RemoveCustomer;


[Collection(WebAppFactoryCollection.CollectionName)]
public class RemoveCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();


    [Fact]
    public async Task Handle_ShouldSucceedAndDelete_WhenCustomerHasNoWorkOrders()
    {
        // Arrange
        var customer = await SeedCustomerAsync();

        // Act
        var result = await _mediator.Send(new RemoveCustomerCommand(customer.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(await CustomerExistsAsync(customer.Id));
    }

    [Fact]
    public async Task Handle_ShouldNotDeleteOtherCustomers()
    {
        // Arrange
        var target = await SeedCustomerAsync();
        var other = await SeedCustomerAsync();

        // Act
        var result = await _mediator.Send(new RemoveCustomerCommand(target.Id));

        // Asserts
        Assert.True(result.IsSuccess);
        Assert.True(await CustomerExistsAsync(other.Id));
    }

    [Fact]
    public async Task Handle_ShouldDelete_WhenAnotherCustomerHasWorkOrders()
    {
        // Arrange
        var busy = await SeedCustomerAsync();
        var idle = await SeedCustomerAsync();
        await SeedWorkOrderAsync(busy);

        // Act
        var result = await _mediator.Send(new RemoveCustomerCommand(idle.Id));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(await CustomerExistsAsync(busy.Id));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenCustomerDoesNotExist()
    {
        // Arrange & Act
        var result = await _mediator.Send(new RemoveCustomerCommand(Guid.NewGuid()));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(ApplicationErrors.CustomerNotFound.Code, result.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDeletingTheSameCustomerTwice()
    {
        // Arrange
        var customer = await SeedCustomerAsync();

        // Act & Assert
        Assert.True((await _mediator.Send(new RemoveCustomerCommand(customer.Id))).IsSuccess);
        var second = await _mediator.Send(new RemoveCustomerCommand(customer.Id));

        Assert.Equal(ApplicationErrors.CustomerNotFound.Code, second.TopError.Code);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOneVehicleHasAWorkOrder()
    {
        // Arrange
        var customer = await SeedCustomerAsync();
        await SeedWorkOrderAsync(customer);

        // Act
        var result = await _mediator.Send(new RemoveCustomerCommand(customer.Id));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(CustomerErrors.CannotDeleteCustomerWithWorkOrders.Code, result.TopError.Code);
        Assert.True(await CustomerExistsAsync(customer.Id));
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_ShouldFail_WhenWorkOrderIsInAnyState(WorkOrderState state)
    {
        // Arrange
        var customer = await SeedCustomerAsync();
        await SeedWorkOrderAsync(customer, state);

        // Act
        var result = await _mediator.Send(new RemoveCustomerCommand(customer.Id));

        // Assert
        Assert.Equal(CustomerErrors.CannotDeleteCustomerWithWorkOrders.Code, result.TopError.Code);
        Assert.True(await CustomerExistsAsync(customer.Id));
    }

    private async Task<Customer> SeedCustomerAsync()
    {
        var customer = CustomerFactory.CreateCustomer(email: $"c-{Guid.NewGuid():N}@example.com").Value;

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(CancellationToken.None);

        return customer;
    }

    private async Task SeedWorkOrderAsync(Customer customer, WorkOrderState state = WorkOrderState.Scheduled)
    {
        var employee = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(name: $"Task-{Guid.NewGuid():N}").Value;

        var day = DateTime.UtcNow.Date.AddDays(Random.Shared.Next(30, 5000));
        var start = new DateTimeOffset(day.AddHours(10), TimeSpan.Zero);

        var workOrder = WorkOrderFactory.CreateWorkOrder
        (
            vehicleId: customer.Vehicles.Last().Id,
            startAt: start,
            endAt: start.AddMinutes(30),
            laborId: employee.Id,
            spot: Spot.A,
            repairTasks: [repairTask]
        ).Value;

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

        _context.Employees.Add(employee);
        _context.RepairTasks.Add(repairTask);
        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync(CancellationToken.None);
    }

    private Task<bool> CustomerExistsAsync(Guid id)
    {
        return factory.CreateAppDbContext().Customers.AnyAsync(c => c.Id == id);
    }
}