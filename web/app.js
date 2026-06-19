const outputEl = document.getElementById('output');
const commandInput = document.getElementById('command-input');
const runtimeHintEl = document.getElementById('runtime-hint');

const localRuntime = window.LocalApplesoftRuntime
  ? new window.LocalApplesoftRuntime()
  : null;

let sessionId = null;
let currentPromptIsKeyInput = false;
const historyStorageKey = 'applesoft-history';
const commandHistory = JSON.parse(localStorage.getItem(historyStorageKey) ?? '[]');
let historyCursor = commandHistory.length;

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

function persistHistory() {
  localStorage.setItem(historyStorageKey, JSON.stringify(commandHistory.slice(-120)));
}

function rememberCommand(command) {
  const trimmed = command.trim();
  if (!trimmed) {
    return;
  }

  if (commandHistory[commandHistory.length - 1] !== trimmed) {
    commandHistory.push(trimmed);
    persistHistory();
  }

  historyCursor = commandHistory.length;
}

function createSession() {
  sessionId = localRuntime.createSession();
  appendOutput('READY.');
  executeCommand('HELP');
}

async function executeCommand(command) {
  // Handle HOME command to clear screen
  if (command.trim().toUpperCase() === 'HOME') {
    replaceOutput('APPLE ][ ONLINE\nBOOTING BROWSER BASIC SUBSYSTEM...\n');
    clearRuntimeHint();
    return;
  }

  if (!sessionId) {
    createSession();
  }

  const awaitingInput = runtimeHintEl.classList.contains('active');

  // Nothing to do for an empty line unless a program is waiting for input.
  if (!command.trim() && !awaitingInput) {
    return;
  }

  // Echo the typed command at the prompt (but not the answers to INPUT/GET).
  if (!awaitingInput) {
    appendOutput(`]${command}`);
    rememberCommand(command);
  }

  localRuntime.setOutputListener(sessionId, chunk => {
    appendOutputChunk(chunk);
  });

  let result;
  try {
    result = await localRuntime.execute(sessionId, command);
  } finally {
    localRuntime.setOutputListener(sessionId, null);
  }

  if (result?.output) {
    appendOutputChunk(result.output);
  }

  if (result?.awaitingInput) {
    currentPromptIsKeyInput = !!result.isKeyInput;
    setRuntimeHint(
      currentPromptIsKeyInput
        ? 'Program is waiting for a key. Enter a single character and press Enter.'
        : 'Program is waiting for input. Type your answer and press Enter.',
      currentPromptIsKeyInput ? 'key' : 'warning'
    );
    commandInput.focus();
  } else {
    currentPromptIsKeyInput = false;
    clearRuntimeHint();
  }
}

commandInput.addEventListener('keydown', async event => {
  if (event.key === 'Enter') {
    event.preventDefault();
    const command = commandInput.value;
    commandInput.value = '';

    try {
      await executeCommand(command);
    } catch (error) {
      appendOutput(`?ERROR: ${error.message}`);
    }
    return;
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

document.addEventListener('keydown', event => {
  if (!sessionId) {
    return;
  }

  const isBreak = event.key === 'Escape' || (event.ctrlKey && event.key.toLowerCase() === 'c');
  if (!isBreak) {
    return;
  }

  event.preventDefault();
  localRuntime.requestStop(sessionId);
  setRuntimeHint('BREAK requested...', 'warning');
});

if (!localRuntime) {
  replaceOutput('?FATAL: BROWSER INTERPRETER FAILED TO LOAD.');
} else {
  replaceOutput('APPLE ][ ONLINE\nBOOTING BROWSER BASIC SUBSYSTEM...\n');
  commandInput.focus();
  createSession();
}
