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
const scanDiskButton = document.getElementById('scan-disk');
const loadSelectedButton = document.getElementById('load-selected');
const runSelectedButton = document.getElementById('run-selected');
const diskListEl = document.getElementById('disk-list');
const clearHistoryButton = document.getElementById('clear-history');
const historyListEl = document.getElementById('history-list');
const inputsQueueEl = document.getElementById('inputs-queue');
const keyInputsEl = document.getElementById('key-inputs');

const sequences = {
  hello: ['10 PRINT "HELLO, WORLD!"', 'RUN'],
  fibonacci: ['LOAD "FIBONACCI"', 'RUN'],
  catalog: ['CATALOG'],
  lemonade: ['LOAD "LEMONADE"', 'RUN'],
  adventure: ['LOAD "ADVENTURE"', 'RUN'],
  guess: ['LOAD "GUESS"', 'RUN']
};

let sessionId = null;
const historyStorageKey = 'applesoft-history';
const diskFallback = ['ADVENTURE', 'FIBONACCI', 'GUESS', 'LEMONADE', 'LORES', 'MULTIPLICATION', 'WUMPUS'];
const commandHistory = JSON.parse(localStorage.getItem(historyStorageKey) ?? '[]');
let historyCursor = commandHistory.length;
let diskPrograms = [];
let selectedProgram = null;

apiBaseUrlEl.textContent = apiBaseUrl;

function appendOutput(text = '') {
  outputEl.textContent += `${text}\n`;
  outputEl.scrollTop = outputEl.scrollHeight;
}

function replaceOutput(text) {
  outputEl.textContent = `${text}\n`;
}

function setSession(id) {
  sessionId = id;
  sessionIdEl.textContent = id ?? 'OFFLINE';
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

function setDiskSelection(name) {
  selectedProgram = name;
  renderDiskPrograms();
}

function renderDiskPrograms() {
  diskListEl.innerHTML = '';

  if (diskPrograms.length === 0) {
    const empty = document.createElement('li');
    empty.className = 'empty';
    empty.textContent = 'Scan disk to discover programs.';
    diskListEl.appendChild(empty);
    return;
  }

  diskPrograms.forEach(name => {
    const li = document.createElement('li');
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = name;
    button.className = name === selectedProgram ? 'active' : '';
    button.addEventListener('click', () => setDiskSelection(name));
    li.appendChild(button);
    diskListEl.appendChild(li);
  });
}

function parseCatalogOutput(output) {
  const stopWords = new Set([
    'CATALOG', 'DISK', 'FREE', 'BLOCKS', 'BYTES', 'READY', 'ERROR', 'PRESS', 'SPACE', 'TO', 'CONTINUE', 'ESC', 'END'
  ]);

  const names = [...output.toUpperCase().matchAll(/\b([A-Z][A-Z0-9_]{1,24})(?:\.BAS)?\b/g)]
    .map(match => match[1])
    .filter(name => !stopWords.has(name));

  return [...new Set(names)].sort((a, b) => a.localeCompare(b));
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

function buildRuntimeInputs() {
  const inputsRaw = inputsQueueEl.value.trim();
  const keyRaw = keyInputsEl.value;

  const inputs = inputsRaw.length === 0
    ? undefined
    : inputsRaw.split(',').map(item => item.trim()).filter(item => item.length > 0);

  const keyInputs = keyRaw.length === 0 ? undefined : keyRaw;

  return { inputs, keyInputs };
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
  appendOutput('READY. TYPE BASIC OR USE THE DISK BUTTONS.');
}

async function resetSession() {
  if (!sessionId) {
    await createSession();
    return;
  }

  await apiRequest(`/api/session/${sessionId}/reset`, { method: 'POST', body: '{}' });
  appendOutput('SESSION RESET. MEMORY CLEARED.');
  diskPrograms = [];
  selectedProgram = null;
  renderDiskPrograms();
}

async function executeCommand(command) {
  if (!command.trim()) {
    return '';
  }

  if (!sessionId) {
    await createSession();
  }

  appendOutput(`]${command}`);
  rememberCommand(command);

  const runtime = buildRuntimeInputs();
  const payload = JSON.stringify({
    command,
    inputs: runtime.inputs,
    keyInputs: runtime.keyInputs
  });
  const data = await apiRequest(`/api/session/${sessionId}/execute`, {
    method: 'POST',
    body: payload
  });

  if (runtime.inputs && runtime.inputs.length > 0) {
    inputsQueueEl.value = '';
  }

  if (data?.output) {
    const cleanOutput = data.output.replace(/\n$/, '');
    appendOutput(cleanOutput);
    return cleanOutput;
  }

  return '';
}

async function runSequence(name) {
  const commands = sequences[name] ?? [];
  for (const command of commands) {
    await executeCommand(command);
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

commandInput.addEventListener('keydown', async event => {
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
  try {
    await resetSession();
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

clearScreenButton.addEventListener('click', () => {
  replaceOutput('APPLE ][ ONLINE\nREMOTE BASIC SUBSYSTEM\n');
});

async function scanDisk() {
  let catalogOutput = '';
  try {
    catalogOutput = await executeCommand('CATALOG');
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }

  const parsed = parseCatalogOutput(catalogOutput);
  diskPrograms = parsed.length > 0 ? parsed : [...diskFallback];
  selectedProgram = diskPrograms[0] ?? null;
  renderDiskPrograms();
}

scanDiskButton.addEventListener('click', async () => {
  await scanDisk();
});

loadSelectedButton.addEventListener('click', async () => {
  if (!selectedProgram) {
    appendOutput('?NO PROGRAM SELECTED');
    return;
  }

  try {
    await executeCommand(`LOAD "${selectedProgram}"`);
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

runSelectedButton.addEventListener('click', async () => {
  if (!selectedProgram) {
    appendOutput('?NO PROGRAM SELECTED');
    return;
  }

  try {
    await executeCommand(`LOAD "${selectedProgram}"`);
    await executeCommand('RUN');
  } catch (error) {
    appendOutput(`?ERROR: ${error.message}`);
  }
});

clearHistoryButton.addEventListener('click', () => {
  commandHistory.length = 0;
  historyCursor = 0;
  persistHistory();
  renderHistory();
  appendOutput('COMMAND HISTORY CLEARED.');
});

document.querySelectorAll('[data-command]').forEach(button => {
  button.addEventListener('click', async () => {
    try {
      await executeCommand(button.dataset.command ?? '');
    } catch (error) {
      appendOutput(`?ERROR: ${error.message}`);
    }
  });
});

document.querySelectorAll('[data-sequence]').forEach(button => {
  button.addEventListener('click', async () => {
    try {
      await runSequence(button.dataset.sequence ?? '');
    } catch (error) {
      appendOutput(`?ERROR: ${error.message}`);
    }
  });
});

replaceOutput('APPLE ][ ONLINE\nBOOTING REMOTE BASIC SUBSYSTEM...\n');
renderHistory();
renderDiskPrograms();
checkHealth().then(createSession).catch(error => {
  appendOutput(`?ERROR: ${error.message}`);
});