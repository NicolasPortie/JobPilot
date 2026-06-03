using System.Net;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;
using JobPilot.Agent.Platforms.LinkedIn;
using Microsoft.Playwright;

namespace JobPilot.Agent.Tests;

public class IntegrationTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private readonly string _testFormPath = Path.GetFullPath("test-form.html");

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-dev-shm-usage"]
        });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task Agent_Preenche_Formulario_Com_Dados_Do_Perfil()
    {
        // Arrange: profile de teste
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            Cpf = "12345678909",
            Email = "nicolas@email.com",
            Phone = "11999998888",
            City = "São Paulo",
            LinkedinUrl = "https://linkedin.com/in/nicolas",
            SalaryExpectation = "5000"
        };

        // Arrange: resolver com mock HTTP
        var mockHttp = new MockHttpMessageHandler();
        var client = new JobPilotApiClient("http://localhost:5000", "fake-token");
        ReplaceHttpClient(client, mockHttp);

        var resolver = new LocalAnswerResolver(client);
        resolver.SetProfile(profile);
        await resolver.LoadAsync();

        var session = new SessionContext();

        // Arrange: abrir formulário de teste
        await _page!.GotoAsync($"file:///{_testFormPath.Replace("\\", "/")}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act: observar e preencher campo a campo usando resolução semântica
        var observation = await PageObserver.ObserveAsync(_page);

        foreach (var field in observation.Elements.Where(e => e.IsTextEntry && !e.IsFile))
        {
            var resolution = resolver.ResolveSemantic(field, session);
            if (resolution?.Value is not null)
            {
                var locator = _page.Locator($"[data-jobpilot-id='{field.ElementId}']").First;
                await locator.FillAsync(resolution.Value);
                session.Record(field.ElementId, resolution.Value, resolution.InferredType);
                await _page.WaitForTimeoutAsync(150);
            }
        }

        // Assert: validar valores preenchidos
        var fullname = await _page.InputValueAsync("#fullname");
        var email = await _page.InputValueAsync("#email");
        var phone = await _page.InputValueAsync("#phone");
        var cpf = await _page.InputValueAsync("#cpf");
        var city = await _page.InputValueAsync("#city");
        var linkedin = await _page.InputValueAsync("#linkedin");

        Assert.Equal("Nicolas Portie Suss", fullname);
        Assert.Equal("nicolas@email.com", email);
        Assert.Equal("(11) 99999-8888", phone);
        Assert.Equal("123.456.789-09", cpf);
        Assert.Equal("São Paulo", city);
        Assert.Equal("https://linkedin.com/in/nicolas", linkedin);
    }

    [Fact]
    public async Task Agent_Nao_Preenche_Cpf_Com_Nome()
    {
        // Teste de regressão: garante que CPF não recebe o nome
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            Cpf = "12345678909",
            Email = "nicolas@email.com",
            Phone = "11999998888",
            City = "São Paulo"
        };

        var mockHttp = new MockHttpMessageHandler();
        var client = new JobPilotApiClient("http://localhost:5000", "fake-token");
        ReplaceHttpClient(client, mockHttp);

        var resolver = new LocalAnswerResolver(client);
        resolver.SetProfile(profile);
        await resolver.LoadAsync();

        var session = new SessionContext();

        await _page!.GotoAsync($"file:///{_testFormPath.Replace("\\", "/")}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var observation = await PageObserver.ObserveAsync(_page);
        var cpfField = observation.Elements.First(e =>
            e.Label.Contains("CPF", StringComparison.OrdinalIgnoreCase) ||
            e.Placeholder.Contains("000.000.000", StringComparison.OrdinalIgnoreCase));
        var resolution = resolver.ResolveSemantic(cpfField, session);

        Assert.NotNull(resolution);
        Assert.Equal(SemanticFieldType.Cpf, resolution.InferredType);
        Assert.Equal("123.456.789-09", resolution.Value);
        Assert.NotEqual(profile.FullName, resolution.Value);
    }

    [Fact]
    public async Task Agent_Valida_Cpf_Apos_Preenchimento()
    {
        // Teste de self-healing: valida CPF após preenchimento
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            Cpf = "12345678909",
            Email = "nicolas@email.com",
            Phone = "11999998888",
            City = "São Paulo"
        };

        var mockHttp = new MockHttpMessageHandler();
        var client = new JobPilotApiClient("http://localhost:5000", "fake-token");
        ReplaceHttpClient(client, mockHttp);

        var resolver = new LocalAnswerResolver(client);
        resolver.SetProfile(profile);
        await resolver.LoadAsync();

        var session = new SessionContext();
        var validator = new FieldValidator();

        await _page!.GotoAsync($"file:///{_testFormPath.Replace("\\", "/")}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var observation = await PageObserver.ObserveAsync(_page);
        var cpfField = observation.Elements.First(e =>
            e.Label.Contains("CPF", StringComparison.OrdinalIgnoreCase) ||
            e.Placeholder.Contains("000.000.000", StringComparison.OrdinalIgnoreCase));

        // Preenche com CPF correto
        var resolution = resolver.ResolveSemantic(cpfField, session);
        Assert.NotNull(resolution);
        var locator = _page.Locator($"[data-jobpilot-id='{cpfField.ElementId}']").First;
        await locator.FillAsync(resolution.Value!);

        // Re-observa e valida
        observation = await PageObserver.ObserveAsync(_page);
        cpfField = observation.Elements.First(e =>
            e.Label.Contains("CPF", StringComparison.OrdinalIgnoreCase) ||
            e.Placeholder.Contains("000.000.000", StringComparison.OrdinalIgnoreCase));

        var validation = validator.Validate(cpfField, resolution.Value!);
        Assert.True(validation.IsValid, $"CPF válido deveria passar: {validation.ErrorMessage}");

        // Agora testa com CPF inválido (nome)
        await locator.FillAsync(profile.FullName);
        observation = await PageObserver.ObserveAsync(_page);
        cpfField = observation.Elements.First(e =>
            e.Label.Contains("CPF", StringComparison.OrdinalIgnoreCase) ||
            e.Placeholder.Contains("000.000.000", StringComparison.OrdinalIgnoreCase));

        validation = validator.Validate(cpfField, profile.FullName);
        Assert.False(validation.IsValid, "Nome no campo CPF deveria falhar validação");
    }

    [Fact]
    public async Task Agent_SessionContext_Evita_Duplicatas()
    {
        // Teste: SessionContext impede que o nome seja reutilizado em outro campo
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            Cpf = "12345678909",
            Email = "nicolas@email.com",
            Phone = "11999998888",
            City = "São Paulo"
        };

        var mockHttp = new MockHttpMessageHandler();
        var client = new JobPilotApiClient("http://localhost:5000", "fake-token");
        ReplaceHttpClient(client, mockHttp);

        var resolver = new LocalAnswerResolver(client);
        resolver.SetProfile(profile);
        await resolver.LoadAsync();

        var session = new SessionContext();

        await _page!.GotoAsync($"file:///{_testFormPath.Replace("\\", "/")}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var observation = await PageObserver.ObserveAsync(_page);

        // Preenche nome completo
        var nameField = observation.Elements.First(e => e.Label.Contains("Nome completo", StringComparison.OrdinalIgnoreCase));
        var nameRes = resolver.ResolveSemantic(nameField, session);
        Assert.NotNull(nameRes);
        session.Record(nameField.ElementId, nameRes.Value!, nameRes.InferredType);

        // Tenta usar o mesmo valor (nome) em um campo de CPF
        var cpfField = observation.Elements.First(e =>
            e.Label.Contains("CPF", StringComparison.OrdinalIgnoreCase) ||
            e.Placeholder.Contains("000.000.000", StringComparison.OrdinalIgnoreCase));

        var cpfRes = resolver.ResolveSemantic(cpfField, session);
        Assert.NotNull(cpfRes);
        Assert.Equal(SemanticFieldType.Cpf, cpfRes.InferredType);
        Assert.NotEqual(profile.FullName, cpfRes.Value);
        Assert.True(session.WasValueUsed(profile.FullName, SemanticFieldType.Cpf),
            "SessionContext deve marcar o nome como 'já usado' para tipos não-relacionados como CPF");
    }

    [Fact]
    public async Task PageObserver_Captura_Combobox_Customizado()
    {
        await _page!.SetContentAsync("""
            <label id="node-label">Voce possui experiencia pratica com Node.js?</label>
            <button role="combobox" aria-labelledby="node-label" aria-controls="node-options" aria-required="true">Selecionar opcao</button>
            <ul id="node-options">
              <li role="option">Sim</li>
              <li role="option">Nao</li>
            </ul>
        """);

        var observation = await PageObserver.ObserveAsync(_page);
        var combo = observation.Elements.FirstOrDefault(e => e.Role == "combobox");

        Assert.NotNull(combo);
        Assert.True(combo.IsSelect);
        Assert.Contains("Sim", combo.Options);
        Assert.Contains("Nao", combo.Options);
    }

    [Fact]
    public async Task PageObserver_MarcaDropdownLinkedInComAsteriscoComoObrigatorio()
    {
        await _page!.SetContentAsync("""
            <div class="jobs-easy-apply-form-section__grouping">
              <label>Voce possui conhecimento em Git e GitHub?*</label>
              <button class="artdeco-dropdown__trigger">Selecionar opcao</button>
            </div>
        """);

        var observation = await PageObserver.ObserveAsync(_page);
        var dropdown = observation.Elements.FirstOrDefault(e => e.Text == "Selecionar opcao");

        Assert.NotNull(dropdown);
        Assert.True(dropdown.IsSelect);
        Assert.True(dropdown.Required);
    }

    [Fact]
    public async Task PlaywrightDriver_AnexaCurriculo_EmInputFileEscondido()
    {
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(resumePath, "fake resume");
        try
        {
            await _page!.SetContentAsync("""
                <form>
                  <label for="resume">Anexar curriculo *</label>
                  <button type="button" onclick="document.getElementById('resume').click()">Selecionar arquivo</button>
                  <input id="resume" name="resume" type="file" required style="display:none"
                         onchange="document.getElementById('status').textContent = this.files[0]?.name || ''">
                  <div id="status"></div>
                </form>
            """);

            var observation = await PageObserver.ObserveAsync(_page);
            var fileInput = observation.Elements.FirstOrDefault(e => e.IsFile);

            Assert.NotNull(fileInput);

            var driver = new PlaywrightBrowserDriver(_page);
            await driver.UploadFileAsync(fileInput.ElementId, resumePath);

            var attachedCount = await _page.Locator("#resume").EvaluateAsync<int>("el => el.files.length");
            var status = await _page.Locator("#status").InnerTextAsync();

            Assert.Equal(1, attachedCount);
            Assert.Equal(Path.GetFileName(resumePath), status);
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task LinkedInSurface_AbreEasyApplySemVirarManual()
    {
        await _page!.SetContentAsync("""
            <section class="jobs-details__main-content">
              <button onclick="document.body.insertAdjacentHTML('beforeend', '<div role=&quot;dialog&quot;><div class=&quot;jobs-easy-apply-content&quot;>Candidatura simplificada</div></div>')">Candidatura simplificada</button>
            </section>
        """);
        var surface = new LinkedInEasyApplySurface();

        var result = await surface.OpenAsync(_page);

        Assert.True(result.Opened);
        Assert.Equal("linkedin_easy_apply", result.SurfaceKind);
        Assert.Equal("LinkedIn", result.SuggestedPlatform);
        Assert.NotEqual("manual_only", result.FailureState);
    }

    [Fact]
    public async Task LinkedInSurface_UsaFallbackGlobalQuandoBotaoFicaForaDoPainel()
    {
        await _page!.SetContentAsync("""
            <main>
              <section class="jobs-details__main-content"><h1>Vaga</h1></section>
              <button onclick="document.body.insertAdjacentHTML('beforeend', '<div role=&quot;dialog&quot;><div class=&quot;jobs-easy-apply-content&quot;>Candidatura simplificada</div></div>')">Candidatura simplificada</button>
            </main>
        """);
        var surface = new LinkedInEasyApplySurface();

        var result = await surface.OpenAsync(_page);

        Assert.True(result.Opened);
        Assert.Equal("linkedin_easy_apply", result.SurfaceKind);
        Assert.NotEqual("manual_only", result.FailureState);
    }

    [Fact]
    public async Task LinkedInSurface_CandidatarSeExternoNovaAba_ViraUnsupportedExternal()
    {
        var externalUrl = CreateExternalApplyFixture("<form><input name=\"email\"></form>");
        var linkedinFixture = CreateExternalApplyFixture($"""
            <section class="jobs-details__main-content">
              <button onclick="window.open('{externalUrl}', '_blank')">Candidatar-se</button>
            </section>
        """);
        await _page!.GotoAsync(linkedinFixture);
        var surface = new LinkedInEasyApplySurface();

        var result = await surface.OpenAsync(_page);

        Assert.True(result.Opened);
        Assert.Equal("unsupported_external", result.SurfaceKind);
        Assert.Equal("Unsupported", result.SuggestedPlatform);
        Assert.NotNull(result.ActivePage);
        Assert.StartsWith("file://", result.FinalUrl);
        Assert.NotEqual("manual_only", result.FailureState);
    }

    [Fact]
    public async Task LinkedInSurface_CandidatarSeMesmaAba_ViraUnsupportedExternal()
    {
        var externalUrl = CreateExternalApplyFixture("<form><input name=\"fullName\"></form>");
        var linkedinFixture = CreateExternalApplyFixture($"""
            <section class="jobs-details__main-content">
              <button onclick="location.href='{externalUrl}'">Candidatar-se</button>
            </section>
        """);
        await _page!.GotoAsync(linkedinFixture);
        var surface = new LinkedInEasyApplySurface();

        var result = await surface.OpenAsync(_page);

        Assert.True(result.Opened);
        Assert.Equal("unsupported_external", result.SurfaceKind);
        Assert.Equal("Unsupported", result.SuggestedPlatform);
        Assert.Same(_page, result.ActivePage);
        Assert.StartsWith("file://", result.FinalUrl);
        Assert.NotEqual("manual_only", result.FailureState);
    }

    [Fact]
    public async Task LinkedInSurface_CandidatarSeInhire_ViraInhire()
    {
        const string externalUrl = "https://empresa.inhire.app/jobs/123";
        await _page!.RouteAsync("https://empresa.inhire.app/jobs/123", route =>
            route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "text/html",
                Body = "<!doctype html><html><body><form><input name=\"email\"></form></body></html>"
            }));
        await _page.SetContentAsync($"""
            <section class="jobs-details__main-content">
              <button onclick="window.open('{externalUrl}', '_blank')">Candidatar-se</button>
            </section>
        """);
        var surface = new LinkedInEasyApplySurface();

        var result = await surface.OpenAsync(_page);

        Assert.True(result.Opened);
        Assert.Equal("inhire", result.SurfaceKind);
        Assert.Equal("InHire", result.SuggestedPlatform);
        Assert.NotNull(result.ActivePage);
        Assert.Equal(externalUrl, result.FinalUrl);
    }

    private static string CreateExternalApplyFixture(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"jobpilot-external-{Guid.NewGuid():N}.html");
        File.WriteAllText(path, $"<!doctype html><html><body>{body}</body></html>");
        return new Uri(path).AbsoluteUri;
    }

    private static void ReplaceHttpClient(JobPilotApiClient client, HttpMessageHandler handler)
    {
        var field = typeof(JobPilotApiClient).GetField("_http", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(client, new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") });
    }
}

/// <summary>
/// HttpMessageHandler mock que retorna respostas vazias para endpoints do agente.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
