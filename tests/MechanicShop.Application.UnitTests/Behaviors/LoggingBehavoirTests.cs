using MechanicShop.Application.Common.Behaviors;
using MechanicShop.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace MechanicShop.Application.UnitTests.Behaviors;

public class LoggingBehaviorTests
{
    private readonly ILogger<DummyRequest> _logger = Substitute.For<ILogger<DummyRequest>>();
    private readonly IUser _user = Substitute.For<IUser>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly LoggingBehavior<DummyRequest> _sut;

    public LoggingBehaviorTests()
    {
        _sut = new LoggingBehavior<DummyRequest>(_logger, _user, _identityService);
    }

    [Fact]
    public async Task Process_WithUserId_LogsRequestWithUsername()
    {
        // Arrange
        var request = new DummyRequest();
        _user.Id.Returns("abc123");
        _identityService.GetUsernameAsync("abc123").Returns("Hisham");

        // Act
        await _sut.Process(request, CancellationToken.None);

        // Assert
        await _identityService.Received(1).GetUsernameAsync("abc123");

        _logger.Received(1).Log
        (
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Request")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Process_WithoutUserId_LogsRequestWithEmptyUsername()
    {
        // Arrange
        var request = new DummyRequest();
        _user.Id.Returns((string?)null);

        // Act
        await _sut.Process(request, CancellationToken.None);

        // Assert
        await _identityService.DidNotReceive().GetUsernameAsync(Arg.Any<string>());

        _logger.Received(1).Log
        (
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Request")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }


    public class DummyRequest;
}