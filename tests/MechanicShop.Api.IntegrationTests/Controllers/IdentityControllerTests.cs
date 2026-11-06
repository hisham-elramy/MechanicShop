using System.Net;
using System.Net.Http.Json;
using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Tests.Common.Security;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class IdentityControllerTests(WebAppFactory factory)
{
    private const string GenerateUrl = "identity/token/generate";
    private const string RefreshUrl = "identity/token/refresh-token";
    private const string CurrentUserUrl = "identity/current-user/claims";

    private readonly AppHttpClient _client = factory.CreateAppHttpClient();

    private sealed record CurrentUser(string UserId, string Email, List<string> Roles);

    // ---------- POST token/generate ----------

    [Fact]
    public async Task GenerateToken_WithValidCredentials_ShouldReturnTokens()
    {
        var response = await Login(TestUsers.Manager.Email!, TestUsers.Manager.Email!);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresOnUtc > DateTime.UtcNow);
    }

    // FAILS with the original: no Cache-Control header on a token response
    [Fact]
    public async Task GenerateToken_ShouldNotBeCacheable()
    {
        var response = await Login(TestUsers.Manager.Email!, TestUsers.Manager.Email!);

        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    [Fact]
    public async Task GenerateToken_ShouldReturnDifferentRefreshTokens_OnEachLogin()
    {
        var first = await _client.GenerateTokensAsync(TestUsers.Manager);
        var second = await _client.GenerateTokensAsync(TestUsers.Manager);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact]
    public async Task GenerateToken_WithWrongPassword_ShouldFail()
    {
        var response = await Login(TestUsers.Manager.Email!, "wrong-password");

        Assert.True(IsClientError(response.StatusCode));
    }

    [Fact]
    public async Task GenerateToken_WithUnknownEmail_ShouldFail()
    {
        var response = await Login("nobody@localhost", "whatever");

        Assert.True(IsClientError(response.StatusCode));
    }

    // Guards against user enumeration: both failures must look identical
    [Fact]
    public async Task GenerateToken_ShouldReturnTheSameFailure_ForUnknownEmailAndWrongPassword()
    {
        var unknown = await Login("nobody@localhost", "whatever");
        var wrong = await Login(TestUsers.Manager.Email!, "wrong-password");

        Assert.Equal(unknown.StatusCode, wrong.StatusCode);

        var a = await unknown.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var b = await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(a?.Title, b?.Title);
        Assert.Equal(a?.Detail, b?.Detail);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("user@localhost", "")]
    [InlineData("", "")]
    public async Task GenerateToken_WithMissingFields_ShouldReturnBadRequest(string email, string password)
    {
        var response = await Login(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateToken_WithMalformedBody_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync(GenerateUrl, "this-is-not-an-object");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- GET current-user/claims ----------

    [Fact]
    public async Task GetCurrentUser_AsManager_ShouldReturnTheManager()
    {
        _client.SetAuthorizationHeader(await _client.GenerateTokenAsync(TestUsers.Manager));

        var response = await _client.GetAsync(CurrentUserUrl);
        var user = await ReadUserAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestUsers.Manager.Id, user.UserId);
        Assert.Equal(TestUsers.Manager.Email, user.Email, ignoreCase: true);
        Assert.Contains("Manager", user.Roles);
    }

    [Fact]
    public async Task GetCurrentUser_AsLabor_ShouldReturnTheLaborAndNotManagerRole()
    {
        _client.SetAuthorizationHeader(await _client.GenerateTokenAsync(TestUsers.Labor01));

        var user = await ReadUserAsync(await _client.GetAsync(CurrentUserUrl));

        Assert.Equal(TestUsers.Labor01.Id, user.UserId);
        Assert.Contains("Labor", user.Roles);
        Assert.DoesNotContain("Manager", user.Roles);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(CurrentUserUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithGarbageToken_ShouldReturnUnauthorized()
    {
        _client.SetAuthorizationHeader("not.a.jwt");

        var response = await _client.GetAsync(CurrentUserUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithTamperedSignature_ShouldReturnUnauthorized()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Manager);
        var tampered = token[..^2] + (token[^2] == 'A' ? "B" : "A") + token[^1];
        _client.SetAuthorizationHeader(tampered);

        var response = await _client.GetAsync(CurrentUserUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- POST token/refresh-token ----------

    [Fact]
    public async Task RefreshToken_WithValidPair_ShouldReturnNewTokens()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);

        var response = await Refresh(tokens.RefreshToken!, tokens.AccessToken!);
        var renewed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(renewed!.AccessToken));
        Assert.NotEqual(tokens.RefreshToken, renewed.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnAnAccessTokenThatWorks()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Labor01);
        var renewed = await (await Refresh(tokens.RefreshToken!, tokens.AccessToken!))
            .Content.ReadFromJsonAsync<TokenResponse>();

        _client.SetAuthorizationHeader(renewed!.AccessToken!);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(CurrentUserUrl)).StatusCode);
    }

    // FAILS with the original: the used refresh token is never revoked
    [Fact]
    public async Task RefreshToken_ReusingTheSameRefreshToken_ShouldFail()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);
        Assert.Equal(HttpStatusCode.OK, (await Refresh(tokens.RefreshToken!, tokens.AccessToken!)).StatusCode);

        var second = await Refresh(tokens.RefreshToken!, tokens.AccessToken!);

        Assert.True(IsClientError(second.StatusCode));
    }

    [Fact]
    public async Task RefreshToken_WithAnotherUsersRefreshToken_ShouldFail()
    {
        var manager = await _client.GenerateTokensAsync(TestUsers.Manager);
        var labor = await _client.GenerateTokensAsync(TestUsers.Labor01);

        var response = await Refresh(manager.RefreshToken!, labor.AccessToken!);

        Assert.True(IsClientError(response.StatusCode));
    }

    [Fact]
    public async Task RefreshToken_WithUnknownRefreshToken_ShouldFail()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);

        var response = await Refresh("no-such-refresh-token", tokens.AccessToken!);

        Assert.True(IsClientError(response.StatusCode));
    }

    [Fact]
    public async Task RefreshToken_WithGarbageAccessToken_ShouldFail()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);

        var response = await Refresh(tokens.RefreshToken!, "garbage");

        Assert.True(IsClientError(response.StatusCode));
    }

    [Fact]
    public async Task RefreshToken_WithTamperedAccessToken_ShouldFail()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);
        var access = tokens.AccessToken!;
        var tampered = access[..^2] + (access[^2] == 'A' ? "B" : "A") + access[^1];

        var response = await Refresh(tokens.RefreshToken!, tampered);

        Assert.True(IsClientError(response.StatusCode));
    }

    // DECISION TEST: an invalid or expired refresh token is a 401. FAILS with the original (409).
    [Fact]
    public async Task RefreshToken_WithUnknownRefreshToken_ShouldReturnUnauthorized()
    {
        var tokens = await _client.GenerateTokensAsync(TestUsers.Manager);

        var response = await Refresh("no-such-refresh-token", tokens.AccessToken!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync(GenerateUrl, new { email, password });

    private Task<HttpResponseMessage> Refresh(string refreshToken, string expiredAccessToken) =>
        _client.PostAsJsonAsync(RefreshUrl, new { refreshToken, expiredAccessToken });

    private static bool IsClientError(HttpStatusCode code) => (int)code is >= 400 and < 500;

    private static async Task<CurrentUser> ReadUserAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<CurrentUser>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
}