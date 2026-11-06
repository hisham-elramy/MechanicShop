using System.Net;
using System.Net.Http.Headers;

namespace MechanicShop.Client.Identity;

public class BearerTokenHandler(IAccountManagement accountManagement) : DelegatingHandler
{
    private readonly IAccountManagement _accountManagement = accountManagement;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authResult = await _accountManagement.LoadAccessTokenFromStorage();

        if (authResult?.AccessToken is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !request.Headers.Contains("X-Retry"))
        {
            var newTokenResponse = await _accountManagement.RefreshTokenAsync();

            if (newTokenResponse is not null)
            {
                var newRequest = CloneRequest(request);
                newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newTokenResponse.AccessToken);
                newRequest.Headers.Add("X-Retry", "true");

                return await base.SendAsync(newRequest, cancellationToken);

            }

            await _accountManagement.LogoutAsync();
        }

        return response;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var newRequest = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content != null)
        {
            var memoryStream = new MemoryStream();
            request.Content.CopyToAsync(memoryStream).Wait();
            memoryStream.Position = 0;
            newRequest.Content = new StreamContent(memoryStream);

            foreach(var header in request.Content.Headers)
            {
                newRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach(var header in request.Headers)
        {
            newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return newRequest;
    }
}