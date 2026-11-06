using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public sealed class FakeTokenProvider : ITokenProvider
{
    public ClaimsPrincipal? Principal { get; set; }
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token) => Principal;
    public List<AppUserDto> UsersSeen { get; } = [];
    public Result<TokenResponse>? Override { get; set; }

    public Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default)
    {
        UsersSeen.Add(user);

        Result<TokenResponse> result = Override ?? new TokenResponse
        {
            AccessToken = $"access-{user.UserId}",
            RefreshToken = Guid.NewGuid().ToString("N"),
            ExpiresOnUtc = DateTime.UtcNow.AddMinutes(15)
        };

        return Task.FromResult(result);
    }
}