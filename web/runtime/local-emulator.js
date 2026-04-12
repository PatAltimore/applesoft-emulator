(function () {
  const BUILTIN_PROGRAMS = [
    "ADVENTURE",
    "FIBONACCI",
    "GUESS",
    "MULTIPLICATION",
    "WUMPUS"
  ];

  class IndexedDiskStore {
    constructor() {
      this.dbName = "applesoft-emulator";
      this.storeName = "programs";
      this.version = 1;
      this.initPromise = null;
      this.memoryFallback = new Map();
    }

    async init() {
      if (!this.initPromise) {
        this.initPromise = this.initInternal();
      }
      await this.initPromise;
    }

    async initInternal() {
      const db = await this.openDb().catch(() => null);
      if (!db) {
        for (const name of BUILTIN_PROGRAMS) {
          const source = await this.fetchBundledProgram(name);
          if (source) {
            this.memoryFallback.set(name, source);
          }
        }
        return;
      }

      await Promise.all(BUILTIN_PROGRAMS.map(async name => {
        const existing = await this.get(db, name);
        if (existing) {
          return;
        }

        const source = await this.fetchBundledProgram(name);
        if (source) {
          await this.put(db, name, source);
        }
      }));

      db.close();
    }

    async fetchBundledProgram(name) {
      try {
        const response = await fetch(`disk/${name}.bas`, { cache: "no-store" });
        if (!response.ok) {
          return null;
        }
        return await response.text();
      } catch {
        return null;
      }
    }

    openDb() {
      return new Promise((resolve, reject) => {
        if (!window.indexedDB) {
          reject(new Error("IndexedDB unavailable"));
          return;
        }

        const request = window.indexedDB.open(this.dbName, this.version);
        request.onerror = () => reject(request.error ?? new Error("IndexedDB open failed"));
        request.onupgradeneeded = () => {
          const db = request.result;
          if (!db.objectStoreNames.contains(this.storeName)) {
            db.createObjectStore(this.storeName, { keyPath: "name" });
          }
        };
        request.onsuccess = () => resolve(request.result);
      });
    }

    get(db, name) {
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.storeName, "readonly");
        const store = tx.objectStore(this.storeName);
        const req = store.get(name.toUpperCase());
        req.onerror = () => reject(req.error ?? new Error("IndexedDB get failed"));
        req.onsuccess = () => resolve(req.result?.source ?? null);
      });
    }

    getAllNames(db) {
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.storeName, "readonly");
        const store = tx.objectStore(this.storeName);
        const req = store.getAllKeys();
        req.onerror = () => reject(req.error ?? new Error("IndexedDB list failed"));
        req.onsuccess = () => resolve((req.result ?? []).map(key => String(key)));
      });
    }

    put(db, name, source) {
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.storeName, "readwrite");
        const store = tx.objectStore(this.storeName);
        const req = store.put({ name: name.toUpperCase(), source, updatedAt: Date.now() });
        req.onerror = () => reject(req.error ?? new Error("IndexedDB put failed"));
        req.onsuccess = () => resolve();
      });
    }

    async save(name, source) {
      await this.init();
      const upper = name.toUpperCase();

      const db = await this.openDb().catch(() => null);
      if (!db) {
        this.memoryFallback.set(upper, source);
        return;
      }

      await this.put(db, upper, source);
      db.close();
    }

    async load(name) {
      await this.init();
      const upper = name.toUpperCase();

      const db = await this.openDb().catch(() => null);
      if (!db) {
        return this.memoryFallback.get(upper) ?? null;
      }

      const value = await this.get(db, upper);
      db.close();
      return value;
    }

    async list() {
      await this.init();

      const db = await this.openDb().catch(() => null);
      if (!db) {
        return [...this.memoryFallback.keys()].sort((a, b) => a.localeCompare(b));
      }

      const names = await this.getAllNames(db);
      db.close();
      return names.sort((a, b) => a.localeCompare(b));
    }
  }

  class LocalApplesoftRuntime {
    constructor() {
      this.sessions = new Map();
      this.disk = new IndexedDiskStore();
    }

    createSession() {
      const sessionId = crypto.randomUUID().replace(/-/g, "");
      this.sessions.set(sessionId, this.createSessionState(sessionId));
      return sessionId;
    }

    createSessionState(sessionId) {
      return {
        id: sessionId,
        program: new Map(),
        vars: Object.create(null),
        stopRequested: false,
        awaitingInput: null,
        pendingInputApply: null,
        outputBuffer: [],
        outputListener: null,
        forStack: [],
        gosubStack: [],
        execution: null,
        textMode: "NORMAL"
      };
    }

    getSession(sessionId) {
      const existing = this.sessions.get(sessionId);
      if (existing) {
        return existing;
      }

      const created = this.createSessionState(sessionId);
      this.sessions.set(sessionId, created);
      return created;
    }

    async reset(sessionId) {
      const session = this.getSession(sessionId);
      session.stopRequested = true;
      session.awaitingInput = null;
      session.pendingInputApply = null;
      session.execution = null;
      this.sessions.set(sessionId, this.createSessionState(sessionId));
    }

    setTextMode(session, mode) {
      session.textMode = mode;
    }

    wrapStyled(text, mode) {
      if (!text) {
        return text;
      }

      if (mode === "INVERSE") {
        return `\u001b[7m${text}\u001b[0m`;
      }

      if (mode === "FLASH") {
        return `\u001b[5m${text}\u001b[0m`;
      }

      return text;
    }

    write(session, text) {
      session.outputBuffer.push(this.wrapStyled(text, session.textMode));
    }

    writeln(session, text = "") {
      session.outputBuffer.push(`${this.wrapStyled(text, session.textMode)}\n`);
    }

    flushOutput(session) {
      const output = session.outputBuffer.join("");
      session.outputBuffer.length = 0;
      return output;
    }

    setOutputListener(sessionId, listener) {
      const session = this.getSession(sessionId);
      session.outputListener = typeof listener === "function" ? listener : null;
    }

    emitBufferedOutput(session) {
      if (!session.outputListener) {
        return;
      }

      const chunk = this.flushOutput(session);
      if (chunk) {
        session.outputListener(chunk);
      }
    }

    parseMaybeLineNumber(command) {
      const trimmed = command.trimStart();
      if (!/^\d+/.test(trimmed)) {
        return null;
      }

      const match = trimmed.match(/^(\d+)\s*(.*)$/s);
      if (!match) {
        return null;
      }

      return {
        line: Number.parseInt(match[1], 10),
        text: match[2] ?? ""
      };
    }

    splitStatements(line) {
      const parts = [];
      let inString = false;
      let current = "";

      for (let i = 0; i < line.length; i += 1) {
        const ch = line[i];
        if (ch === '"') {
          inString = !inString;
          current += ch;
          continue;
        }

        if (ch === ':' && !inString) {
          parts.push(current.trim());
          current = "";
          continue;
        }

        current += ch;
      }

      if (current.trim().length > 0) {
        parts.push(current.trim());
      }

      return parts;
    }

    transformExpression(session, expr) {
      const tokens = [];
      const re = /"(?:""|[^"])*"|[A-Z][A-Z0-9$]*|\d+(?:\.\d+)?|<=|>=|<>|[()=+\-*/^,<>]/gi;
      let match;
      while ((match = re.exec(expr)) !== null) {
        tokens.push(match[0]);
      }

      const transformed = [];
      for (let i = 0; i < tokens.length; i += 1) {
        const token = tokens[i];
        const upper = token.toUpperCase();

        if (/^"/.test(token)) {
          transformed.push(token.replace(/""/g, '\\"'));
          continue;
        }

        if (/^\d/.test(token)) {
          transformed.push(token);
          continue;
        }

        if (upper === "AND") {
          transformed.push("&&");
          continue;
        }

        if (upper === "OR") {
          transformed.push("||");
          continue;
        }

        if (upper === "NOT") {
          transformed.push("!");
          continue;
        }

        if (token === "<>") {
          transformed.push("!=");
          continue;
        }

        if (token === "=") {
          transformed.push("==");
          continue;
        }

        if (token === "^") {
          transformed.push("**");
          continue;
        }

        if (/^[A-Z][A-Z0-9$]*$/i.test(token)) {
          const fnMap = {
            ABS: "ABS",
            INT: "INT",
            SQR: "SQR",
            RND: "RND",
            LEN: "LEN",
            VAL: "VAL",
            CHR$: "CHR",
            ASC: "ASC",
            LEFT$: "LEFT",
            RIGHT$: "RIGHT",
            MID$: "MID"
          };

          const next = tokens[i + 1];
          if (next === "(" && fnMap[upper]) {
            transformed.push(`f.${fnMap[upper]}`);
            continue;
          }

          transformed.push(`v(${JSON.stringify(upper)})`);
          continue;
        }

        transformed.push(token);
      }

      return transformed.join(" ");
    }

    evaluateExpression(session, expr) {
      const transformed = this.transformExpression(session, expr);
      const funcs = {
        ABS: x => Math.abs(Number(x)),
        INT: x => Math.floor(Number(x)),
        SQR: x => Math.sqrt(Number(x)),
        RND: () => Math.random(),
        LEN: s => String(s ?? "").length,
        VAL: s => {
          const n = Number(String(s ?? "").trim());
          return Number.isNaN(n) ? 0 : n;
        },
        CHR: x => String.fromCharCode(Number(x) & 0xff),
        ASC: s => {
          const text = String(s ?? "");
          return text.length > 0 ? text.charCodeAt(0) : 0;
        },
        LEFT: (s, n) => String(s ?? "").slice(0, Math.max(0, Number(n) || 0)),
        RIGHT: (s, n) => {
          const text = String(s ?? "");
          const count = Math.max(0, Number(n) || 0);
          return text.slice(Math.max(0, text.length - count));
        },
        MID: (s, start, n) => {
          const text = String(s ?? "");
          const offset = Math.max(0, (Number(start) || 1) - 1);
          if (n === undefined) {
            return text.slice(offset);
          }
          return text.slice(offset, offset + Math.max(0, Number(n) || 0));
        }
      };

      const readVar = name => {
        const value = session.vars[name];
        return value === undefined ? 0 : value;
      };

      try {
        // eslint-disable-next-line no-new-func
        const fn = new Function("v", "f", `return (${transformed});`);
        return fn(readVar, funcs);
      } catch {
        return 0;
      }
    }

    evaluateCondition(session, conditionExpr) {
      const value = this.evaluateExpression(session, conditionExpr);
      if (typeof value === "boolean") {
        return value;
      }
      if (typeof value === "string") {
        return value.length > 0;
      }
      return Number(value) !== 0;
    }

    formatNumber(value) {
      const num = Number(value);
      if (Number.isNaN(num)) {
        return " 0 ";
      }
      if (num === 0) {
        return " 0 ";
      }
      if (Number.isInteger(num)) {
        return num > 0 ? ` ${num} ` : `${num} `;
      }
      const text = String(num);
      return num > 0 ? ` ${text} ` : `${text} `;
    }

    parsePrintItems(body) {
      const items = [];
      let inString = false;
      let current = "";

      for (let i = 0; i < body.length; i += 1) {
        const ch = body[i];
        if (ch === '"') {
          inString = !inString;
          current += ch;
          continue;
        }

        if ((ch === ';' || ch === ',') && !inString) {
          items.push({ expr: current.trim(), sep: ch });
          current = "";
          continue;
        }

        current += ch;
      }

      if (current.trim().length > 0) {
        items.push({ expr: current.trim(), sep: "" });
      }

      return items;
    }

    ensureExecution(session) {
      if (!session.execution) {
        const lines = [...session.program.keys()].sort((a, b) => a - b);
        session.execution = {
          lines,
          lineIndex: new Map(lines.map((line, idx) => [line, idx])),
          linePos: 0,
          stmtPos: 0,
          statementsCache: new Map()
        };
      }
      return session.execution;
    }

    getStatements(exec, lineNo, lineText) {
      if (!exec.statementsCache.has(lineNo)) {
        exec.statementsCache.set(lineNo, this.splitStatements(lineText));
      }
      return exec.statementsCache.get(lineNo);
    }

    async runProgram(session) {
      const exec = this.ensureExecution(session);
      let steps = 0;

      while (exec.linePos < exec.lines.length) {
        if (session.stopRequested) {
          this.writeln(session, "BREAK");
          session.execution = null;
          session.forStack = [];
          session.gosubStack = [];
          return { awaitingInput: false, isKeyInput: false };
        }

        if (steps++ % 300 === 0) {
          // Flush chunks while running so long loops render progressively.
          this.emitBufferedOutput(session);
          await new Promise(resolve => setTimeout(resolve, 0));
        }

        const lineNo = exec.lines[exec.linePos];
        const lineText = session.program.get(lineNo) ?? "";
        const statements = this.getStatements(exec, lineNo, lineText);

        if (exec.stmtPos >= statements.length) {
          exec.linePos += 1;
          exec.stmtPos = 0;
          continue;
        }

        const stmt = statements[exec.stmtPos];
        const action = await this.executeStatement(session, exec, lineNo, stmt);

        if (action.type === "await-input") {
          return { awaitingInput: true, isKeyInput: action.isKeyInput };
        }

        if (action.type === "jump") {
          const idx = exec.lineIndex.get(action.line);
          if (idx === undefined) {
            this.writeln(session, "?UNDEF'D STATEMENT ERROR");
            session.execution = null;
            session.forStack = [];
            session.gosubStack = [];
            return { awaitingInput: false, isKeyInput: false };
          }
          exec.linePos = idx;
          exec.stmtPos = action.stmtPos ?? 0;
          continue;
        }

        if (action.type === "return") {
          if (session.gosubStack.length === 0) {
            this.writeln(session, "?RETURN WITHOUT GOSUB ERROR");
            session.execution = null;
            return { awaitingInput: false, isKeyInput: false };
          }
          const frame = session.gosubStack.pop();
          exec.linePos = frame.linePos;
          exec.stmtPos = frame.stmtPos;
          continue;
        }

        if (action.type === "stop") {
          session.execution = null;
          session.forStack = [];
          session.gosubStack = [];
          return { awaitingInput: false, isKeyInput: false };
        }

        exec.stmtPos += 1;
      }

      session.execution = null;
      session.forStack = [];
      session.gosubStack = [];
      this.emitBufferedOutput(session);
      return { awaitingInput: false, isKeyInput: false };
    }

    parseInputTarget(stmt, keyword) {
      const rest = stmt.slice(keyword.length).trim();
      const promptMatch = rest.match(/^"([^"]*)"\s*;\s*([A-Z][A-Z0-9$]*)$/i);
      if (promptMatch) {
        return { prompt: `${promptMatch[1]}? `, variable: promptMatch[2].toUpperCase() };
      }

      const plain = rest.match(/^([A-Z][A-Z0-9$]*)$/i);
      if (plain) {
        return { prompt: "? ", variable: plain[1].toUpperCase() };
      }

      return null;
    }

    applyInputValue(session, variable, input, isKey) {
      const value = isKey ? String(input ?? "").slice(0, 1) : String(input ?? "");
      if (variable.endsWith("$")) {
        session.vars[variable] = isKey ? value.toUpperCase() : value;
      } else {
        const parsed = Number(value);
        session.vars[variable] = Number.isNaN(parsed) ? 0 : parsed;
      }
    }

    async executeStatement(session, exec, lineNo, stmt) {
      if (!stmt || /^REM\b/i.test(stmt)) {
        return { type: "next" };
      }

      if (/^END\b/i.test(stmt) || /^STOP\b/i.test(stmt)) {
        return { type: "stop" };
      }

      if (/^NORMAL\b/i.test(stmt)) {
        this.setTextMode(session, "NORMAL");
        return { type: "next" };
      }

      if (/^INVERSE\b/i.test(stmt)) {
        this.setTextMode(session, "INVERSE");
        return { type: "next" };
      }

      if (/^FLASH\b/i.test(stmt)) {
        this.setTextMode(session, "FLASH");
        return { type: "next" };
      }

      if (/^HOME\b/i.test(stmt)) {
        this.write(session, "\u001b[2J\u001b[H");
        return { type: "next" };
      }

      if (/^PRINT\b/i.test(stmt) || /^\?/i.test(stmt)) {
        const body = stmt.replace(/^PRINT\b/i, "").replace(/^\?/, "").trim();
        if (!body) {
          this.writeln(session, "");
          return { type: "next" };
        }

        const items = this.parsePrintItems(body);
        let line = "";
        let noNewline = false;

        for (const item of items) {
          const value = this.evaluateExpression(session, item.expr || "0");
          line += typeof value === "number" ? this.formatNumber(value) : String(value);

          if (item.sep === ',') {
            line += " ";
            noNewline = true;
          } else if (item.sep === ';') {
            noNewline = true;
          } else {
            noNewline = false;
          }
        }

        if (noNewline) {
          this.write(session, line);
        } else {
          this.writeln(session, line);
        }

        return { type: "next" };
      }

      if (/^INPUT\b/i.test(stmt)) {
        const target = this.parseInputTarget(stmt, "INPUT");
        if (!target) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }

        this.write(session, target.prompt);
        session.awaitingInput = { isKeyInput: false, prompt: target.prompt };
        session.pendingInputApply = input => {
          this.applyInputValue(session, target.variable, input, false);
          this.writeln(session, String(input ?? ""));
        };
        return { type: "await-input", isKeyInput: false };
      }

      if (/^GET\b/i.test(stmt)) {
        const m = stmt.match(/^GET\s+([A-Z][A-Z0-9$]*)$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }

        session.awaitingInput = { isKeyInput: true, prompt: "" };
        session.pendingInputApply = input => {
          this.applyInputValue(session, m[1].toUpperCase(), input, true);
        };
        return { type: "await-input", isKeyInput: true };
      }

      if (/^IF\b/i.test(stmt)) {
        const m = stmt.match(/^IF\s+(.+?)\s+THEN\s+(.+)$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }

        if (!this.evaluateCondition(session, m[1])) {
          return { type: "next" };
        }

        const thenPart = m[2].trim();
        if (/^\d+$/.test(thenPart)) {
          return { type: "jump", line: Number.parseInt(thenPart, 10), stmtPos: 0 };
        }

        // Execute inline THEN statement immediately.
        return this.executeStatement(session, exec, lineNo, thenPart);
      }

      if (/^GOTO\b/i.test(stmt)) {
        const target = Number.parseInt(stmt.replace(/^GOTO\s+/i, "").trim(), 10);
        if (Number.isNaN(target)) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }
        return { type: "jump", line: target, stmtPos: 0 };
      }

      if (/^GOSUB\b/i.test(stmt)) {
        const target = Number.parseInt(stmt.replace(/^GOSUB\s+/i, "").trim(), 10);
        if (Number.isNaN(target)) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }
        session.gosubStack.push({ linePos: exec.linePos, stmtPos: exec.stmtPos + 1 });
        return { type: "jump", line: target, stmtPos: 0 };
      }

      if (/^RETURN\b/i.test(stmt)) {
        return { type: "return" };
      }

      if (/^FOR\b/i.test(stmt)) {
        const m = stmt.match(/^FOR\s+([A-Z][A-Z0-9$]*)\s*=\s*(.+?)\s+TO\s+(.+?)(?:\s+STEP\s+(.+))?$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }

        const varName = m[1].toUpperCase();
        const start = Number(this.evaluateExpression(session, m[2]));
        const limit = Number(this.evaluateExpression(session, m[3]));
        const step = m[4] ? Number(this.evaluateExpression(session, m[4])) : 1;

        session.vars[varName] = Number.isNaN(start) ? 0 : start;
        session.forStack.push({
          varName,
          limit: Number.isNaN(limit) ? 0 : limit,
          step: Number.isNaN(step) || step === 0 ? 1 : step,
          linePos: exec.linePos,
          stmtPos: exec.stmtPos + 1
        });
        return { type: "next" };
      }

      if (/^NEXT\b/i.test(stmt)) {
        const m = stmt.match(/^NEXT\s*([A-Z][A-Z0-9$]*)?$/i);
        const requestedVar = m?.[1]?.toUpperCase() ?? null;

        if (session.forStack.length === 0) {
          this.writeln(session, "?NEXT WITHOUT FOR ERROR");
          return { type: "next" };
        }

        let index = session.forStack.length - 1;
        if (requestedVar) {
          index = session.forStack.findIndex(frame => frame.varName === requestedVar);
          if (index < 0) {
            this.writeln(session, "?NEXT WITHOUT FOR ERROR");
            return { type: "next" };
          }
        }

        const frame = session.forStack[index];
        const current = Number(session.vars[frame.varName] ?? 0) + frame.step;
        session.vars[frame.varName] = current;

        const done = frame.step > 0 ? current > frame.limit : current < frame.limit;
        if (done) {
          session.forStack.splice(index, 1);
          return { type: "next" };
        }

        return { type: "jump", line: exec.lines[frame.linePos], stmtPos: frame.stmtPos };
      }

      if (/^(LET\b|[A-Z][A-Z0-9$]*\s*=)/i.test(stmt)) {
        const assign = stmt.replace(/^LET\s+/i, "");
        const m = assign.match(/^([A-Z][A-Z0-9$]*)\s*=\s*(.+)$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { type: "next" };
        }
        const variable = m[1].toUpperCase();
        const value = this.evaluateExpression(session, m[2]);
        session.vars[variable] = value;
        return { type: "next" };
      }

      this.writeln(session, "?UNSUPPORTED STATEMENT IN LOCAL MODE");
      return { type: "stop" };
    }

    async continueAfterInput(session) {
      const runResult = await this.runProgram(session);
      return {
        output: this.flushOutput(session),
        awaitingInput: runResult.awaitingInput,
        isKeyInput: runResult.isKeyInput
      };
    }

    parseCommandArgument(command, keyword) {
      const regex = new RegExp(`^${keyword}\\s+\\"?([A-Z0-9_.$-]+)\\"?$`, "i");
      const match = command.match(regex);
      return match?.[1] ?? null;
    }

    async executeCommand(session, trimmedInput) {
      if (/^HELP$/i.test(trimmedInput)) {
        this.writeln(session, "SUPPORTED COMMANDS:");
        this.writeln(session, "  HELP            SHOW THIS HELP");
        this.writeln(session, "  RUN             RUN PROGRAM");
        this.writeln(session, "  LIST            LIST PROGRAM LINES");
        this.writeln(session, "  NEW             CLEAR PROGRAM");
        this.writeln(session, "  LOAD \"NAME\"     LOAD PROGRAM FROM BROWSER DISK");
        this.writeln(session, "  SAVE \"NAME\"     SAVE PROGRAM TO BROWSER DISK");
        this.writeln(session, "  CATALOG         LIST PROGRAMS ON BROWSER DISK");
        this.writeln(session, "  DEL A,B         DELETE LINE RANGE");
        this.writeln(session, "  NORMAL|INVERSE|FLASH TEXT MODES");
        return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
      }

      if (/^(EXIT|QUIT)$/i.test(trimmedInput)) {
        return { output: "", awaitingInput: false, isKeyInput: false };
      }

      const maybeLine = this.parseMaybeLineNumber(trimmedInput);
      if (maybeLine) {
        if (!maybeLine.text.trim()) {
          session.program.delete(maybeLine.line);
        } else {
          session.program.set(maybeLine.line, maybeLine.text);
        }
        return { output: "", awaitingInput: false, isKeyInput: false };
      }

      if (/^LIST\b/i.test(trimmedInput)) {
        let start = -Infinity;
        let end = Infinity;
        const arg = trimmedInput.replace(/^LIST\s*/i, "");
        if (arg) {
          const m = arg.match(/^(\d+)\s*(?:,\s*(\d+)\s*)?$/);
          if (m) {
            start = Number.parseInt(m[1], 10);
            end = m[2] ? Number.parseInt(m[2], 10) : start;
          }
        }

        const lines = [...session.program.keys()].sort((a, b) => a - b);
        lines
          .filter(line => line >= start && line <= end)
          .forEach(line => this.writeln(session, `${line} ${session.program.get(line)}`));

        return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
      }

      if (/^NEW$/i.test(trimmedInput)) {
        session.program.clear();
        session.vars = Object.create(null);
        session.forStack = [];
        session.gosubStack = [];
        session.execution = null;
        return { output: "", awaitingInput: false, isKeyInput: false };
      }

      if (/^DEL\b/i.test(trimmedInput)) {
        const m = trimmedInput.match(/^DEL\s+(\d+)\s*,\s*(\d+)$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
        }

        const start = Number.parseInt(m[1], 10);
        const end = Number.parseInt(m[2], 10);
        const keys = [...session.program.keys()].filter(line => line >= start && line <= end);
        keys.forEach(line => session.program.delete(line));
        return { output: "", awaitingInput: false, isKeyInput: false };
      }

      if (/^SAVE\b/i.test(trimmedInput)) {
        const name = this.parseCommandArgument(trimmedInput, "SAVE");
        if (!name) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
        }

        const lines = [...session.program.keys()]
          .sort((a, b) => a - b)
          .map(line => `${line} ${session.program.get(line)}`)
          .join("\n");
        await this.disk.save(name, lines);
        return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
      }

      if (/^LOAD\b/i.test(trimmedInput)) {
        const name = this.parseCommandArgument(trimmedInput, "LOAD");
        if (!name) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
        }

        const source = await this.disk.load(name);
        if (source == null) {
          this.writeln(session, "?FILE NOT FOUND ERROR");
          return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
        }

        session.program.clear();
        source.split(/\r?\n/).forEach(line => {
          const parsed = this.parseMaybeLineNumber(line);
          if (parsed && parsed.text.trim()) {
            session.program.set(parsed.line, parsed.text);
          }
        });
        return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
      }

      if (/^CATALOG$/i.test(trimmedInput)) {
        const names = await this.disk.list();
        names.forEach(name => this.writeln(session, name));
        return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
      }

      if (/^RUN$/i.test(trimmedInput)) {
        session.stopRequested = false;
        session.execution = null;
        session.forStack = [];
        session.gosubStack = [];
        const runResult = await this.runProgram(session);
        return {
          output: this.flushOutput(session),
          awaitingInput: runResult.awaitingInput,
          isKeyInput: runResult.isKeyInput
        };
      }

      // Immediate-mode single statement support.
      session.stopRequested = false;
      const tempLineNo = -1;
      const action = await this.executeStatement(session, this.ensureExecution(session), tempLineNo, trimmedInput);
      if (action.type === "await-input") {
        return { output: this.flushOutput(session), awaitingInput: true, isKeyInput: action.isKeyInput };
      }

      session.execution = null;
      return { output: this.flushOutput(session), awaitingInput: false, isKeyInput: false };
    }

    async execute(sessionId, command) {
      await this.disk.init();
      const session = this.getSession(sessionId);
      const text = command ?? "";
      const trimmedInput = text.trimEnd();

      if (session.awaitingInput && session.pendingInputApply) {
        const applied = session.pendingInputApply;
        session.awaitingInput = null;
        session.pendingInputApply = null;
        applied(text);
        return this.continueAfterInput(session);
      }

      if (!trimmedInput.trim()) {
        return { output: "", awaitingInput: false, isKeyInput: false };
      }

      return this.executeCommand(session, trimmedInput);
    }

    requestStop(sessionId) {
      const session = this.getSession(sessionId);
      session.stopRequested = true;
    }
  }

  window.LocalApplesoftRuntime = LocalApplesoftRuntime;
})();
