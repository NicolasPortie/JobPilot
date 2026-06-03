using JobPilot.Agent.Application;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInLoginService
{
    private readonly AgentOptions _options;

    public LinkedInLoginService(AgentOptions options) => _options = options;

    public async Task<bool> NeedsLoginAsync(IPage page)
    {
        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[LoginCheck] URL contains /login");
            return true;
        }

        var emailCount = await page.Locator("input[name='session_key']:visible, input#username:visible, input[name='email-or-phone']:visible, input[type='email']:visible").CountAsync();
        var passwordCount = await page.Locator("input[name='session_password']:visible, input#password:visible, input[type='password']:visible").CountAsync();
        Console.WriteLine($"[LoginCheck] emailInputs={emailCount}, passwordInputs={passwordCount}");
        if (emailCount > 0 && passwordCount > 0) return true;

        try
        {
            var visibleText = await PageObserver.GetVisibleTextAsync(page);
            var hasEntre = visibleText.Contains("Entre para ver quem", StringComparison.OrdinalIgnoreCase);
            var hasEntrarCadastro = visibleText.Contains("Entrar", StringComparison.OrdinalIgnoreCase) &&
                                    visibleText.Contains("Cadastre-se agora", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[LoginCheck] textCheck: hasEntre={hasEntre}, hasEntrarCadastro={hasEntrarCadastro}");

            if ((hasEntre || hasEntrarCadastro) && page.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase))
            {
                var easyApplyCount = await page.Locator(LinkedInSelectors.EasyApplyButton).CountAsync();
                Console.WriteLine($"[LoginCheck] EasyApply buttons={easyApplyCount}");
                if (easyApplyCount == 0) return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoginCheck] Error reading body text: {ex.Message}");
        }

        if (page.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase))
        {
            var headerSignIn = await page.Locator("header a[href*='login'], .nav__button-secondary:has-text('Entrar'), [data-tracking-control-name*='signin']").CountAsync();
            var easyApplyCount = await page.Locator(LinkedInSelectors.EasyApplyButton).CountAsync();
            Console.WriteLine($"[LoginCheck] headerSignIn={headerSignIn}, easyApply={easyApplyCount}");
            if (headerSignIn > 0 && easyApplyCount == 0) return true;
        }

        Console.WriteLine("[LoginCheck] No login detected");
        return false;
    }

    public async Task<bool> TryLoginAsync(IPage page, string? email = null, string? password = null)
    {
        var loginEmail = email ?? _options.LinkedInEmail;
        var loginPassword = password ?? _options.LinkedInPassword;
        if (string.IsNullOrWhiteSpace(loginEmail) || string.IsNullOrWhiteSpace(loginPassword))
        {
            Console.WriteLine("[Login] Credenciais não configuradas.");
            return false;
        }

        try
        {
            Console.WriteLine("[Login] Navegando para página de login...");
            await page.GotoAsync("https://www.linkedin.com/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.WaitForTimeoutAsync(2000);

            var username = page.Locator("input[name='session_key']:visible, input#username:visible, input[name='email-or-phone']:visible, input[type='email']:visible").First;
            var passwordInput = page.Locator("input[name='session_password']:visible, input#password:visible, input[type='password']:visible").First;

            if (await username.CountAsync() == 0 || await passwordInput.CountAsync() == 0)
            {
                Console.WriteLine("[Login] Inputs de login não encontrados.");
                return false;
            }

            Console.WriteLine("[Login] Preenchendo credenciais...");
            await username.FillAsync(loginEmail);
            await passwordInput.FillAsync(loginPassword);
            await passwordInput.PressAsync("Enter");

            var waitSeconds = Math.Clamp(_options.LinkedInLoginWaitSeconds, 5, 120);
            Console.WriteLine($"[Login] Aguardando {waitSeconds}s após login...");
            await page.WaitForTimeoutAsync(waitSeconds * 1000);

            var stillNeedsLogin = await NeedsLoginAsync(page);
            Console.WriteLine($"[Login] Ainda precisa de login? {stillNeedsLogin}");
            return !stillNeedsLogin;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Login] Erro: {ex.Message}");
            return false;
        }
    }
}
