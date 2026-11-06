using MechanicShop.Application.Features.Labors.Mappers;
using MechanicShop.Domain.Employees;
using MechanicShop.Tests.Common.Employees;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class LaborMapperTests
{

    [Fact]
    public void ToDto_ShouldMapCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var employee = EmployeeFactory.CreateLabor(id: id, firstName: "Jane", lastName: "Smith").Value;

        // Act
        var dto = employee.ToDto();

        // Assert
        Assert.Equal(id, dto.LaborId);
        Assert.Equal(employee.FullName, dto.Name);
    }

    [Fact]
    public void ToDto_ShouldIncludeFirstAndLastNameInName()
    {
        // Arrange
        var employee = EmployeeFactory.CreateLabor(firstName: "Jane", lastName: "Smith").Value;

        // Act
        var dto = employee.ToDto();

        // Assert
        Assert.Contains("Jane", dto.Name);
        Assert.Contains("Smith", dto.Name);
    }

    [Fact]
    public void ToDto_ShouldMapAnyEmployeeRole()
    {
        var manager = EmployeeFactory.CreateManager().Value;

        var dto = manager.ToDto();

        Assert.Equal(manager.Id, dto.LaborId);
        Assert.Equal(manager.FullName, dto.Name);
    }

    [Fact]
    public void ToDto_WithNullEntity_ShouldThrowNullReferenceException()
    {
        Employee employee = null!;

        Assert.Throws<ArgumentNullException>(() => employee.ToDto());
    }


    [Fact]
    public void ToDtos_ShouldMapListCorrectly()
    {
        // Arrange
        var employee1 = EmployeeFactory.CreateLabor(firstName: "Jane", lastName: "Smith").Value;
        var employee2 = EmployeeFactory.CreateLabor(firstName: "Bob", lastName: "Brown").Value;
        var employees = new List<Employee> { employee1, employee2 };

        // Act
        var dtos = employees.ToDtos();

        // Assert
        Assert.Equal(2, dtos.Count);
        Assert.Equal(employee1.Id, dtos[0].LaborId);
        Assert.Equal(employee1.FullName, dtos[0].Name);
        Assert.Equal(employee2.Id, dtos[1].LaborId);
        Assert.Equal(employee2.FullName, dtos[1].Name);
    }

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var employees = new List<Employee>();

        // Act
        var dtos = employees.ToDtos();

        // Assert
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public void ToDtos_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        IEnumerable<Employee> employees = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => employees.ToDtos());
    }
}