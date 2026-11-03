using Microsoft.Playwright;

namespace Gamer.Api.Features.Deals;

public sealed class BrowserPageFetcher : IPageFetcher, IAsyncDisposable
{
    private const string InstallHint = "Browser not available. Install it with: pwsh bin/Debug/net10.0/playwright.ps1 install chromium";

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _pages = new(2, 2);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync(cancellationToken);
        await _pages.WaitAsync(cancellationToken);
        try
        {
            await using IBrowserContext context = await browser.NewContextAsync();
            IPage page = await context.NewPageAsync();
            IResponse? response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000,
            });
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
