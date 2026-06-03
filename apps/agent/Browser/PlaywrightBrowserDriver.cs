using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Browser;

public sealed class PlaywrightBrowserDriver : IBrowserDriver
{
    private readonly IPage _page;

    public PlaywrightBrowserDriver(IPage page) => _page = page;

    public string CurrentUrl => _page.Url;

    public Task<PageObservation> ObserveAsync() =>
        PageObserver.ObserveAsync(_page);

    public async Task ClickAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var locator = Locate(elementId);
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 6000 });
    }

    public async Task FillAsync(string elementId, string value, CancellationToken cancellationToken = default)
    {
        var locator = Locate(elementId);
        await locator.FillAsync(value);
    }

    public async Task SelectAsync(string elementId, string value, CancellationToken cancellationToken = default)
    {
        var locator = Locate(elementId);
        try
        {
            await locator.SelectOptionAsync(new[] { value });
            return;
        }
        catch
        {
        }

        try
        {
            await locator.SelectOptionAsync(new[] { new SelectOptionValue { Label = value } });
            return;
        }
        catch
        {
        }

        await locator.ClickAsync(new LocatorClickOptions { Timeout = 6000 });
        await _page.WaitForTimeoutAsync(250);

        var selected = await _page.EvaluateAsync<bool>("""
            (optionText) => {
              const clean = value => (value || '').replace(/\s+/g, ' ').trim();
              const wanted = clean(optionText).toLowerCase();
              const visible = el => {
                const style = window.getComputedStyle(el);
                const rect = el.getBoundingClientRect();
                return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
              };
              const candidates = Array.from(document.querySelectorAll('[role="option"], [role="menuitem"], li, button'));
              const matches = candidates.filter(el => {
                if (!visible(el)) return false;
                const text = clean(el.innerText || el.textContent || el.getAttribute('aria-label') || '');
                return text.toLowerCase() === wanted;
              });
              if (matches.length !== 1) return false;
              const match = matches[0];
              match.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, view: window }));
              match.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true, view: window }));
              match.click();
              return true;
            }
        """, value);

        if (!selected)
            throw new InvalidOperationException($"Could not select observed option '{value}' without guessing.");
    }

    public async Task CheckAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var locator = Locate(elementId);
        await locator.CheckAsync();
    }

    public async Task UploadFileAsync(string elementId, string filePath, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[UPLOAD] Tentando anexar arquivo via input observado: element={elementId}; file={Path.GetFileName(filePath)}");
        var locator = Locate(elementId, visibleOnly: false);
        await locator.SetInputFilesAsync(filePath);
        var attached = await locator.EvaluateAsync<int>("el => el.files ? el.files.length : 0");
        Console.WriteLine($"[UPLOAD] Arquivo enviado para input observado: element={elementId}; files={attached}");
    }

    public async Task ScrollAsync(CancellationToken cancellationToken = default)
    {
        await PageObserver.ScrollActiveSurfaceAsync(_page);
    }

    public Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default) =>
        PageObserver.CaptureEvidenceAsync(_page, reasonCode);

    private ILocator Locate(string elementId, bool visibleOnly = true)
    {
        var selector = $"[data-jobpilot-id='{elementId}']";
        if (visibleOnly)
            selector += ":visible";

        return _page.Locator(selector).First;
    }
}
