using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepairTasks;
using MechanicShop.Tests.Common.WorkOrders;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class WorkOrderMapperTests
{
    private static WorkOrder CreateWorkOrder(List<RepairTask>? repairTasks = null, bool attachVehicle = true, bool attachLabor = true)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var labor = EmployeeFactory.CreateEmployee().Value;
        var vehicle = customer.Vehicles.First();

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: repairTasks).Value;

        if (attachVehicle)
        {
            workOrder.Vehicle = vehicle;
        }

        if (attachLabor)
        {
            workOrder.Labor = labor;
        }

        return workOrder;
    }


    [Fact]
    public void ToDto_ShouldMapCorrectly()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;
        var labor = EmployeeFactory.CreateEmployee().Value;
        var vehicle = customer.Vehicles.First();
        var part = PartFactory.CreatePart(cost: 100m, quantity: 2).Value;

        var repairTask = RepairTaskFactory.CreateRepairTask(laborCost: 150m, parts: [part]).Value;

        var totalPartsCost = part.Cost * part.Quantity;
        var totalLaborCost = repairTask.LaborCost;
        var totalCost = totalPartsCost + totalLaborCost;
        var duration = (int)repairTask.EstimatedDurationInMins;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        var invoiceLine = InvoiceLineItemFactory.CreateInvoiceLineItem(
            lineNumber: 1,
            description: "some description",
            quantity: 1,
            unitPrice: totalCost).Value;

        var invoice = InvoiceFactory.CreateInvoice(
            workOrderId: workOrder.Id,
            items: [invoiceLine]).Value;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;
        workOrder.Invoice = invoice;

        // Act
        var dto = workOrder.ToDto();

        // Assert
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(workOrder.Spot, dto.Spot);
        Assert.Equal(workOrder.StartAtUtc, dto.StartAtUtc);
        Assert.Equal(workOrder.EndAtUtc, dto.EndAtUtc);
        Assert.Equal(workOrder.State, dto.State);
        Assert.Equal(workOrder.CreatedAtUtc, dto.CreatedAt);

        Assert.NotNull(dto.Labor);
        Assert.Equal(workOrder.LaborId, dto.Labor!.LaborId);
        Assert.Equal($"{labor.FirstName} {labor.LastName}", dto.Labor.Name);

        Assert.NotNull(dto.Vehicle);
        Assert.Equal(vehicle.Id, dto.Vehicle!.VehicleId);
        Assert.Equal(vehicle.Make, dto.Vehicle.Make);
        Assert.Equal(vehicle.Model, dto.Vehicle.Model);
        Assert.Equal(vehicle.Year, dto.Vehicle.Year);
        Assert.Equal(vehicle.LicensePlate, dto.Vehicle.LicensePlate);

        Assert.Single(dto.RepairTasks);
        Assert.Equal(totalPartsCost, dto.TotalPartCost);
        Assert.Equal(totalLaborCost, dto.TotalLaborCost);
        Assert.Equal(totalCost, dto.TotalCost);
        Assert.Equal(duration, dto.TotalDurationInMins);
        Assert.Equal(invoice.Id, dto.InvoiceId);
    }

    [Fact]
    public void ToDto_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        WorkOrder workOrder = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => workOrder.ToDto());
    }

    [Fact]
    public void ToDto_WhenLaborIsNull_ShouldMapLaborAsNull()
    {
        // Arrange
        var workOrder = CreateWorkOrder(attachLabor: false);

        // Act
        var dto = workOrder.ToDto();

        // Assert
        Assert.Null(dto.Labor);
    }

    [Fact]
    public void ToDto_WhenVehicleIsNull_ShouldMapVehicleAsNull()
    {
        // Arrange
        var workOrder = CreateWorkOrder(attachVehicle: false);

        // Act
        var dto = workOrder.ToDto();

        // Assert
        Assert.Null(dto.Vehicle);
    }

    [Fact]
    public void ToDto_WithMultipleRepairTasks_ShouldSumTotals()
    {
        // Arrange
        var task1 = RepairTaskFactory.CreateRepairTask(
            name: "Oil Change",
            laborCost: 100m,
            parts: [PartFactory.CreatePart(cost: 10m, quantity: 3).Value]).Value;

        var task2 = RepairTaskFactory.CreateRepairTask(
            name: "Brake Service",
            laborCost: 200m,
            parts: [PartFactory.CreatePart(cost: 20m, quantity: 1).Value]).Value;

        var workOrder = CreateWorkOrder(repairTasks: [task1, task2]);

        // Act
        var dto = workOrder.ToDto();

        // Assert
        Assert.Equal(2, dto.RepairTasks.Count);
        Assert.Equal(50m, dto.TotalPartCost);
        Assert.Equal(300m, dto.TotalLaborCost);
        Assert.Equal(task1.TotalCost + task2.TotalCost, dto.TotalCost);
        Assert.Equal((int)task1.EstimatedDurationInMins + (int)task2.EstimatedDurationInMins,
                     dto.TotalDurationInMins);
    }


    [Fact]
    public void ToDtos_ShouldMapListCorrectly()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;
        var labor = EmployeeFactory.CreateEmployee().Value;
        var vehicle = customer.Vehicles.First();

        var repairTask = RepairTaskFactory.CreateRepairTask(
            laborCost: 100m,
            parts: [PartFactory.CreatePart(cost: 50m, quantity: 1).Value]).Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        var workOrders = new List<WorkOrder> { workOrder };

        // Act
        var dtos = workOrders.ToDtos();

        // Assert
        Assert.Single(dtos);
        var dto = dtos[0];

        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(workOrder.Spot, dto.Spot);
        Assert.Equal(workOrder.StartAtUtc, dto.StartAtUtc);
        Assert.Equal(workOrder.EndAtUtc, dto.EndAtUtc);
        Assert.NotNull(dto.Labor);
        Assert.Equal($"{labor.FirstName} {labor.LastName}", dto.Labor!.Name);
        Assert.Equal(labor.Id, dto.Labor.LaborId);
        Assert.NotNull(dto.Vehicle);
        Assert.Single(dto.RepairTasks);
        Assert.Equal(workOrder.State, dto.State);
    }

    [Fact]
    public void ToDtos_ShouldPreserveOrder()
    {
        // Arrange
        var first = CreateWorkOrder();
        var second = CreateWorkOrder();

        // Act
        var dtos = new List<WorkOrder> { first, second }.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(first.Id, dtos[0].WorkOrderId);
        Assert.Equal(second.Id, dtos[1].WorkOrderId);
    }

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var workOrders = new List<WorkOrder>();

        // Act
        var dtos = workOrders.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDtos_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<WorkOrder> workOrders = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => workOrders.ToDtos());
    }


    [Fact]
    public void ToListItemDto_ShouldMapSummaryCorrectly()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;
        var labor = EmployeeFactory.CreateEmployee().Value;
        var vehicle = customer.Vehicles.First();

        var repairTask = RepairTaskFactory.CreateRepairTask(name: "Oil Change").Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        // Act
        var dto = workOrder.ToListItemDto();

        // Assert
        Assert.Equal(workOrder.Id, dto.WorkOrderId);
        Assert.Equal(workOrder.Spot, dto.Spot);
        Assert.Equal(workOrder.StartAtUtc, dto.StartAtUtc);
        Assert.Equal(workOrder.EndAtUtc, dto.EndAtUtc);
        Assert.Equal(vehicle.Make, dto.Vehicle.Make);
        Assert.Equal($"{labor.FirstName} {labor.LastName}", dto.Labor);
        Assert.Single(dto.RepairTasks);
        Assert.Equal("Oil Change", dto.RepairTasks[0]);
        Assert.Equal(workOrder.State, dto.State);
    }

    [Fact]
    public void ToListItemDto_WhenLaborIsNull_ShouldMapLaborAsNull()
    {
        // Arrange
        var workOrder = CreateWorkOrder(attachLabor: false);

        // Act
        var dto = workOrder.ToListItemDto();

        // Assert
        Assert.Null(dto.Labor);
    }

    [Fact]
    public void ToListItemDto_WithMultipleRepairTasks_ShouldMapAllNamesInOrder()
    {
        // Arrange
        var task1 = RepairTaskFactory.CreateRepairTask(name: "Oil Change").Value;
        var task2 = RepairTaskFactory.CreateRepairTask(name: "Brake Service").Value;
        var workOrder = CreateWorkOrder(repairTasks: [task1, task2]);

        // Act
        var dto = workOrder.ToListItemDto();

        // Assert
        Assert.Equal(["Oil Change", "Brake Service"], dto.RepairTasks);
    }

    [Fact]
    public void ToListItemDto_WhenVehicleIsNull_ShouldThrowNullReferenceException()
    {
        var workOrder = CreateWorkOrder(attachVehicle: false);

        Assert.Throws<ArgumentNullException>(() => workOrder.ToListItemDto());
    }

    [Fact]
    public void ToListItemDto_WithNullEntity_ShouldThrowNullReferenceException()
    {
        WorkOrder workOrder = null!;

        Assert.Throws<NullReferenceException>(() => workOrder.ToListItemDto());
    }
}