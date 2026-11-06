using MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;
using MechanicShop.Domain.WorkOrders.Enums;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.CreateWorkOrder;

public class CreateWorkOrderCommandValidatorTests
{
    private readonly CreateWorkOrderCommandValidator _validator;

    public CreateWorkOrderCommandValidatorTests()
    {
        _validator = new CreateWorkOrderCommandValidator();
    }

    [Fact]
    public void Validate_ShouldFail_WhenVehicleIdIsEmpty()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: Spot.A,
            VehicleId: Guid.Empty,
            StartAt: DateTime.UtcNow.AddHours(1),
            RepairTaskIds: [Guid.NewGuid()],
            LaborId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateWorkOrderCommand.VehicleId));
    }

    [Fact]
    public void Validate_ShouldFail_WhenStartAtIsNotInFuture()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: Spot.A,
            VehicleId: Guid.NewGuid(),
            StartAt: DateTime.UtcNow.AddSeconds(-1),
            RepairTaskIds: [Guid.NewGuid()],
            LaborId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateWorkOrderCommand.StartAt));
    }

    [Fact]
    public void Validate_ShouldFail_WhenRepairTaskIdsIsEmpty()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: Spot.A,
            VehicleId: Guid.NewGuid(),
            StartAt: DateTime.UtcNow.AddHours(1),
            RepairTaskIds: [],
            LaborId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateWorkOrderCommand.RepairTaskIds));
    }

    [Fact]
    public void Validate_ShouldFail_WhenLaborIdIsEmpty()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: Spot.A,
            VehicleId: Guid.NewGuid(),
            StartAt: DateTime.UtcNow.AddHours(1),
            RepairTaskIds: [Guid.NewGuid()],
            LaborId: Guid.Empty
        );

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateWorkOrderCommand.LaborId));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSpotIdInvalid()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: (Spot)999,
            VehicleId: Guid.NewGuid(),
            StartAt: DateTime.UtcNow.AddHours(1),
            RepairTaskIds: [Guid.NewGuid()],
            LaborId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateWorkOrderCommand.Spot));
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        var command = new CreateWorkOrderCommand
        (
            Spot: Spot.A,
            VehicleId: Guid.NewGuid(),
            StartAt: DateTime.UtcNow.AddHours(1),
            RepairTaskIds: [Guid.NewGuid()],
            LaborId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }


}