using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using Xunit;

namespace MechanicShop.Domain.UnitTests.WorkOrders;

public class WorkOrderTests
{
    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenIdIsEmpty()
    {
        var result = WorkOrderFactory.CreateWorkOrder(id: Guid.Empty);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.WorkOrderIdRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenVehicleIdIsEmpty()
    {
        var result = WorkOrderFactory.CreateWorkOrder(vehicleId: Guid.Empty);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.VehicleIdRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenNoRepairTasks()
    {
        var result = WorkOrderFactory.CreateWorkOrder(repairTasks: []);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.RepairTasksRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenLaborIdIsEmpty()
    {
        var result = WorkOrderFactory.CreateWorkOrder(laborId: Guid.Empty);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.LaborIdRequired.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenTimingInvalid()
    {
        var result = WorkOrderFactory.CreateWorkOrder(startAt: DateTimeOffset.UtcNow.AddHours(1), endAt: DateTimeOffset.UtcNow);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.InvalidTiming.Code, result.TopError.Code);
    }

    [Fact]
    public void CreateWorkOrder_ShouldReturnError_WhenSpotInvalid()
    {
        const Spot invalidSpot = (Spot)999;

        var result = WorkOrderFactory.CreateWorkOrder(spot: invalidSpot);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.SpotInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void AddRepairTask_ShouldReturnError_WhenNotEditable()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        workOrder.UpdateState(WorkOrderState.InProgress);
        workOrder.UpdateState(WorkOrderState.Completed);

        var result = workOrder.AddRepairTask(RepairTaskFactory.CreateRepairTask().Value);

        Assert.True(result.IsError);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void UpdateLabor_ShouldReturnError_WhenLaborIdEmpty()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var result = workOrder.UpdateLabor(Guid.Empty);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.LaborIdEmpty(workOrder.Id.ToString()).Code, result.TopError.Code);
    }

    [Fact]
    public void UpdateSpot_ShouldReturnError_WhenSpotInvalid()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        const Spot invalidSpot = (Spot)999;
        var result = workOrder.UpdateSpot(invalidSpot);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.SpotInvalid.Code, result.TopError.Code);
    }

    [Fact]
    public void UpdateTiming_ShouldReturnError_WhenTimingInvalid()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var result = workOrder.UpdateTiming(DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.InvalidTiming.Code, result.TopError.Code);
    }

    [Fact]
    public void UpdateState_ShouldReturnError_WhenTransitionInvalid()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var result = workOrder.UpdateState(WorkOrderState.Completed);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderErrors.InvalidStateTransition(WorkOrderState.Scheduled, WorkOrderState.Completed).Code, result.TopError.Code);
    }

    [Fact]
    public void UpdateLabor_ShouldReturnSuccess_AndSetNewLaborId()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var newLabor = Guid.NewGuid();
        var result = workOrder.UpdateLabor(newLabor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newLabor, workOrder.LaborId);
    }

    [Fact]
    public void UpdateSpot_ShouldReturnSuccess_AndSetNewSpot()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var result = workOrder.UpdateSpot(Spot.B);

        Assert.True(result.IsSuccess);
        Assert.Equal(Spot.B, workOrder.Spot);
    }

    [Fact]
    public void UpdateTiming_ShouldReturnSuccess_AndSetNewTiming()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var newStart = workOrder.StartAtUtc.AddHours(2);
        var newEnd = newStart.AddHours(1);
        var result = workOrder.UpdateTiming(newStart, newEnd);

        Assert.True(result.IsSuccess);
        Assert.Equal(newStart, workOrder.StartAtUtc);
        Assert.Equal(newEnd, workOrder.EndAtUtc);
    }

    [Fact]
    public void UpdateState_ShouldReturnSuccess_AndSetStateToInProgress()
    {
        var workOrder = WorkOrderFactory.CreateWorkOrder().Value;

        var result = workOrder.UpdateState(WorkOrderState.InProgress);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderState.InProgress, workOrder.State);
    }
}