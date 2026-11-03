using System.Net;
using System.Net.Http.Headers;

namespace Gamer.Ui.Services;

public class ApiHandler(ServerSettings settings, AuthSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string baseUrl = await settings.GetBaseUrlAsync();
        request.RequestUri = new Uri(new Uri(baseUrl), request.RequestUri!.PathAndQuery.TrimStart('/'));

        if (session.Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && session.IsAuthenticated)
        {
            await session.SignOutAsync();
        }

        return response;
    }
}
