const config = window.APP_CONFIG ?? {};
const apiBaseUrl = (config.apiBaseUrl ?? window.location.origin).replace(/\/$/, '');

const outputEl = document.getElementById('output');
const commandForm = document.getElementById('command-form');
const commandInput = document.getElementById('command-input');
const sessionIdEl = document.getElementById('session-id');
const apiStatusEl = document.getElementById('api-status');
const apiBaseUrlEl = document.getElementById('api-base-url');
const newSessionButton = document.getElementById('new-session');
const resetSessionButton = document.getElementById('reset-session');
const clearScreenButton = document.getElementById('clear-screen');
const clearHistoryButton = document.getElementById('clear-history');
const historyListEl = document.getElementById('history-list');
const runtimeHintEl = document.getElementById('runtime-hint');

let sessionId = null;
let currentPromptIsKeyInput = false;
const historyStorageKey = 'applesoft-history';
const commandHistory = JSON.parse(localStorage.getItem(historyStorageKey) ?? '[]');
let historyCursor = commandHistory.length;

let hubConnection = null;
let hubReady = false;

apiBaseUrlEl.textContent = apiBaseUrl;

function appendOutput(text = '') {
  outputEl.textContent += `${text}\n`;
  outputEl.scrollTop = outputEl.scrollHeight;
}

function appendOutputChunk(text = '') {
  outputEl.textContent += normalizeOutputForDisplay(text);
  outputEl.scrollTop = outputEl.scrollHeight;
}

function normalizeOutputForDisplay(text = '') {
  if (!text) {
    return '';
  }

  let normalized = text.replace(/\r/g, '');
  normalized = normalized.replace(/^[ \t]{200,}/, '');
  normalized = normalized.replace(/\n[ \t]{200,}/g, '\n');
  return normalized;
}

function replaceOutput(text) {
  outputEl.textContent = `${text}\n`;
}

function clearRuntimeHint() {
  runtimeHintEl.textContent = '';
  runtimeHintEl.className = 'runtime-hint';
}

function setRuntimeHint(message, kind) {
  runtimeHintEl.textContent = message;
  runtimeHintEl.className = `runtime-hint active ${kind}`;
}

function setSession(id) {
  sessionId = id;
  sessionIdEl.textContent = id ?? 'OFFLINE';

  if (hubReady && sessionId) {
    hubConnection.invoke('AttachSession', sessionId).catch(error => {
      appendOutput(`?HUB ATTACH ERROR: ${error.message}`);
    });
  }
}

function persistHistory() {
  localStorage.setItem(historyStorageKey, JSON.stringify(commandHistory.slice(-120)));
}

function renderHistory() {
  historyListEl.innerHTML = '';

  if (commandHistory.length === 0) {
    const empty = document.createElement('li');
    empty.className = 'empty';
    empty.textContent = 'No command history yet.';
    historyListEl.appendChild(empty);
    return;
  }

  [...commandHistory].reverse().slice(0, 20).forEach(command => {
    const li = document.createElement('li');
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = command;
    button.addEventListener('click', () => {
      commandInput.value = command;
      commandInput.focus();
    });
    li.appendChild(button);
    historyListEl.appendChild(li);
  });
}

function rememberCommand(command) {
  const trimmed = command.trim();
  if (!trimmed) {
    return;
  }

  if (commandHistory[commandHistory.length - 1] !== trimmed) {
    commandHistory.push(trimmed);
    persistHistory();
    renderHistory();
  }

  historyCursor = commandHistory.length;
}

async function apiRequest(path, options = {}) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {})
    },
    ...options
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function initializeHub() {
  if (!window.signalR) {
    appendOutput('?SIGNALR CLIENT NOT AVAILABLE. USING LEGACY MODE.');
    return;
  }

  hubConnection = new window.signalR.HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/emulator`)
    .withAutomaticReconnect()
    .build();

  hubConnection.on('ReceiveOutput', text => {
    appendOutputChunk(text);
  });

  hubConnection.on('InputRequested', request => {
    currentPromptIsKeyInput = !!request?.isKeyInput;
    const hint = currentPromptIsKeyInput 
      ? 'Program is waiting for a key. Enter a single character.' 
      : 'Program is waiting for input. Type your answer and press SEND.';
    const kind = currentPromptIsKeyInput ? 'key' : 'warning';
    setRuntimeHint(hint, kind);
    commandInput.focus();
  });

  hubConnection.on('ExecutionError', message => {
    appendOutput(`?ERROR: ${message}`);
    clearRuntimeHint();
  });

  hubConnection.on('ExecutionComplete', () => {
    currentPromptIsKeyInput = false;
    clearRuntimeHint();
  });

  hubConnection.onreconnecting(() => {
    hubReady = false;
    setRuntimeHint('Reconnecting live terminal...', 'warning');
  });

  hubConnection.onreconnected(async () => {
    hubReady = true;
    if (sessionId) {
      await hubConnection.invoke('AttachSession', sessionId);
    }
    setRuntimeHint('Live terminal reconnected.', 'key');
  });

  try {
    await hubConnection.start();
    hubReady = true;
    clearRuntimeHint();
  } catch (error) {
    hubReady = false;
    appendOutput(`?HUB OFFLINE: ${error.message}`);
  }
}

async function checkHealth() {
  try {
    const health = await apiRequest('/health', { method: 'GET' });
    apiStatusEl.textContent = health.status?.toUpperCase() ?? 'OK';
  } catch (error) {
    apiStatusEl.textContent = 'OFFLINE';
    appendOutput(`?NETWORK ERROR: ${error.message}`);
  }
}

async function createSession() {
  const data = await apiRequest('/api/session', { method: 'POST', body: '{}' });
  setSession(data.sessionId);
  appendOutput(`NEW SESSION: ${data.sessionId}`);
  appendOutput(hubReady
    ? 'READY. LIVE INTERACTIVE MODE ENABLED.'
    : 'READY. TYPE BASIC OR USE THE DISK BUTTONS.');
}

async function resetSession() {
  if (!sessionId) {
    await createSession();
    return;
  }

  await apiRequest(`/api/session/${sessionId}/reset`, { method: 'POST', body: '{}' });
  appendOutput('SESSION RESET. MEMORY CLEARED.');
  currentPromptIsKeyInput = false;
  clearRuntimeHint();

  if (hubReady) {
    await hubConnection.invoke('AttachSession', sessionId);
  }
}

async function executeCommand(command) {
  if (!sessionId) {
    await createSession();
  }

  // If a program is waiting for input, submit through the live channel first.
  // This allows GET prompts like "PRESS ANY KEY" to accept Enter/blank input.
  if (runtimeHintEl.classList.contains('active')) {
    if (hubReady) {
      const inputToSend = currentPromptIsKeyInput
        ? (command.length === 0 ? '\n' : command[0])
        : command;
      await hubConnection.invoke('SubmitInput', sessionId, inputToSend);
    }
    return;
  }

  if (!command.trim()) {
    return;
  }

  appendOutput(`]${command}`);
  rememberCommand(command);

  if (hubReady) {
    await hubConnection.invoke('StartExecution', sessionId, command);
    return;
  }
}

commandForm.addEventListener('submit', async event => {
  event.preventDefault();
  const command = commandInput.value;
  commandInput.value = '';

  try {
    await executeCommand(command);
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

commandInput.addEventListener('keydown', event => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    commandForm.requestSubmit();
  }

  if (event.key === 'ArrowUp') {
    event.preventDefault();
    if (historyCursor > 0) {
      historyCursor -= 1;
      commandInput.value = commandHistory[historyCursor] ?? '';
    }
  }

  if (event.key === 'ArrowDown') {
    event.preventDefault();
    if (historyCursor < commandHistory.length - 1) {
      historyCursor += 1;
      commandInput.value = commandHistory[historyCursor] ?? '';
    } else {
      historyCursor = commandHistory.length;
      commandInput.value = '';
    }
  }
});

newSessionButton.addEventListener('click', async () => {
  try {
    await createSession();
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

resetSessionButton.addEventListener('click', async () => {
  replaceOutput('APPLE ][ ONLINE\nREMOTE BASIC SUBSYSTEM\n');
  clearRuntimeHint();

  try {
    await resetSession();
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

clearScreenButton.addEventListener('click', () => {
  replaceOutput('APPLE ][ ONLINE\nREMOTE BASIC SUBSYSTEM\n');
  clearRuntimeHint();
});

clearHistoryButton.addEventListener('click', () => {
  commandHistory.length = 0;
  historyCursor = 0;
  persistHistory();
  renderHistory();
  appendOutput('COMMAND HISTORY CLEARED.');
});

replaceOutput('APPLE ][ ONLINE\nBOOTING REMOTE BASIC SUBSYSTEM...\n');
renderHistory();

Promise.all([checkHealth(), initializeHub()])
  .then(createSession)
  .catch(error => {
    appendOutput(`?ERROR: ${error.message}`);
  });