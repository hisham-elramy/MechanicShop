using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Tests.Common.Employees;
using Xunit;

namespace MechanicShop.Domain.UnitTests.Employees;

public class EmployeeTests
{
    [Fact]
    public void CreateEmployee_ShouldSucceed_WithValidData()
    {
        var id = Guid.NewGuid();
        const string firstName = "John";
        const string lastName = "Doe";
        const Role role = Role.Labor;

        var result = EmployeeFactory.CreateEmployee(id: id, firstName: firstName, lastName: lastName, role: role);

        Assert.True(result.IsSuccess);

        var employee = result.Value;
        Assert.Equal(id, employee.Id);
        Assert.Equal(firstName, employee.FirstName);
        Assert.Equal(lastName, employee.LastName);
        Assert.Equal(role, employee.Role);
        Assert.Equal("John Doe", employee.FullName);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WithEmptyId()
    {
        var result = EmployeeFactory.CreateManager(id: Guid.Empty, firstName: "John", lastName: "Doe");

        Assert.True(result.IsError);
        Assert.Equal(EmployeeErrors.IdRequired.Code, result.TopError.Code);
        Assert.Equal(EmployeeErrors.IdRequired.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WithEmptyFirstName()
    {
        var result = EmployeeFactory.CreateManager(id: Guid.NewGuid(), firstName: " ", lastName: "Doe");

        Assert.True(result.IsError);
        Assert.Equal(EmployeeErrors.FirstNameRequired.Code, result.TopError.Code);
        Assert.Equal(EmployeeErrors.FirstNameRequired.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WithEmptyLastName()
    {
        var result = EmployeeFactory.CreateManager(id: Guid.NewGuid(), firstName: "John", lastName: " ");

        Assert.True(result.IsError);
        Assert.Equal(EmployeeErrors.LastNameRequired.Code, result.TopError.Code);
        Assert.Equal(EmployeeErrors.LastNameRequired.Description, result.TopError.Description);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WithInvalidRole()
    {
        var result = EmployeeFactory.CreateEmployee(id: Guid.NewGuid(), firstName: "John", lastName: "Doe", role: (Role)999);

        Assert.True(result.IsError);
        Assert.Equal(EmployeeErrors.RoleInvalid.Code, result.TopError.Code);
        Assert.Equal(EmployeeErrors.RoleInvalid.Description, result.TopError.Description);
    }
}