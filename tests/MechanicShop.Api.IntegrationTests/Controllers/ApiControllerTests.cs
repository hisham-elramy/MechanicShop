using MechanicShop.Api.Controllers;
using MechanicShop.Domain.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Api.UnitTests.Controllers;

public class ApiControllerTests
{
    private sealed class TestController : ApiController
    {
        public ActionResult Run(List<Error> errors) => Problem(errors);
    }

    private static TestController Create()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();

        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
    }

    private static int? StatusOf(ActionResult result) => ((ObjectResult)result).StatusCode;

    [Fact]
    public void Problem_ShouldReturn409_ForConflict() =>
        Assert.Equal(409, StatusOf(Create().Run([Error.Conflict("c", "d")])));

    [Fact]
    public void Problem_ShouldReturn404_ForNotFound() =>
        Assert.Equal(404, StatusOf(Create().Run([Error.NotFound("c", "d")])));

    [Fact]
    public void Problem_ShouldReturn400_ForValidation() =>
        Assert.Equal(400, StatusOf(Create().Run([Error.Validation("c", "d")])));

    [Fact]
    public void Problem_ShouldReturn500_ForFailure() =>
        Assert.Equal(500, StatusOf(Create().Run([Error.Failure("c", "d")])));

    [Fact]
    public void Problem_ShouldReturn500_ForAnEmptyList() =>
        Assert.Equal(500, StatusOf(Create().Run([])));

    [Fact]
    public void Problem_ShouldReturn401_ForUnauthorized() =>
        Assert.Equal(401, StatusOf(Create().Run([Error.Unauthorized("c", "d")])));

    [Fact]
    public void Problem_ShouldUseTheFirstErrorsStatus_ForMixedErrors() =>
        Assert.Equal(409, StatusOf(Create().Run([Error.Conflict("c1", "d"), Error.Validation("c2", "d")])));

    [Fact]
    public void Problem_ShouldReturnAValidationProblem_WhenAllErrorsAreValidation()
    {
        var result = Create().Run([Error.Validation("Name", "Name is required"), Error.Validation("Email", "Bad email")]);

        var body = Assert.IsType<ValidationProblemDetails>(((ObjectResult)result).Value);
        Assert.Equal(400, StatusOf(result));
        Assert.Contains("Name", body.Errors.Keys);
        Assert.Contains("Email", body.Errors.Keys);
    }

    // Pins current behavior (error 2): keys are error codes, so duplicate codes merge
    [Fact]
    public void Problem_ShouldGroupMessagesUnderTheErrorCode()
    {
        var result = Create().Run([Error.Validation("Same", "first"), Error.Validation("Same", "second")]);

        var body = Assert.IsType<ValidationProblemDetails>(((ObjectResult)result).Value);
        Assert.Equal(2, body.Errors["Same"].Length);
    }

    [Fact]
    public void Problem_ShouldUseTheDescriptionAsTitle_ForASingleError()
    {
        var result = Create().Run([Error.NotFound("c", "Thing does not exist")]);

        Assert.Equal("Thing does not exist", Assert.IsType<ProblemDetails>(((ObjectResult)result).Value).Title);
    }
}