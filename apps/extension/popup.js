const statusEl = document.getElementById("status");
const outputEl = document.getElementById("output");
const observeButton = document.getElementById("observe");
const saveButton = document.getElementById("save");
const apiUrlInput = document.getElementById("apiUrl");

function statusText(state) {
  if (state?.ok && state.connected && state.supported) return "Conectada ao JobPilot nesta aba.";
  if (state?.ok && state.connected) return "Conectada. Abra LinkedIn, Gupy ou Indeed.";
  if (state?.supported) return "Pronto nesta aba. Clique em conectar.";
  return state?.message || "Abra LinkedIn, Gupy ou Indeed para usar.";
}

async function activeTabState() {
  const response = await chrome.runtime.sendMessage({ type: "JOBPILOT_GET_ACTIVE_TAB" });
  return response || { ok: false, supported: false };
}

async function loadSettings() {
  const settings = await chrome.storage.local.get(["jobpilotApiUrl"]);
  apiUrlInput.value = settings.jobpilotApiUrl || "http://localhost:5000/api";
}

async function saveSettings() {
  saveButton.disabled = true;
  statusEl.textContent = "Conectando...";

  await chrome.storage.local.set({
    jobpilotApiUrl: apiUrlInput.value.trim() || "http://localhost:5000/api"
  });

  try {
    const state = await chrome.runtime.sendMessage({ type: "JOBPILOT_CONNECT", force: true });
    statusEl.textContent = statusText(state);
    outputEl.textContent = state?.ok
      ? "Pronto. Agora o JobPilot pode trabalhar em segundo plano."
      : state?.message || "Nao foi possivel conectar.";
  } catch (error) {
    statusEl.textContent = `API local indisponivel: ${error?.message || error}`;
  } finally {
    saveButton.disabled = false;
    await refreshStatus();
  }
}

async function refreshStatus() {
  const state = await chrome.runtime.sendMessage({ type: "JOBPILOT_REFRESH" });
  if (state?.skipped) return;

  if (!state?.ok) {
    const tabState = await activeTabState();
    statusEl.textContent = statusText({ ...tabState, message: state?.message });
    observeButton.disabled = !tabState.supported;
    return;
  }

  statusEl.textContent = statusText(state);
  observeButton.disabled = !state.supported;

  if (state.commandResult) {
    outputEl.textContent = JSON.stringify(state.commandResult, null, 2);
  }
}

saveButton.addEventListener("click", saveSettings);

observeButton.addEventListener("click", async () => {
  observeButton.disabled = true;
  outputEl.textContent = "Observando...";
  try {
    const response = await chrome.runtime.sendMessage({ type: "JOBPILOT_OBSERVE_ACTIVE_TAB" });
    if (response?.ok === false) {
      outputEl.textContent = response.error || "Nao foi possivel observar a pagina.";
      return;
    }

    const observation = response?.result || response;
    const summary = {
      url: observation.url,
      title: observation.title,
      elements: observation.elements?.length || 0,
      sample: observation.elements?.slice(0, 8)
    };
    outputEl.textContent = JSON.stringify(summary, null, 2);
    await refreshStatus();
  } catch (error) {
    outputEl.textContent = String(error?.message || error);
  } finally {
    observeButton.disabled = false;
  }
});

loadSettings().then(refreshStatus);
setInterval(refreshStatus, 5000);
