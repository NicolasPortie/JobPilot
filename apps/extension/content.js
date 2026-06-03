(() => {
if (globalThis.__jobpilotContentLoaded) return;
globalThis.__jobpilotContentLoaded = true;

const ELEMENT_ATTR = "data-jobpilot-extension-id";
let nextId = 1;

function textOf(element) {
  const id = element.getAttribute("id");
  const labelledBy = element.getAttribute("aria-labelledby");
  const explicitLabel = id ? document.querySelector(`label[for="${CSS.escape(id)}"]`)?.innerText : "";
  const labelledByText = labelledBy
    ? labelledBy.split(/\s+/).map((part) => document.getElementById(part)?.innerText || "").join(" ")
    : "";
  const wrappingLabel = element.closest("label")?.innerText || "";

  return [
    explicitLabel,
    labelledByText,
    wrappingLabel,
    element.getAttribute("aria-label"),
    element.getAttribute("placeholder"),
    element.innerText,
    element.textContent,
    element.value
  ]
    .filter(Boolean)
    .join(" ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 240);
}

function setNativeValue(element, value) {
  const prototype = element instanceof HTMLTextAreaElement
    ? HTMLTextAreaElement.prototype
    : element instanceof HTMLSelectElement
      ? HTMLSelectElement.prototype
      : HTMLInputElement.prototype;
  const descriptor = Object.getOwnPropertyDescriptor(prototype, "value");
  if (descriptor?.set) {
    descriptor.set.call(element, value);
  } else {
    element.value = value;
  }
}

function emitInputEvents(element) {
  element.dispatchEvent(new Event("input", { bubbles: true }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
  element.dispatchEvent(new Event("blur", { bubbles: true }));
}

function selectOption(element, requestedValue) {
  const wanted = (requestedValue || "").trim();
  const wantedLower = wanted.toLowerCase();
  const options = Array.from(element.options || []);
  const option = options.find((item) => item.value === wanted)
    || options.find((item) => (item.textContent || "").trim().toLowerCase() === wantedLower)
    || options.find((item) => (item.textContent || "").trim().toLowerCase().includes(wantedLower));

  if (!option) return false;
  setNativeValue(element, option.value);
  return true;
}

function activateElement(element) {
  element.scrollIntoView({ block: "center", inline: "center" });
  element.focus?.();
  const rect = element.getBoundingClientRect();
  const eventInit = {
    bubbles: true,
    cancelable: true,
    composed: true,
    view: window,
    clientX: Math.round(rect.left + rect.width / 2),
    clientY: Math.round(rect.top + rect.height / 2)
  };
  for (const type of ["pointerover", "pointerenter", "mouseover", "mouseenter", "pointerdown", "mousedown", "pointerup", "mouseup", "click"]) {
    const event = type.startsWith("pointer")
      ? new PointerEvent(type, { ...eventInit, pointerId: 1, pointerType: "mouse", isPrimary: true })
      : new MouseEvent(type, eventInit);
    element.dispatchEvent(event);
  }
}

function isVisible(element) {
  const style = window.getComputedStyle(element);
  const rect = element.getBoundingClientRect();
  return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
}

function ensureId(element) {
  const existing = element.getAttribute(ELEMENT_ATTR);
  if (existing) return existing;
  const id = `ext-${nextId++}`;
  element.setAttribute(ELEMENT_ATTR, id);
  return id;
}

function activeModalRoot() {
  const candidates = Array.from(document.querySelectorAll("[role='dialog'], #artdeco-modal-outlet"))
    .filter(isVisible)
    .sort((a, b) => {
      const aRect = a.getBoundingClientRect();
      const bRect = b.getBoundingClientRect();
      return (bRect.width * bRect.height) - (aRect.width * aRect.height);
    });

  return candidates[0] || null;
}

function activeObservationRoot() {
  return activeModalRoot() || document;
}

function collectElements() {
  const selector = [
    "input",
    "textarea",
    "select",
    "button",
    "a[href]",
    "[role='button']",
    "[role='checkbox']",
    "[role='radio']",
    "[role='combobox']",
    "[contenteditable='true']"
  ].join(",");

  const root = activeObservationRoot();
  return Array.from(root.querySelectorAll(selector))
    .filter(isVisible)
    .slice(0, 250)
    .map((element) => {
      const rect = element.getBoundingClientRect();
      const tag = element.tagName.toLowerCase();
      const type = element.getAttribute("type") || element.getAttribute("role") || tag;
      const role = element.getAttribute("role") || "";
      const label = textOf(element);
      const describedBy = element.getAttribute("aria-describedby") || "";
      const describedByText = describedBy
        ? describedBy.split(/\s+/).map((part) => document.getElementById(part)?.innerText || "").join(" ")
        : "";
      return {
        elementId: ensureId(element),
        tag,
        type,
        role,
        label,
        text: label,
        value: element.value || element.getAttribute("aria-valuetext") || "",
        href: element.href || "",
        required: element.required || element.getAttribute("aria-required") === "true",
        disabled: element.disabled || element.getAttribute("aria-disabled") === "true",
        checked: Boolean(element.checked || element.getAttribute("aria-checked") === "true"),
        options: tag === "select" ? Array.from(element.options).map((option) => option.textContent.trim()).filter(Boolean) : [],
        placeholder: element.getAttribute("placeholder") || "",
        pattern: element.getAttribute("pattern") || "",
        inputMode: element.getAttribute("inputmode") || "",
        autoComplete: element.getAttribute("autocomplete") || "",
        dataMask: element.getAttribute("data-mask") || element.getAttribute("data-testid") || "",
        title: element.getAttribute("title") || "",
        name: element.getAttribute("name") || "",
        describedBy: describedByText.replace(/\s+/g, " ").trim().slice(0, 240),
        bounds: {
          x: Math.round(rect.x),
          y: Math.round(rect.y),
          width: Math.round(rect.width),
          height: Math.round(rect.height)
        }
      };
    });
}

function observe() {
  const modal = activeModalRoot();
  const root = modal || document.body;
  return {
    url: location.href,
    title: document.title,
    visibleText: (root.innerText || "").replace(/\s+/g, " ").trim().slice(0, 8000),
    elements: collectElements(),
    hasModal: Boolean(modal),
    canScrollDown: modal
      ? modal.scrollTop + modal.clientHeight < modal.scrollHeight - 8
      : window.scrollY + window.innerHeight < document.documentElement.scrollHeight - 8,
    observedAt: new Date().toISOString()
  };
}

function extractLinkedInJobs() {
  const normalizeUrl = (href) => href ? new URL(href, document.location.href).toString().split("?")[0] : "";
  const cleanText = (text) => [...new Set((text || "").split("\n").map((x) => x.trim()).filter(Boolean))].join(" ").trim();
  const companySelectors = [
    ".job-card-container__primary-description",
    ".job-card-container__company-name",
    ".base-search-card__subtitle",
    ".artdeco-entity-lockup__subtitle",
    ".artdeco-entity-lockup__subtitle span",
    "[data-test-job-card-company-name]"
  ].join(",");
  const cleanCompany = (text, title = "") => {
    const titleNorm = normalizeText(title);
    const blocked = /promoted|promovida|viewed|visualizada|applicant|candidatura|candidate|verification|vaga|remote|remoto|tempo integral|full-time/i;
    const lines = (text || "").split("\n").map((x) => x.trim()).filter(Boolean);
    const candidate = lines.find((line) => {
      const normalized = normalizeText(line);
      return normalized &&
        normalized !== titleNorm &&
        !normalized.includes(titleNorm) &&
        !titleNorm.includes(normalized) &&
        !blocked.test(line);
    });
    return cleanText(candidate || text).replace(/\s+with verification$/i, "").trim();
  };
  const cleanTitle = (text) => {
    let value = cleanText(text).replace(/\s+with verification$/i, "").trim();
    const half = Math.floor(value.length / 2);
    if (value.length % 2 === 0 && value.slice(0, half) === value.slice(half)) return value.slice(0, half).trim();
    const words = value.split(/\s+/);
    for (let size = Math.floor(words.length / 2); size >= 2; size--) {
      const first = words.slice(0, size).join(" ");
      const second = words.slice(size, size * 2).join(" ");
      if (first.toLowerCase() === second.toLowerCase()) return first;
    }
    return value;
  };
  const inferCompanyFromVisibleText = (title, text) => {
    const normalizedTitle = cleanTitle(title);
    if (!normalizedTitle) return "";
    const compact = (text || "").replace(/\s+/g, " ");
    const escapedTitle = normalizedTitle.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const match = compact.match(new RegExp(`${escapedTitle}\\s+${escapedTitle}\\s+(?:with verification\\s+)?`, "i"));
    if (!match?.index && match?.index !== 0) return "";
    const tail = compact.slice(match.index + match[0].length).trim();
    const markers = [
      " São Paulo", " Sao Paulo", " Rio de Janeiro", " Belo Horizonte", " Brasil", " Brazil",
      " Curitiba", " Campinas", " Florianópolis", " Florianopolis", " Porto Alegre", " Remoto", " (Remoto)"
    ];
    const markerIndexes = markers
      .map((marker) => tail.search(new RegExp(marker.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "i")))
      .filter((index) => index > 0);
    const regionMatch = tail.match(/\s+[A-ZÁ-Ú][\wÀ-ÿ.'+-]+(?:\s+e\s+Regi(?:ão|ao))/i);
    if (regionMatch?.index && regionMatch.index > 0) markerIndexes.push(regionMatch.index);
    if (markerIndexes.length === 0) return "";
    return cleanCompany(tail.slice(0, Math.min(...markerIndexes)), normalizedTitle);
  };
  const withCompanyFallback = (job) => ({
    ...job,
    company: job.company || inferCompanyFromVisibleText(job.title, pageText)
  });
  const isValidJob = (job) => {
    const title = normalizeText(job.title);
    return Boolean(job.title && job.url) &&
      !title.includes("volte a experimentar o premium") &&
      !title.includes("experimente o premium") &&
      !title.includes("try premium") &&
      !title.includes("premium gratis") &&
      !title.includes("premium free");
  };
  const params = new URLSearchParams(document.location.search);
  const pageText = document.body?.innerText || "";
  const remoteSearch = (params.get("f_WT") || "").split(",").includes("2") || /ultima semana\s+remoto|última semana\s+remoto|remote/i.test(pageText);
  const readCard = (card) => {
    const link = card.querySelector('a[href*="/jobs/view/"]');
    const title = cleanTitle(card.querySelector('.job-card-list__title, .base-search-card__title, .job-card-container__link, a[href*="/jobs/view/"]')?.textContent);
    const company = cleanCompany(card.querySelector(companySelectors)?.textContent, title);
    const jobLocation = cleanText(card.querySelector(".job-card-container__metadata-item, .job-search-card__location, [data-test-job-card-location]")?.textContent);
    const url = normalizeUrl(link?.getAttribute("href"));
    return { title, company, location: jobLocation || (remoteSearch ? "Remote search filter" : ""), description: "", url, isRemote: remoteSearch || /remote|remoto|home office/i.test(`${title} ${jobLocation}`) };
  };
  const fromCards = Array.from(document.querySelectorAll("[data-job-id], .job-card-container, .jobs-search-results__list-item")).map(readCard);
  const fromLinks = Array.from(document.querySelectorAll('a[href*="/jobs/view/"]')).map((link) => {
    const card = link.closest("[data-job-id], .job-card-container, .jobs-search-results__list-item, li, article") || link;
    const title = cleanTitle(link.textContent || card.textContent || "");
    const text = (card.textContent || "").trim();
    const lines = text.split("\n").map((x) => x.trim()).filter(Boolean);
    const company = cleanCompany(card.querySelector?.(companySelectors)?.textContent || lines.join("\n"), title);
    const location = lines.find((x) => /remote|remoto|brazil|brasil|sao paulo|rio de janeiro|curitiba|campinas|florianopolis|porto alegre/i.test(x)) || "";
    return { title, company, location: location || (remoteSearch ? "Remote search filter" : ""), description: "", url: normalizeUrl(link.getAttribute("href")), isRemote: remoteSearch || /remote|remoto|home office/i.test(`${title} ${location}`) };
  });

  return {
    url: location.href,
    jobs: [...fromCards, ...fromLinks].map(withCompanyFallback).filter(isValidJob)
  };
}

function normalizeText(value) {
  return (value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();
}

function wait(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function currentLinkedInJobId() {
  const params = new URLSearchParams(location.search);
  const fromQuery = params.get("currentJobId") || params.get("referenceJobId");
  if (fromQuery) return fromQuery;
  const match = location.pathname.match(/\/jobs\/view\/(\d+)/);
  return match ? match[1] : "";
}

function linkedinDetailRoot() {
  return document.querySelector(".jobs-search__job-details--container, .jobs-details, .jobs-unified-top-card, main") || document.body;
}

function findLinkedInApplyButton(root) {
  const candidates = Array.from(root.querySelectorAll("button, a[href], [role='button']"))
    .filter(isVisible)
    .map((element) => ({ element, text: normalizeText(textOf(element)) }))
    .filter((candidate) => candidate.text);

  const alreadyApplied = candidates.find(({ text }) =>
    /\b(applied|candidatura enviada|voce se candidatou|ja se candidatou)\b/i.test(text));
  if (alreadyApplied) return { kind: "already_applied", element: alreadyApplied.element, text: alreadyApplied.text };

  const easyApply = candidates.find(({ text }) =>
    text.includes("easy apply") ||
    text.includes("candidatura simplificada") ||
    text.includes("candidate-se simplificadamente"));
  if (easyApply) return { kind: "linkedin_easy_apply", element: easyApply.element, text: easyApply.text };

  const external = candidates.find(({ text }) =>
    /^candidatar-se\b/.test(text) ||
    /^apply\b/.test(text) ||
    text.includes("apply on company website") ||
    text.includes("candidate-se no site da empresa"));
  if (external) return { kind: "external", element: external.element, text: external.text };

  return null;
}

function classifyExternalUrl(url) {
  if (/inhire\.app/i.test(url)) return "inhire";
  return "unsupported_external";
}

async function waitForEasyApplyModal() {
  for (let attempt = 0; attempt < 14; attempt++) {
    const modal = document.querySelector("[role='dialog'], #artdeco-modal-outlet");
    if (modal && isVisible(modal)) return true;
    await wait(500);
  }
  return false;
}

async function openLinkedInApplySurface(command) {
  const expectedJobId = command.elementId || "";
  const expectedTitle = normalizeText(command.value || "");
  const root = linkedinDetailRoot();
  const rootText = normalizeText(root.innerText || "");
  const activeJobId = currentLinkedInJobId();

  if (expectedJobId && activeJobId && activeJobId !== expectedJobId) {
    return {
      ok: false,
      opened: false,
      surfaceKind: "",
      finalUrl: location.href,
      failureCode: "job_mismatch",
      failureState: "needs_review",
      fieldLabel: "Vaga",
      detail: `LinkedIn opened job ${activeJobId}, but JobPilot expected ${expectedJobId}.`
    };
  }

  if (expectedTitle && rootText && !rootText.includes(expectedTitle.slice(0, Math.min(expectedTitle.length, 80)))) {
    return {
      ok: false,
      opened: false,
      surfaceKind: "",
      finalUrl: location.href,
      failureCode: "job_title_not_visible",
      failureState: "needs_review",
      fieldLabel: "Vaga",
      detail: "The expected job title is not visible in the LinkedIn details panel."
    };
  }

  const applyButton = findLinkedInApplyButton(root);
  if (!applyButton) {
    return {
      ok: false,
      opened: false,
      surfaceKind: "",
      finalUrl: location.href,
      failureCode: "application_surface_not_found",
      failureState: "needs_review",
      fieldLabel: "Candidatar-se",
      detail: "No Easy Apply or external apply button was found in the LinkedIn job detail panel."
    };
  }

  if (applyButton.kind === "already_applied") {
    return {
      ok: true,
      opened: false,
      surfaceKind: "already_applied",
      finalUrl: location.href,
      failureCode: "already_applied",
      failureState: "applied",
      fieldLabel: "Candidatura",
      detail: "LinkedIn indicates this job was already applied."
    };
  }

  if (applyButton.kind === "linkedin_easy_apply") {
    activateElement(applyButton.element);
    const modalOpened = await waitForEasyApplyModal();
    if (!modalOpened) {
      return {
        ok: false,
        opened: false,
        surfaceKind: "linkedin_easy_apply",
        finalUrl: location.href,
        failureCode: "easy_apply_modal_not_opened",
        failureState: "needs_review",
        fieldLabel: "Candidatura simplificada",
        detail: "Easy Apply was present, but the modal did not open."
      };
    }

    return {
      ok: true,
      opened: true,
      surfaceKind: "linkedin_easy_apply",
      finalUrl: location.href,
      detail: "LinkedIn Easy Apply modal opened in the user's real browser."
    };
  }

  const href = applyButton.element.href || applyButton.element.getAttribute("href") || "";
  applyButton.element.click();
  await wait(2500);
  const finalUrl = location.href !== "about:blank" ? location.href : href;
  const surfaceKind = classifyExternalUrl(finalUrl || href);
  return {
    ok: true,
    opened: true,
    surfaceKind,
    finalUrl: finalUrl || href,
    detail: surfaceKind === "inhire"
      ? "LinkedIn external apply opened an InHire surface."
      : "LinkedIn job uses an external application site that JobPilot does not automate yet."
  };
}

function findElement(elementId) {
  return document.querySelector(`[${ELEMENT_ATTR}="${CSS.escape(elementId)}"]`);
}

function elementAllowedInActiveSurface(element) {
  const modal = activeModalRoot();
  return !modal || modal.contains(element);
}

async function dispatch(command) {
  if (command.type === "observe") return observe();
  if (command.type === "extract_jobs") return extractLinkedInJobs();
  if (command.type === "open_apply_surface") return openLinkedInApplySurface(command);
  if (command.type === "scroll") {
    window.scrollBy({ top: Math.round(window.innerHeight * 0.7), behavior: "smooth" });
    return { ok: true };
  }

  const element = findElement(command.elementId);
  if (!element) return { ok: false, error: "element_not_found" };
  if (!elementAllowedInActiveSurface(element)) return { ok: false, error: "element_outside_active_modal" };

  if (command.type === "click") {
    activateElement(element);
    return { ok: true };
  }

  if (command.type === "fill") {
    element.focus();
    if (element.isContentEditable) {
      element.textContent = command.value || "";
    } else {
      setNativeValue(element, command.value || "");
    }
    emitInputEvents(element);
    return { ok: true };
  }

  if (command.type === "select") {
    if (element.tagName.toLowerCase() !== "select") {
      return { ok: false, error: "element_is_not_select" };
    }
    if (!selectOption(element, command.value || "")) {
      return { ok: false, error: "option_not_found" };
    }
    emitInputEvents(element);
    return { ok: true };
  }

  if (command.type === "check") {
    if (element.checked || element.getAttribute("aria-checked") === "true") return { ok: true };
    element.click();
    emitInputEvents(element);
    return { ok: true };
  }

  if (command.type === "upload_file") {
    return { ok: false, error: "file_upload_requires_user_selection" };
  }

  return { ok: false, error: "unsupported_command" };
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type !== "JOBPILOT_COMMAND") return false;
  dispatch(message.command)
    .then((result) => sendResponse({ ok: true, result }))
    .catch((error) => sendResponse({ ok: false, error: String(error?.message || error) }));
  return true;
});
})();
