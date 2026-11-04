using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Gamer.Api.Features.Deals;

public sealed class HttpPageFetcher(HttpClient http) : IPageFetcher
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(800);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly ConcurrentDictionary<string, DateTime> LastRequests = new();

    public static HttpPageFetcher Create()
    {
        HttpClient client = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GamerPriceCheck/1.0; personal use)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
        return new HttpPageFetcher(client);
    }

    public async Task<string> FetchAsync(string url, string? waitForSelector, CancellationToken cancellationToken)
    {
        string host = new Uri(url).Host;
        SemaphoreSlim gate = Gates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (LastRequests.TryGetValue(host, out DateTime last))
            {
                TimeSpan wait = MinimumInterval - (DateTime.UtcNow - last);
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }
            }

            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
            LastRequests[host] = DateTime.UtcNow;
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (BlockDetector.IsBlocked((int)response.StatusCode, body))
            {
                throw new BlockedException($"{host} blocked the request ({(int)response.StatusCode}).");
            }

            response.EnsureSuccessStatusCode();
            return body;
        }
        finally
        {
            gate.Release();
        }
    }
}
