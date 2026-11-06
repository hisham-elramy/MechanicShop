using MechanicShop.Application.Common.Behaviors;
using MechanicShop.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace MechanicShop.Application.UnitTests.Behaviors;

public class PerformanceBehavior
{
    private readonly ILogger<TestRequest> _logger = Substitute.For<ILogger<TestRequest>>();
    private readonly IUser _user = Substitute.For<IUser>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly PerformanceBehavior<TestRequest, TestResponse> _behavior;

    public PerformanceBehavior()
    {
        _behavior = new PerformanceBehavior<TestRequest, TestResponse>(_logger, _user, _identityService);
    }

    [Fact]
    public async Task Handle_ShouldNotLogWarning_WhenRequestTakesLessThan500Ms()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new TestResponse { Result = "Success" };
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _behavior.Handle(request, _ => Task.FromResult(expectedResponse), cancellationToken);

        // Assert
        Assert.Equal(expectedResponse, result);
        _logger.DidNotReceive().Log
        (
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Handle_ShouldLogWarning_WhenRequestTakesMoreThan500Ms()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new TestResponse { Result = "Success" };
        var cancellationToken = CancellationToken.None;
        const string userId = "user123";
        const string username = "TestUser";

        _user.Id.Returns(userId);
        _identityService.GetUsernameAsync(userId).Returns(username);

        // Act
        var result = await _behavior.Handle(request, async _ => 
        {
            await Task.Delay(600, cancellationToken);
            return expectedResponse;

        }, cancellationToken);

        // Assert
        Assert.Equal(expectedResponse, result);
        _logger.Received(1).Log
        (
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>
            (
                o => o.ToString()!.Contains("Long Running Request") &&
                o.ToString()!.Contains("TestRequest") &&
                o.ToString()!.Contains(userId) &&
                o.ToString()!.Contains(username)
            ),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Handle_ShouldLogWarningWithEmptyUserData_WhenUserIdIsNull()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new TestResponse { Result = "Success" };
        var cancellationToken = CancellationToken.None;

        _user.Id.Returns((string?)null);

        // Act
        var result = await _behavior.Handle
        (
            request,
            async _ =>
            {
                await Task.Delay(600, cancellationToken);
                return expectedResponse;
            },
            cancellationToken
        );

        // Assert
        Assert.Equal(expectedResponse, result);
        _logger.Received(1).Log
        (
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>
            (
                o => o.ToString()!.Contains("Long Running Request") &&
                o.ToString()!.Contains("TestRequest")
            ),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );

        await _identityService.DidNotReceive().GetUsernameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldLogWarningWithEmptyUserData_WhenUserIdIsEmpty()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new TestResponse { Result = "Success" };
        var cancellationToken = CancellationToken.None;

        _user.Id.Returns(string.Empty);

        // Act
        var result = await _behavior.Handle
        (
            request,
            async _ =>
            {
                await Task.Delay(600, cancellationToken);
                return expectedResponse;
            },
            cancellationToken
        );

        // Assert
        Assert.Equal(expectedResponse, result);
        _logger.Received(1).Log
        (
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>
            (
                o => o.ToString()!.Contains("Long Running Request") &&
                o.ToString()!.Contains("TestRequest")
            ),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );

        await _identityService.DidNotReceive().GetUsernameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldLogWarningWithNullUsername_WhenIdentityServiceReturnNull()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new TestResponse { Result = "Success" };
        var cancellationToken = CancellationToken.None;
        const string userId = "userId";

        _user.Id.Returns(userId);
        _identityService.GetUsernameAsync(userId).Returns((string?)null);

        // Act
        var result = await _behavior.Handle
        (
            request,
            async _ =>
            {
                await Task.Delay(600, cancellationToken);
                return expectedResponse;
            },
            cancellationToken
        );

        // Assert
        Assert.Equal(expectedResponse, result);
        _logger.Received(1).Log
        (
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Handle_ShouldNotCatchException_WhenNextThrowsException()
    {
        // Arrange
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new InvalidOperationException("Test exception");
        var cancellationToken = CancellationToken.None;

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => _behavior.Handle
            (
                request,
                _ => throw expectedResponse, cancellationToken
            )
        );

        // Assert
        Assert.Equal(expectedResponse, exception);
    }


    public class TestRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class TestResponse
    {
        public string Result { get; set; } = string.Empty;
    }
}