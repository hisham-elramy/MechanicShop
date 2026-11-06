using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public sealed class FakeIdentityService : IIdentityService
{
    private readonly Dictionary<string, AppUserDto> _usersById = [];
    private readonly Dictionary<string, (string Password, AppUserDto User)> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public List<string?> RequestedUserIds { get; } = [];
    public List<(string Email, string Password)> AuthenticationAttempts { get; } = [];

    public static Error NotFound => Error.NotFound("Auth.User.NotFound", "User not found.");
    public static Error InvalidCredentials => Error.Validation("Auth.Credentials.Invalid", "Invalid credentials.");

    public FakeIdentityService WithUser(AppUserDto user, string password)
    {
        _usersById[user.UserId] = user;
        _credentials[user.Email] = (password, user);
        return this;
    }

    public Task<Result<AppUserDto>> GetUserByIdAsync(string userId)
    {
        RequestedUserIds.Add(userId);

        Result<AppUserDto> result = userId is not null && _usersById.TryGetValue(userId, out var user)
            ? user
            : NotFound;

        return Task.FromResult(result);
    }

    public Task<Result<AppUserDto>> AuthenticateAsync(string email, string password)
    {
        AuthenticationAttempts.Add((email, password));

        Result<AppUserDto> result = _credentials.TryGetValue(email, out var entry) && entry.Password == password
            ? entry.User
            : InvalidCredentials;

        return Task.FromResult(result);
    }

    public Task<bool> IsInRoleAsync(string userId, string role)
    {
        return Task.FromResult(_usersById.TryGetValue(userId, out var u) && u.Roles.Contains(role));
    }

    public Task<bool> AuthorizeAsync(string userId, string? policyName)
    {
        return Task.FromResult(true);
    }

    public Task<string?> GetUsernameAsync(string userId)
    {
        return Task.FromResult(_usersById.TryGetValue(userId, out var u) ? u.Email : null);
    }
}

public static class TestUserDtos
{
    public static AppUserDto Manager() => new(
        "19a59129-6c20-417a-834d-11a208d32d96", "pm@localhost", ["Manager"], []);

    public static AppUserDto Labor() => new(
        "b6327240-0aea-46fc-863a-777fc4e42560", "john.labor@localhost", ["Labor"],
        [new Claim("custom", "value")]);
}