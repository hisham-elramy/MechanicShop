using System.Security.Claims;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public sealed class RefreshTokenQueryHandler(
    IAppDbContext context,
    ILogger<RefreshTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider,
    IAppClock clock

)
: IRequestHandler<RefreshTokenQuery, Result<TokenResponse>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RefreshTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly ITokenProvider _tokenProvider = tokenProvider;
    private readonly IAppClock _clock = clock;

    public async Task<Result<TokenResponse>> Handle(RefreshTokenQuery request, CancellationToken cancellationToken)
    {
        var principal = _tokenProvider.GetPrincipalFromExpiredToken(request.ExpiredAccessToken);

        if (principal is null)
        {
            _logger.LogError("Expired access token is not valid");

            return ApplicationErrors.ExpiredAccessTokenInvalid;
        }

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
          ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
        {
            _logger.LogError("Invalid userId claim");

            return ApplicationErrors.UserIdClaimInvalid;
        }

        var getUserResult = await _identityService.GetUserByIdAsync(userId);

        if (getUserResult.IsError)
        {
            _logger.LogError("Get user by id error occurred: {ErrorDescription}", getUserResult.TopError.Description);

            return getUserResult.Errors;
        }

        var refreshToken = await _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync
            (
                rt => rt.Token == request.RefreshToken &&
                rt.UserId == userId, cancellationToken
            );

        if (refreshToken is null || refreshToken.ExpiresOnUtc < _clock.UtcNow)
        {
            _logger.LogError("Refresh token has expired");

            return ApplicationErrors.RefreshTokenExpired;
        }

        var generateTokenResult = await _tokenProvider
            .GenerateJwtTokenAsync(getUserResult.Value, cancellationToken);

        if (generateTokenResult.IsError)
        {
            _logger.LogError("Generate token error occurred: {ErrorDescription}", generateTokenResult.TopError.Description);
            return generateTokenResult.Errors;
        }

        return generateTokenResult.Value;
    }
}