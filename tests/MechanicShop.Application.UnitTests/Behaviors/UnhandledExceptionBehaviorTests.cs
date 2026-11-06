using MechanicShop.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace MechanicShop.Application.UnitTests.Behaviors;

public class UnhandledExceptionBehaviorTests
{
    private readonly ILogger<DummyRequest> _logger = Substitute.For<ILogger<DummyRequest>>();
    private readonly UnhandledExceptionBehavior<DummyRequest, string> _sut;

    public UnhandledExceptionBehaviorTests()
    {
        _sut = new UnhandledExceptionBehavior<DummyRequest, string>(_logger);
    }

    [Fact]
    public async Task Handle_InvokesNextAndReturnsResult_WhenNoException()
    {
        // Arrange
        var request = new DummyRequest();
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next.Invoke().Returns("OK");

        // Act
        var result = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task Handle_LogsErrorAndRethrows_WhenExceptionThrown()
    {
        // Arrange
        var request = new DummyRequest();
        var exception = new InvalidOperationException("test failure");

        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next.Invoke().Returns<Task<string>>(_ => throw exception);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Handle(request, next, CancellationToken.None));

        // Assert
        Assert.Equal(exception, ex);
        _logger.Received(1).Log
        (
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Unhandled Exception")),
            exception,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    public class DummyRequest;
}