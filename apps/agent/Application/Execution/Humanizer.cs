using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public static class Humanizer
{
    private static readonly Random Rng = new();

    public static async Task DelayAsync(int minMs = 700, int maxMs = 1400)
    {
        await Task.Delay(Rng.Next(minMs, maxMs));
    }

    public static async Task TypeHumanAsync(ILocator locator, string text)
    {
        foreach (var ch in text)
        {
            await locator.PressAsync(ch.ToString());
            await Task.Delay(Rng.Next(30, 120));
        }
    }

    public static async Task ScrollNaturalAsync(IPage page, int deltaY = 900)
    {
        await page.Mouse.WheelAsync(0, deltaY);
        await Task.Delay(Rng.Next(400, 900));
    }

    public static async Task ClickHumanAsync(ILocator locator)
    {
        await Task.Delay(Rng.Next(200, 600));
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 6000 });
        await Task.Delay(Rng.Next(300, 700));
    }
}
