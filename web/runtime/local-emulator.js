(function () {
  class LocalDiskStore {
    constructor() {
      this.keyPrefix = "applesoft:program:";
    }

    async save(name, source) {
      localStorage.setItem(`${this.keyPrefix}${name.toUpperCase()}`, source);
    }

    async load(name) {
      return localStorage.getItem(`${this.keyPrefix}${name.toUpperCase()}`);
    }

    async list() {
      const names = [];
      for (let i = 0; i < localStorage.length; i += 1) {
        const key = localStorage.key(i);
        if (key && key.startsWith(this.keyPrefix)) {
          names.push(key.slice(this.keyPrefix.length));
        }
      }
      return names.sort((a, b) => a.localeCompare(b));
    }

    async delRange(programMap, start, end) {
      const keys = [...programMap.keys()].filter(line => line >= start && line <= end);
      keys.forEach(line => programMap.delete(line));
    }
  }

  class LocalApplesoftRuntime {
    constructor() {
      this.sessions = new Map();
      this.disk = new LocalDiskStore();
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
        outputBuffer: []
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
      if (session.awaitingInput) {
        session.awaitingInput.resolve("");
      }
      this.sessions.set(sessionId, this.createSessionState(sessionId));
    }

    async submitInput(sessionId, input) {
      const session = this.getSession(sessionId);
      if (!session.awaitingInput) {
        return false;
      }

      const pending = session.awaitingInput;
      session.awaitingInput = null;
      pending.resolve(input ?? "");
      return true;
    }

    formatNumber(value) {
      if (value === 0) {
        return " 0 ";
      }

      if (Number.isInteger(value)) {
        return value > 0 ? ` ${value} ` : `${value} `;
      }

      const text = Number(value).toString();
      return value > 0 ? ` ${text} ` : `${text} `;
    }

    flushOutput(session) {
      const output = session.outputBuffer.join("");
      session.outputBuffer.length = 0;
      return output;
    }

    write(session, text) {
      session.outputBuffer.push(text);
    }

    writeln(session, text = "") {
      session.outputBuffer.push(`${text}\n`);
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

    evalExpr(session, expr) {
      const trimmed = expr.trim();
      if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
        return trimmed.slice(1, -1);
      }

      if (/^[A-Z][A-Z0-9$]*$/i.test(trimmed)) {
        const value = session.vars[trimmed.toUpperCase()];
        return value ?? 0;
      }

      const n = Number(trimmed);
      if (!Number.isNaN(n)) {
        return n;
      }

      return trimmed;
    }

    evalCondition(session, expr) {
      const compareMatch = expr.match(/^(.*?)(=|<>|<=|>=|<|>)(.*)$/);
      if (compareMatch) {
        const left = this.evalExpr(session, compareMatch[1]);
        const right = this.evalExpr(session, compareMatch[3]);
        switch (compareMatch[2]) {
          case "=": return left === right;
          case "<>": return left !== right;
          case "<": return Number(left) < Number(right);
          case ">": return Number(left) > Number(right);
          case "<=": return Number(left) <= Number(right);
          case ">=": return Number(left) >= Number(right);
          default: return false;
        }
      }

      return Number(this.evalExpr(session, expr)) !== 0;
    }

    async requestInput(session, prompt) {
      this.write(session, prompt);
      const value = await new Promise(resolve => {
        session.awaitingInput = { resolve };
      });
      this.writeln(session, value);
      return value;
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

    async executeRun(session) {
      const lines = [...session.program.keys()].sort((a, b) => a - b);
      if (lines.length === 0) {
        return;
      }

      const lineIndex = new Map(lines.map((line, index) => [line, index]));
      let pc = 0;
      let steps = 0;

      while (pc < lines.length) {
        if (session.stopRequested) {
          this.writeln(session, "BREAK");
          break;
        }

        if (steps++ > 300000) {
          this.writeln(session, "?PROGRAM TOO LONG");
          break;
        }

        const lineNo = lines[pc];
        const rawLine = session.program.get(lineNo) ?? "";
        const statements = this.splitStatements(rawLine);
        let jumped = false;

        for (const stmt of statements) {
          if (!stmt || /^REM\b/i.test(stmt)) {
            continue;
          }

          if (/^END\b/i.test(stmt) || /^STOP\b/i.test(stmt)) {
            return;
          }

          if (/^PRINT\b/i.test(stmt) || /^\?/i.test(stmt)) {
            const body = stmt.replace(/^PRINT\b/i, "").replace(/^\?/, "").trim();
            if (!body) {
              this.writeln(session, "");
              continue;
            }

            const noNewLine = /;\s*$/.test(body);
            const pieces = body.replace(/;\s*$/, "").split(";");
            let output = "";
            for (const piece of pieces) {
              const value = this.evalExpr(session, piece);
              output += typeof value === "number" ? this.formatNumber(value) : String(value);
            }
            if (noNewLine) {
              this.write(session, output);
            } else {
              this.writeln(session, output);
            }
            continue;
          }

          if (/^INPUT\b/i.test(stmt)) {
            const match = stmt.match(/^INPUT\s*(?:"([^"]*)"\s*;\s*)?([A-Z][A-Z0-9$]*)$/i);
            if (!match) {
              this.writeln(session, "?SYNTAX ERROR");
              continue;
            }
            const prompt = (match[1] ?? "") + "? ";
            const variable = match[2].toUpperCase();
            const input = await this.requestInput(session, prompt);
            if (variable.endsWith("$")) {
              session.vars[variable] = input;
            } else {
              const n = Number(input);
              session.vars[variable] = Number.isNaN(n) ? 0 : n;
            }
            continue;
          }

          if (/^LET\b/i.test(stmt) || /^[A-Z][A-Z0-9$]*\s*=/.test(stmt)) {
            const assign = stmt.replace(/^LET\s+/i, "");
            const match = assign.match(/^([A-Z][A-Z0-9$]*)\s*=\s*(.+)$/i);
            if (!match) {
              this.writeln(session, "?SYNTAX ERROR");
              continue;
            }
            const variable = match[1].toUpperCase();
            const value = this.evalExpr(session, match[2]);
            session.vars[variable] = value;
            continue;
          }

          if (/^IF\b/i.test(stmt)) {
            const match = stmt.match(/^IF\s+(.+?)\s+THEN\s+(\d+)$/i);
            if (!match) {
              this.writeln(session, "?SYNTAX ERROR");
              continue;
            }
            if (this.evalCondition(session, match[1])) {
              const target = Number.parseInt(match[2], 10);
              const idx = lineIndex.get(target);
              if (idx === undefined) {
                this.writeln(session, "?UNDEF'D STATEMENT ERROR");
                return;
              }
              pc = idx;
              jumped = true;
              break;
            }
            continue;
          }

          if (/^GOTO\b/i.test(stmt)) {
            const target = Number.parseInt(stmt.replace(/^GOTO\s+/i, "").trim(), 10);
            const idx = lineIndex.get(target);
            if (idx === undefined) {
              this.writeln(session, "?UNDEF'D STATEMENT ERROR");
              return;
            }
            pc = idx;
            jumped = true;
            break;
          }

          this.writeln(session, "?UNSUPPORTED STATEMENT IN LOCAL MODE");
          return;
        }

        if (!jumped) {
          pc += 1;
        }
      }
    }

    async execute(sessionId, command) {
      const session = this.getSession(sessionId);
      const trimmedInput = (command ?? "").trimEnd();

      if (session.awaitingInput) {
        await this.submitInput(sessionId, command ?? "");
        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (!trimmedInput) {
        return { output: "", awaitingInput: false };
      }

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
        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (/^(EXIT|QUIT)$/i.test(trimmedInput)) {
        return { output: "", awaitingInput: false };
      }

      const maybeLine = this.parseMaybeLineNumber(trimmedInput);
      if (maybeLine) {
        if (!maybeLine.text.trim()) {
          session.program.delete(maybeLine.line);
        } else {
          session.program.set(maybeLine.line, maybeLine.text);
        }
        return { output: "", awaitingInput: false };
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

        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (/^NEW$/i.test(trimmedInput)) {
        session.program.clear();
        session.vars = Object.create(null);
        return { output: "", awaitingInput: false };
      }

      if (/^DEL\b/i.test(trimmedInput)) {
        const m = trimmedInput.match(/^DEL\s+(\d+)\s*,\s*(\d+)$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false };
        }
        await this.disk.delRange(session.program, Number.parseInt(m[1], 10), Number.parseInt(m[2], 10));
        return { output: "", awaitingInput: false };
      }

      if (/^SAVE\b/i.test(trimmedInput)) {
        const m = trimmedInput.match(/^SAVE\s+"?([A-Z0-9_.$-]+)"?$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false };
        }
        const lines = [...session.program.keys()].sort((a, b) => a - b).map(line => `${line} ${session.program.get(line)}`);
        await this.disk.save(m[1], lines.join("\n"));
        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (/^LOAD\b/i.test(trimmedInput)) {
        const m = trimmedInput.match(/^LOAD\s+"?([A-Z0-9_.$-]+)"?$/i);
        if (!m) {
          this.writeln(session, "?SYNTAX ERROR");
          return { output: this.flushOutput(session), awaitingInput: false };
        }
        const source = await this.disk.load(m[1]);
        if (source == null) {
          this.writeln(session, "?FILE NOT FOUND ERROR");
          return { output: this.flushOutput(session), awaitingInput: false };
        }
        session.program.clear();
        source.split(/\r?\n/).forEach(line => {
          const parsed = this.parseMaybeLineNumber(line);
          if (parsed && parsed.text.trim()) {
            session.program.set(parsed.line, parsed.text);
          }
        });
        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (/^CATALOG$/i.test(trimmedInput)) {
        const names = await this.disk.list();
        names.forEach(name => this.writeln(session, name));
        return { output: this.flushOutput(session), awaitingInput: false };
      }

      if (/^RUN$/i.test(trimmedInput)) {
        session.stopRequested = false;
        await this.executeRun(session);
        const awaitingInput = session.awaitingInput !== null;
        return { output: this.flushOutput(session), awaitingInput };
      }

      this.writeln(session, "?UNSUPPORTED COMMAND IN LOCAL MODE");
      return { output: this.flushOutput(session), awaitingInput: false };
    }
  }

  window.LocalApplesoftRuntime = LocalApplesoftRuntime;
})();
