using JobPilot.Agent.Application;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInLoginService
{
    private readonly AgentOptions _options;

    public LinkedInLoginService(AgentOptions options) => _options = options;

    public async Task<bool> HasPersistedSessionAsync(IPage page)
    {
        try
        {
            var cookies = await page.Context.CookiesAsync(["https://www.linkedin.com", "https://www.linkedin.com/"]);
            return cookies.Any(c =>
                c.Name.Equals("li_at", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(c.Value));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoginCheck] Erro ao ler cookies: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> HasAuthenticatedNavAsync(IPage page)
    {
        try
        {
            var navCount = await page.Locator(
                "[data-control-name='identity_welcome_message'], .global-nav__me, .global-nav__me-content, img.global-nav__me-photo, a.global-nav__primary-link[href*='/in/']").CountAsync();
            return navCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> NeedsLoginAsync(IPage page)
    {
        if (await HasPersistedSessionAsync(page))
        {
            if (await HasAuthenticatedNavAsync(page))
            {
                Console.WriteLine("[LoginCheck] Sessao autenticada (cookie li_at + navegacao logada).");
                return false;
            }

            var jobLinks = await page.Locator("a[href*='/jobs/view/']").CountAsync();
            if (jobLinks > 0 && !page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[LoginCheck] Sessao autenticada (cookie li_at + resultados de vagas visiveis).");
                return false;
            }
        }

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
            var hasLoginCadastro = visibleText.Contains("Login", StringComparison.OrdinalIgnoreCase) &&
                                    visibleText.Contains("Cadastre-se agora", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[LoginCheck] textCheck: hasEntre={hasEntre}, hasLoginCadastro={hasLoginCadastro}");

            // Banner de visitante sozinho nao exige login se a sessao persistida ainda e valida.
            if ((hasEntre || hasLoginCadastro) &&
                page.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase) &&
                !await HasPersistedSessionAsync(page))
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

        if (page.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase) &&
            !await HasPersistedSessionAsync(page))
        {
            var headerSignIn = await page.Locator("header a[href*='login'], .nav__button-secondary:has-text('Login'), [data-tracking-control-name*='signin']").CountAsync();
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
            Console.WriteLine("[Login] Credenciais automaticas nao disponiveis (email/senha ausentes).");
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

    public async Task<bool> TryRestorePersistedSessionAsync(IPage page, string returnUrl)
    {
        if (!await HasPersistedSessionAsync(page))
            return false;

        Console.WriteLine("[Login] Tentando reativar sessao persistida via feed...");
        await page.GotoAsync("https://www.linkedin.com/feed/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(2500);

        if (await NeedsLoginAsync(page))
            return false;

        await page.GotoAsync(returnUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(2500);
        return !await NeedsLoginAsync(page);
    }
}
