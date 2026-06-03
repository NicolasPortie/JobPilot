using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public static class PageObserver
{
    private static readonly IPageObservationExtension[] Extensions = PageObservationExtensions.Default;

    public static async Task<PageObservation> ObserveAsync(IPage page)
    {
        var (title, extension) = await ResolveExtensionAsync(page);
        var activeSurface = extension.ActiveSurface;

        var json = await page.EvaluateAsync<JsonElement>("""
            (activeSurface) => {
              let counter = Number(document.documentElement.getAttribute('data-jobpilot-counter') || '0');
              const clean = value => (value || '').replace(/\s+/g, ' ').trim();
              const visible = el => {
                const style = window.getComputedStyle(el);
                const rect = el.getBoundingClientRect();
                return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
              };
              const queryAll = (root, selectors) => selectors && selectors.length
                ? Array.from(root.querySelectorAll(selectors.join(', ')))
                : [];
              const firstVisible = (root, selectors) => queryAll(root, selectors).find(visible);
              const activeModalOutlet = () => activeSurface.modalOutletSelector
                ? document.querySelector(activeSurface.modalOutletSelector)
                : null;
              const meaningfulDialog = el => {
                if (!visible(el)) return false;
                const rect = el.getBoundingClientRect();
                const text = clean(el.innerText || el.textContent || el.getAttribute('aria-label') || el.getAttribute('title') || '').toLowerCase();
                const identity = clean(`${el.id || ''} ${el.className || ''} ${el.getAttribute('aria-label') || ''}`).toLowerCase();
                if (/accessib|acessib|vlibras|libras|assistive|widget/.test(`${identity} ${text}`)) return false;
                const hasForm = !!el.querySelector('form, input:not([type="hidden"]), textarea, select, [role="combobox"], [aria-haspopup="listbox"]');
                const substantial = rect.width >= 320 && rect.height >= 180;
                return hasForm || substantial;
              };
              const visibleDialog = () => queryAll(document, activeSurface.dialogSelectors).find(meaningfulDialog);
              const inActiveModal = el => {
                const modalOutlet = activeModalOutlet();
                if (modalOutlet && firstVisible(modalOutlet, activeSurface.modalSelectors)) {
                  return modalOutlet.contains(el);
                }
                const dialog = visibleDialog();
                return dialog ? dialog.contains(el) : true;
              };
              const closestFrom = (el, selectors) => {
                for (const selector of selectors || []) {
                  const match = el.closest(selector);
                  if (match) return match;
                }
                return null;
              };
              const labelFor = el => {
                const parts = [];
                if (el.getAttribute('aria-label')) parts.push(el.getAttribute('aria-label'));
                const labelledBy = el.getAttribute('aria-labelledby');
                if (labelledBy) {
                  for (const id of labelledBy.split(/\s+/).filter(Boolean)) {
                    const labelNode = document.getElementById(id);
                    if (labelNode) parts.push(labelNode.innerText || labelNode.textContent || '');
                  }
                }
                if (el.getAttribute('placeholder')) parts.push(el.getAttribute('placeholder'));
                if (el.id) {
                  const explicit = document.querySelector(`label[for="${CSS.escape(el.id)}"]`);
                  if (explicit) parts.push(explicit.innerText || explicit.textContent || '');
                }
                const label = el.closest('label');
                if (label) parts.push(label.innerText || label.textContent || '');
                const group = closestFrom(el, activeSurface.fieldContextSelectors);
                if (group && group.tagName.toLowerCase() !== 'form') {
                  const heading = group.querySelector('legend, h1, h2, h3, h4, label, span[aria-hidden="true"]');
                  if (heading) parts.push(heading.innerText || heading.textContent || '');
                }
                const previous = el.previousElementSibling;
                if (previous && /^(label|span|div|p)$/i.test(previous.tagName)) {
                  const previousText = previous.innerText || previous.textContent || '';
                  if (previousText && previousText.length < 180) parts.push(previousText);
                }
                const parentText = el.parentElement ? el.parentElement.innerText || el.parentElement.textContent || '' : '';
                if (parentText && parentText.length < 220) parts.push(parentText);
                return clean([...new Set(parts.map(clean).filter(Boolean))].join(' ')).slice(0, 260);
              };
              const fieldGroupId = el => {
                const group = el.closest('fieldset, [role="group"], .form-group, .field-group');
                return group ? (group.id || group.getAttribute('data-testid') || '') : '';
              };
              const describedBy = el => {
                const id = el.getAttribute('aria-describedby');
                if (!id) return '';
                const desc = document.getElementById(id);
                return desc ? clean(desc.innerText || desc.textContent || '') : '';
              };
              const detectWizardStep = () => {
                const text = document.body ? document.body.innerText || '' : '';
                const m = text.match(/(?:step|etapa|p[Ã¡a]gina|page)\s*(\d+)\s*(?:of|de|\/)\s*(\d+)/i);
                if (m) return parseInt(m[1], 10);
                const progress = document.querySelector('[role="progressbar"], .progress-bar, .step.active, .step-current');
                if (progress) {
                  const steps = document.querySelectorAll('.step, [role="tab"], .progress-step');
                  for (let i = 0; i < steps.length; i++) {
                    const cls = steps[i].className || '';
                    if (cls.includes('active') || cls.includes('current') || steps[i].getAttribute('aria-selected') === 'true') return i + 1;
                  }
                }
                return null;
              };
              const readOptions = el => {
                if (el.tagName.toLowerCase() === 'select') {
                  return Array.from(el.options).map(o => clean(o.text || o.value)).filter(Boolean).slice(0, 40);
                }

                const role = (el.getAttribute('role') || '').toLowerCase();
                const selectLikeText = clean(el.innerText || el.textContent || el.getAttribute('aria-label') || el.getAttribute('title') || '').toLowerCase();
                const selectLike = role === 'combobox' ||
                  el.getAttribute('aria-haspopup') === 'listbox' ||
                  /selecionar op[cÃ§][aÃ£]o|select an option|choose/i.test(selectLikeText);
                if (!selectLike) return [];

                const ownedIds = [
                  el.getAttribute('aria-controls'),
                  el.getAttribute('aria-owns')
                ].filter(Boolean);
                const optionNodes = [];
                for (const id of ownedIds) {
                  const list = document.getElementById(id);
                  if (list) optionNodes.push(...Array.from(list.querySelectorAll('[role="option"], li, button')));
                }
                if (optionNodes.length === 0) {
                  const group = closestFrom(el, activeSurface.fieldContextSelectors);
                  if (group) optionNodes.push(...Array.from(group.querySelectorAll('[role="option"], li, button')));
                }
                return optionNodes.map(o => clean(o.innerText || o.textContent || o.getAttribute('aria-label') || '')).filter(Boolean).slice(0, 40);
              };
              const actionableControl = el => {
                const tag = el.tagName.toLowerCase();
                const type = (el.getAttribute('type') || '').toLowerCase();
                if (tag === 'input' && !['checkbox', 'radio', 'file'].includes(type)) {
                  const rect = el.getBoundingClientRect();
                  if (rect.width < 24 || rect.height < 16) return false;
                }
                return true;
              };
              const observableControl = el => {
                const tag = el.tagName.toLowerCase();
                const type = (el.getAttribute('type') || '').toLowerCase();
                return visible(el) || (tag === 'input' && type === 'file');
              };

              const modalOutlet = activeModalOutlet();
              const hasOpenModal = modalOutlet && firstVisible(modalOutlet, activeSurface.modalSelectors);
              let scope = document;
              let visibleTextSource = document.body;
              if (hasOpenModal) {
                scope = modalOutlet;
                visibleTextSource = modalOutlet;
              } else {
                const dialog = visibleDialog();
                if (dialog) {
                  scope = dialog;
                  visibleTextSource = dialog;
                }
              }

              const scrollCandidates = hasOpenModal
                ? [
                    ...activeSurface.modalScrollSelectors.map(selector => modalOutlet.querySelector(selector)),
                    modalOutlet
                  ].filter(Boolean)
                : [document.scrollingElement, document.documentElement, document.body].filter(Boolean);
              const scrollTarget = scrollCandidates.find(el => el.scrollHeight > el.clientHeight + 8) || scrollCandidates[0];
              const canScrollDown = !!scrollTarget && (scrollTarget.scrollTop + scrollTarget.clientHeight < scrollTarget.scrollHeight - 12);

              const nodes = Array.from(scope.querySelectorAll([
                'input:not([type="hidden"])',
                'textarea',
                'select',
                '[role="combobox"]',
                '[aria-haspopup="listbox"]',
                '[aria-expanded]',
                '.artdeco-dropdown__trigger',
                '[tabindex]:not([tabindex="-1"])',
                'button',
                'a[href]',
                '[role="button"]',
                '[role="checkbox"]',
                '[role="radio"]'
              ].join(','))).filter(observableControl).filter(actionableControl).filter(inActiveModal).slice(0, 120);
              const wizardStep = detectWizardStep();
              const elements = nodes.map(el => {
                const id = el.getAttribute('data-jobpilot-id') || `jp-${++counter}`;
                el.setAttribute('data-jobpilot-id', id);
                document.documentElement.setAttribute('data-jobpilot-counter', String(counter));
                const tag = el.tagName.toLowerCase();
                const type = (el.getAttribute('type') || '').toLowerCase();
                const role = (el.getAttribute('role') || '').toLowerCase();
                const text = clean(el.innerText || el.textContent || el.value || '');
                const label = labelFor(el) || text || clean(el.getAttribute('title') || el.getAttribute('name') || id);
                const rect = el.getBoundingClientRect();
                const plc = clean(el.getAttribute('placeholder') || '');
                const pat = clean(el.getAttribute('pattern') || '');
                const im = clean(el.getAttribute('inputmode') || '');
                const ac = clean(el.getAttribute('autocomplete') || '');
                const dm = clean(el.getAttribute('data-mask') || el.getAttribute('data-inputmask') || '');
                const tit = clean(el.getAttribute('title') || '');
                const nm = clean(el.getAttribute('name') || '');
                const grp = fieldGroupId(el);
                const db = describedBy(el);
                return {
                  elementId: id,
                  tag,
                  type,
                  role,
                  label,
                  text: text.slice(0, 260),
                  value: clean(el.value || ''),
                  required: el.required === true ||
                    el.getAttribute('aria-required') === 'true' ||
                    /\*\s*$/.test(label) ||
                    /\*\s*(selecionar|select|choose|escolha|$)/i.test(label) ||
                    /\*\s*$/.test(clean(el.parentElement?.innerText || '')) ||
                    /\*\s*(selecionar|select|choose|escolha|$)/i.test(clean(el.parentElement?.innerText || '')),
                  disabled: el.disabled === true || el.getAttribute('aria-disabled') === 'true',
                  checked: el.checked === true || el.getAttribute('aria-checked') === 'true',
                  href: el.href || '',
                  options: readOptions(el),
                  bounds: { x: Math.round(rect.x), y: Math.round(rect.y), width: Math.round(rect.width), height: Math.round(rect.height) },
                  placeholder: plc,
                  pattern: pat,
                  inputMode: im,
                  autoComplete: ac,
                  dataMask: dm,
                  title: tit,
                  name: nm,
                  fieldGroupId: grp,
                  wizardStep: wizardStep,
                  describedBy: db
                };
              });
              return {
                url: document.location.href,
                title: document.title || '',
                visibleText: clean(visibleTextSource?.innerText || document.body?.innerText || '').slice(0, 1800),
                hasModal: !!hasOpenModal || scope !== document,
                canScrollDown,
                elements
              };
            }
        """, new
        {
            modalOutletSelector = activeSurface.ModalOutletSelector,
            modalSelectors = activeSurface.ModalSelectors,
            dialogSelectors = activeSurface.DialogSelectors,
            modalScrollSelectors = activeSurface.ModalScrollSelectors,
            fieldContextSelectors = activeSurface.FieldContextSelectors
        });

        var observation = json.Deserialize<PageObservation>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new PageObservation(page.Url, title, await GetVisibleTextAsync(page), []);

        return extension.Normalize(observation);
    }

    public static async Task ScrollActiveSurfaceAsync(IPage page)
    {
        var (_, extension) = await ResolveExtensionAsync(page);
        var activeSurface = extension.ActiveSurface;

        await page.EvaluateAsync("""
            (activeSurface) => {
              const visible = el => {
                const style = window.getComputedStyle(el);
                const rect = el.getBoundingClientRect();
                return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
              };
              const queryAll = (root, selectors) => selectors && selectors.length
                ? Array.from(root.querySelectorAll(selectors.join(', ')))
                : [];
              const firstVisible = (root, selectors) => queryAll(root, selectors).find(visible);
              const modalOutlet = activeSurface.modalOutletSelector
                ? document.querySelector(activeSurface.modalOutletSelector)
                : null;
              const hasOpenModal = modalOutlet && firstVisible(modalOutlet, activeSurface.modalSelectors);
              const meaningfulDialog = el => {
                if (!visible(el)) return false;
                const rect = el.getBoundingClientRect();
                const text = (el.innerText || el.textContent || el.getAttribute('aria-label') || el.getAttribute('title') || '').replace(/\s+/g, ' ').trim().toLowerCase();
                const identity = `${el.id || ''} ${el.className || ''} ${el.getAttribute('aria-label') || ''}`.toLowerCase();
                if (/accessib|acessib|vlibras|libras|assistive|widget/.test(`${identity} ${text}`)) return false;
                const hasForm = !!el.querySelector('form, input:not([type="hidden"]), textarea, select, [role="combobox"], [aria-haspopup="listbox"]');
                const substantial = rect.width >= 320 && rect.height >= 180;
                return hasForm || substantial;
              };
              const dialog = queryAll(document, activeSurface.dialogSelectors).find(meaningfulDialog);
              const modalScrollTargets = hasOpenModal
                ? [
                    ...activeSurface.modalScrollSelectors.map(selector => modalOutlet.querySelector(selector)),
                    modalOutlet
                  ].filter(Boolean)
                : [];
              const candidates = [
                ...modalScrollTargets,
                dialog,
                document.scrollingElement,
                document.documentElement,
                document.body
              ].filter(Boolean);
              const scroller = candidates.find(el => el.scrollHeight > el.clientHeight + 8) || document.scrollingElement;
              scroller?.scrollBy({ top: Math.max(420, Math.floor(scroller.clientHeight * 0.72)), left: 0, behavior: 'instant' });
            }
            """, new
        {
            modalOutletSelector = activeSurface.ModalOutletSelector,
            modalSelectors = activeSurface.ModalSelectors,
            dialogSelectors = activeSurface.DialogSelectors,
            modalScrollSelectors = activeSurface.ModalScrollSelectors,
            fieldContextSelectors = activeSurface.FieldContextSelectors
        });

        await page.WaitForTimeoutAsync(700);
    }

    private static async Task<(string Title, IPageObservationExtension Extension)> ResolveExtensionAsync(IPage page)
    {
        var title = await page.TitleAsync();
        var extension = Extensions.FirstOrDefault(e => e.CanApply(page.Url, title)) ?? NoOpPageObservationExtension.Instance;
        return (title, extension);
    }

    public static async Task<string> GetVisibleTextAsync(IPage page)
    {
        try
        {
            var text = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 3000 });
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length > 700 ? text[..700] : text;
        }
        catch
        {
            return "";
        }
    }

    public static async Task<string> CaptureEvidenceAsync(IPage page, string reasonCode)
    {
        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "evidence");
        Directory.CreateDirectory(evidenceDir);
        var safeReason = string.Concat(reasonCode.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_'));
        var screenshotPath = Path.Combine(evidenceDir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeReason}.png");

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
        }
        catch
        {
            screenshotPath = "screenshot_unavailable";
        }

        var text = await GetVisibleTextAsync(page);
        return $"url={page.Url}; screenshot={screenshotPath}; visibleText={text}";
    }
}
