const SUPPORTED_HOSTS = [
  "linkedin.com",
  "inhire.app",
  "gupy.io",
  "gupy.com.br",
  "indeed.com"
];

const DEFAULT_API_URL = "http://localhost:5000/api";
const LOOP_INTERVAL_MS = 5000;
const ALARM_NAME = "jobpilot-background-sync";

let loopTimer = null;
let syncInFlight = false;
let automationTabId = null;

function normalizeApiUrl(value) {
  return (value || DEFAULT_API_URL).trim().replace(/\/$/, "");
}

function isSupportedUrl(url) {
  return SUPPORTED_HOSTS.some((host) => (url || "").includes(host));
}

async function getSettings() {
  const settings = await chrome.storage.local.get(["jobpilotApiUrl", "jobpilotExtensionToken", "jobpilotInstallationId"]);
  const installationId = settings.jobpilotInstallationId || crypto.randomUUID();
  if (!settings.jobpilotInstallationId) {
    await chrome.storage.local.set({ jobpilotInstallationId: installationId });
  }

  return {
    apiUrl: normalizeApiUrl(settings.jobpilotApiUrl),
    token: settings.jobpilotExtensionToken || "",
    installationId
  };
}

async function getActiveTab() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const url = tab?.url || "";
  return { tab, url, supported: isSupportedUrl(url) };
}

async function pairLocally(apiUrl, installationId) {
  const response = await fetch(`${apiUrl}/extension/local-pair`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      name: "Chrome local",
      browser: "chrome",
      extensionVersion: chrome.runtime.getManifest().version,
      installationId
    })
  });

  if (!response.ok) {
    throw new Error("Abra o JobPilot local antes de conectar.");
  }

  const data = await response.json();
  if (!data.token) {
    throw new Error("Nao foi possivel conectar automaticamente.");
  }

  await chrome.storage.local.set({ jobpilotExtensionToken: data.token });
  return data.token;
}

async function ensurePaired(force = false) {
  const settings = await getSettings();
  if (!force && settings.token) return { apiUrl: settings.apiUrl, token: settings.token };
  const token = await pairLocally(settings.apiUrl, settings.installationId);
  return { apiUrl: settings.apiUrl, token };
}

async function sendHeartbeat(apiUrl, token, url, supported, lastError = "") {
  const response = await fetch(`${apiUrl}/extension/heartbeat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      token,
      currentUrl: url || "",
      isSupportedPage: supported,
      browser: "chrome",
      extensionVersion: chrome.runtime.getManifest().version,
      lastError
    })
  });

  if (!response.ok) {
    throw new Error("Conexao recusada pela API local.");
  }

  return response.json();
}

async function captureScreenshot(tab) {
  return new Promise((resolve) => {
    chrome.tabs.captureVisibleTab(tab?.windowId, { format: "png" }, (dataUrl) => {
      resolve(chrome.runtime.lastError
        ? { ok: false, error: chrome.runtime.lastError.message }
        : { ok: true, result: { dataUrl } });
    });
  });
}

async function commandActiveTab(command) {
  const { tab } = await getCommandTab(command);
  if (!tab?.id) throw new Error("Aba ativa nao encontrada.");

  if (command.type === "screenshot") {
    return captureScreenshot(tab);
  }

  if (command.type === "navigate") {
    return navigateActiveTab(tab.id, command.value || "");
  }

  return sendContentCommand(tab.id, command, tab.url || "");
}

async function getCommandTab(command) {
  if (command.type !== "navigate" && automationTabId) {
    try {
      const tab = await chrome.tabs.get(automationTabId);
      if (tab?.id && isSupportedUrl(tab.url || "")) {
        return { tab, url: tab.url || "", supported: true };
      }
    } catch {
      automationTabId = null;
    }
  }

  const active = await getActiveTab();
  if (command.type === "navigate" || active.supported) return active;

  const supportedTabs = await chrome.tabs.query({});
  const tab = supportedTabs.find((candidate) => isSupportedUrl(candidate.url || ""));
  if (tab?.id) {
    automationTabId = tab.id;
    return { tab, url: tab.url || "", supported: true };
  }

  return active;
}

async function navigateActiveTab(tabId, url) {
  if (!url || !/^https?:\/\//i.test(url)) {
    return { ok: false, error: "invalid_navigation_url" };
  }

  automationTabId = tabId;
  await chrome.tabs.update(tabId, { url });

  const completed = await new Promise((resolve) => {
    const timeout = setTimeout(() => {
      chrome.tabs.onUpdated.removeListener(listener);
      resolve(false);
    }, 30000);

    const listener = (updatedTabId, changeInfo) => {
      if (updatedTabId !== tabId || changeInfo.status !== "complete") return;
      clearTimeout(timeout);
      chrome.tabs.onUpdated.removeListener(listener);
      resolve(true);
    };

    chrome.tabs.onUpdated.addListener(listener);
  });

  const updated = await chrome.tabs.get(tabId);
  if (updated?.id) automationTabId = updated.id;
  return {
    ok: completed,
    url: updated?.url || url,
    title: updated?.title || "",
    error: completed ? "" : "navigation_timeout"
  };
}

async function sendContentCommand(tabId, command, url) {
  const currentTab = await chrome.tabs.get(tabId);
  const currentUrl = currentTab?.url || url || "";
  try {
    if (isSupportedUrl(currentUrl)) {
      await chrome.scripting.executeScript({ target: { tabId }, files: ["content.js"] });
    }
    return await chrome.tabs.sendMessage(tabId, { type: "JOBPILOT_COMMAND", command });
  } catch (error) {
    if (!isSupportedUrl(currentUrl)) throw error;
    await chrome.scripting.executeScript({ target: { tabId }, files: ["content.js"] });
    return chrome.tabs.sendMessage(tabId, { type: "JOBPILOT_COMMAND", command });
  }
}

async function pollNextCommand(apiUrl, token, url, supported) {
  const response = await fetch(`${apiUrl}/extension/commands/next`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      token,
      currentUrl: url || "",
      isSupportedPage: supported,
      browser: "chrome",
      extensionVersion: chrome.runtime.getManifest().version
    })
  });

  if (!response.ok) return null;

  const payload = await response.json();
  const command = payload.command;
  if (!command) return null;

  let success = false;
  let result = {};
  let error = "";
  try {
    const dispatchResponse = await commandActiveTab(command);
    result = dispatchResponse?.result ?? dispatchResponse ?? {};
    success = dispatchResponse?.ok !== false && result?.ok !== false;
    error = result?.error || dispatchResponse?.error || "";
  } catch (dispatchError) {
    error = String(dispatchError?.message || dispatchError);
  }

  await fetch(`${apiUrl}/extension/commands/${command.id}/result`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      token,
      success,
      resultJson: JSON.stringify(result ?? {}),
      error
    })
  });

  return { command, success, result, error };
}

async function syncOnce() {
  if (syncInFlight) return { ok: true, skipped: true };
  syncInFlight = true;

  try {
    const { apiUrl, token } = await getSettings();
    const { url, supported } = await getActiveTab();
    if (!token) {
      return { ok: false, connected: false, url, supported, message: "Clique em conectar ao JobPilot." };
    }

    await sendHeartbeat(apiUrl, token, url, supported, "");
    const commandResult = await pollNextCommand(apiUrl, token, url, supported);
    return { ok: true, connected: true, url, supported, commandResult };
  } catch (error) {
    const message = String(error?.message || error);
    await chrome.storage.local.set({ jobpilotLastError: message });
    return { ok: false, connected: false, message };
  } finally {
    syncInFlight = false;
  }
}

function startLoop() {
  if (loopTimer) return;
  loopTimer = setInterval(syncOnce, LOOP_INTERVAL_MS);
}

async function connect(force = false) {
  const { apiUrl, token } = await ensurePaired(force);
  const { url, supported } = await getActiveTab();
  const status = await sendHeartbeat(apiUrl, token, url, supported, "");
  await chrome.storage.local.set({ jobpilotLastError: "" });
  startLoop();
  return { ok: true, status, url, supported };
}

chrome.runtime.onInstalled.addListener(() => {
  chrome.alarms.create(ALARM_NAME, { periodInMinutes: 0.5 });
});

chrome.runtime.onStartup.addListener(() => {
  startLoop();
});

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === ALARM_NAME) void syncOnce();
});

chrome.tabs.onActivated.addListener(() => {
  void syncOnce();
});

chrome.tabs.onUpdated.addListener((_tabId, changeInfo) => {
  if (changeInfo.status === "complete" || changeInfo.url) void syncOnce();
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "JOBPILOT_CONNECT") {
    connect(Boolean(message.force))
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, message: String(error?.message || error) }));
    return true;
  }

  if (message?.type === "JOBPILOT_REFRESH") {
    syncOnce()
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, message: String(error?.message || error) }));
    return true;
  }

  if (message?.type === "JOBPILOT_OBSERVE_ACTIVE_TAB") {
    commandActiveTab({ type: "observe" })
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, error: String(error?.message || error) }));
    return true;
  }

  if (message?.type === "JOBPILOT_CAPTURE_SCREENSHOT") {
    getActiveTab()
      .then(({ tab }) => captureScreenshot(tab))
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, error: String(error?.message || error) }));
    return true;
  }

  if (message?.type === "JOBPILOT_GET_ACTIVE_TAB") {
    getActiveTab().then(({ tab, url, supported }) => {
      sendResponse({ ok: true, tabId: tab?.id, url, supported });
    });
    return true;
  }

  return false;
});

startLoop();
