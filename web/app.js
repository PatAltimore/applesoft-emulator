const config = window.APP_CONFIG ?? {};

const outputEl = document.getElementById('output');
const commandForm = document.getElementById('command-form');
const commandInput = document.getElementById('command-input');
const clearScreenButton = document.getElementById('clear-screen');
const clearHistoryButton = document.getElementById('clear-history');
const runtimeHintEl = document.getElementById('runtime-hint');
const useBrowserRuntime = config.useBrowserRuntime !== false;
const localRuntime = useBrowserRuntime && window.LocalApplesoftRuntime
  ? new window.LocalApplesoftRuntime()
  : null;

console.log('[APPLESOFT] Runtime Configuration:', {
  useBrowserRuntime,
  hasLocalApplesoftRuntime: !!window.LocalApplesoftRuntime,
  localRuntimeCreated: !!localRuntime
});

let sessionId = null;
let currentPromptIsKeyInput = false;
const historyStorageKey = 'applesoft-history';
const commandHistory = JSON.parse(localStorage.getItem(historyStorageKey) ?? '[]');
let historyCursor = commandHistory.length;

let hubConnection = null;
let hubReady = false;
const outputStyleState = {
  inverse: false,
  flash: false
};

function appendOutput(text = '') {
  appendOutputChunk(`${text}\n`);
}

function appendOutputChunk(text = '') {
  const normalized = normalizeOutputForDisplay(text);
  appendStyledText(normalized);
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
  outputEl.textContent = '';
  outputStyleState.inverse = false;
  outputStyleState.flash = false;
  appendOutputChunk(`${text}\n`);
}

function applyAnsiCodes(codesText) {
  const codes = codesText.split(';').map(code => Number.parseInt(code, 10));
  for (const code of codes) {
    if (code === 0) {
      outputStyleState.inverse = false;
      outputStyleState.flash = false;
    } else if (code === 7) {
      outputStyleState.inverse = true;
    } else if (code === 27) {
      outputStyleState.inverse = false;
    } else if (code === 5) {
      outputStyleState.flash = true;
    } else if (code === 25) {
      outputStyleState.flash = false;
    }
  }
}

function appendStyledText(text) {
  const ansiRegex = /\x1b\[([0-9;]+)m/g;
  let lastIndex = 0;
  let match;

  while ((match = ansiRegex.exec(text)) !== null) {
    const plain = text.slice(lastIndex, match.index);
    appendStyledRun(plain);
    applyAnsiCodes(match[1]);
    lastIndex = ansiRegex.lastIndex;
  }

  appendStyledRun(text.slice(lastIndex));
}

function appendStyledRun(text) {
  if (!text) {
    return;
  }

  const isStyled = outputStyleState.inverse || outputStyleState.flash;
  if (!isStyled) {
    outputEl.appendChild(document.createTextNode(text));
    return;
  }

  const span = document.createElement('span');
  if (outputStyleState.inverse) {
    span.classList.add('crt-inverse');
  }
  if (outputStyleState.flash) {
    span.classList.add('crt-flash');
  }
  span.textContent = text;
  outputEl.appendChild(span);
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

  if (!useBrowserRuntime && hubReady && sessionId) {
    hubConnection.invoke('AttachSession', sessionId).catch(error => {
      appendOutput(`?HUB ATTACH ERROR: ${error.message}`);
    });
  }
}

function persistHistory() {
  localStorage.setItem(historyStorageKey, JSON.stringify(commandHistory.slice(-120)));
}

function renderHistory() {
  // History panel removed from UI; users navigate history with arrow keys
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
  // Browser-only mode doesn't use the backend API
  if (useBrowserRuntime) {
    throw new Error('Backend API not available in browser-only mode');
  }

  const apiBaseUrl = (config.apiBaseUrl ?? window.location.origin).replace(/\/$/, '');
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
  if (useBrowserRuntime) {
    hubReady = false;
    return;
  }

  if (!window.signalR) {
    appendOutput('?SIGNALR CLIENT NOT AVAILABLE. USING LEGACY MODE.');
    return;
  }

  const apiBaseUrl = (config.apiBaseUrl ?? window.location.origin).replace(/\/$/, '');
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
  // Health check no longer needed in browser-only mode
}

async function createSession() {
  if (useBrowserRuntime && localRuntime) {
    setSession(localRuntime.createSession());
    appendOutput(`NEW SESSION: ${sessionId}`);
    appendOutput('READY. BROWSER INTERPRETER MODE ENABLED.');
    await executeCommand('HELP');
    return;
  }

  const data = await apiRequest('/api/session', { method: 'POST', body: '{}' });
  setSession(data.sessionId);
  appendOutput(`NEW SESSION: ${data.sessionId}`);
  appendOutput(hubReady
    ? 'READY. LIVE INTERACTIVE MODE ENABLED.'
    : 'READY. TYPE BASIC OR USE THE DISK BUTTONS.');

  await executeCommand('HELP');
}

async function resetSession() {
  if (!sessionId) {
    await createSession();
    return;
  }

  if (useBrowserRuntime && localRuntime) {
    await localRuntime.reset(sessionId);
    appendOutput('SESSION RESET. MEMORY CLEARED.');
    currentPromptIsKeyInput = false;
    clearRuntimeHint();
    await executeCommand('HELP');
    return;
  }

  await apiRequest(`/api/session/${sessionId}/reset`, { method: 'POST', body: '{}' });
  appendOutput('SESSION RESET. MEMORY CLEARED.');
  currentPromptIsKeyInput = false;
  clearRuntimeHint();

  if (hubReady) {
    await hubConnection.invoke('AttachSession', sessionId);
  }

  await executeCommand('HELP');
}

async function executeCommand(command) {
  if (!sessionId) {
    await createSession();
  }

  if (useBrowserRuntime && localRuntime) {
    if (!command.trim() && !runtimeHintEl.classList.contains('active')) {
      return;
    }

    if (!runtimeHintEl.classList.contains('active') && command.trim()) {
      appendOutput(`]${command}`);
      rememberCommand(command);
    }

    const result = await localRuntime.execute(sessionId, command);
    if (result?.output) {
      appendOutputChunk(result.output);
    }

    if (result?.awaitingInput) {
      currentPromptIsKeyInput = !!result.isKeyInput;
      if (currentPromptIsKeyInput) {
        setRuntimeHint('Program is waiting for a key. Enter a single character and press SEND.', 'key');
      } else {
        setRuntimeHint('Program is waiting for input. Type your answer and press SEND.', 'warning');
      }
      commandInput.focus();
    } else {
      currentPromptIsKeyInput = false;
      clearRuntimeHint();
    }
    return;
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

commandInput.addEventListener('keydown', async event => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault();
    const command = commandInput.value;
    commandInput.value = '';

    try {
      await executeCommand(command);
    } catch (error) {
      appendOutput(`?ERROR: ${error.message}`);
    }
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

replaceOutput('APPLE ][ ONLINE\nBOOTING BROWSER BASIC SUBSYSTEM...\n');
renderHistory();

if (useBrowserRuntime) {
  checkHealth()
    .then(createSession)
    .catch(error => {
      appendOutput(`?ERROR: ${error.message}`);
    });
} else {
  Promise.all([checkHealth(), initializeHub()])
    .then(createSession)
    .catch(error => {
      appendOutput(`?ERROR: ${error.message}`);
    });
}