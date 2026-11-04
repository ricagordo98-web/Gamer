using Microsoft.Playwright;

namespace Gamer.Api.Features.Deals;

public sealed class BrowserPageFetcher : IPageFetcher, IAsyncDisposable
{
    private const string InstallHint = "Browser not available. Install it with: pwsh bin/Debug/net10.0/playwright.ps1 install chromium";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
    private const float SelectorTimeoutMs = 15_000;

    private static readonly HashSet<string> SkippedResources = ["image", "media", "font"];

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _pages = new(2, 2);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<string> FetchAsync(string url, string? waitForSelector, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync(cancellationToken);
        await _pages.WaitAsync(cancellationToken);
        try
        {
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "pt-PT",
                UserAgent = UserAgent,
            });
            await context.RouteAsync("**/*", route => SkippedResources.Contains(route.Request.ResourceType)
                ? route.AbortAsync()
                : route.ContinueAsync());

            IPage page = await context.NewPageAsync();
            IResponse? response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            });

            if (waitForSelector is not null)
            {
                await WaitForItemsAsync(page, waitForSelector);
            }

            string html = await page.ContentAsync();
            if (BlockDetector.IsBlocked(response?.Status ?? 200, html))
            {
                throw new BlockedException($"{new Uri(url).Host} blocked the browser request.");
            }

            return html;
        }
        catch (PlaywrightException exception)
        {
            throw new BrowserUnavailableException(exception.Message, exception);
        }
        finally
        {
            _pages.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    private static async Task WaitForItemsAsync(IPage page, string selector)
    {
        try
        {
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = SelectorTimeoutMs,
            });
            await page.WaitForTimeoutAsync(600);
        }
        catch (TimeoutException)
        {
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is null)
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }

            return _browser;
        }
        catch (PlaywrightException exception)
        {
            throw new BrowserUnavailableException(InstallHint, exception);
        }
        finally
        {
            _startLock.Release();
        }
    }
}
