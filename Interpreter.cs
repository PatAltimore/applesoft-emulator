namespace ApplesoftEmulator;

// Represents errors that occur during Applesoft BASIC interpretation.
public class BasicException : Exception
{
// Initializes a new instance of the BasicException class with a specified error message.
// message: The message that describes the error.
public BasicException(string message) : base(message) { }
}

// Represents a STOP or BREAK event in Applesoft BASIC execution.
public class StopException : Exception
{
// Gets the line number where the STOP or BREAK occurred.
public int LineNumber { get; }
// Initializes a new instance of the StopException class for a specific line number.
// lineNumber: The line number where execution stopped.
public StopException(int lineNumber) : base($"BREAK IN {lineNumber}") { LineNumber = lineNumber; }
}

// FOR/NEXT loop state
public class ForState
{
// Gets or sets the name of the loop variable.
public string Variable { get; set; } = "";
// Gets or sets the limit value for the loop.
public double Limit { get; set; }
// Gets or sets the step value for the loop.
public double StepValue { get; set; }
// Gets or sets the line number where the FOR statement appears.
public int LineNumber { get; set; }
// Gets or sets the token position in the line for the FOR statement.
public int TokenPosition { get; set; }
// Gets or sets the program index for the FOR statement.
public int ProgramIndex { get; set; }
// True when the loop body is empty (FOR x TO y: NEXT) — a delay loop.
public bool IsEmptyBody { get; set; }
}

// User-defined function (DEF FN)
public class UserFunction
{
// Gets or sets the parameter name for the user-defined function.
public string ParamName { get; set; } = "";
// Gets or sets the list of tokens representing the function body.
public List<Token> BodyTokens { get; set; } = new();
}

// The main Applesoft BASIC interpreter.
public class Interpreter
{
// Stores the program lines, keyed by line number.
private readonly SortedDictionary<int, string> _program = new();
// Stores scalar variables by name.
private readonly Dictionary<string, BasicValue> _variables = new();
// Stores array variables by name.
private readonly Dictionary<string, BasicValue[]> _arrays = new();
// Stores array dimensions by array name.
private readonly Dictionary<string, int[]> _arrayDimensions = new();
// Stack for managing nested FOR/NEXT loops.
private readonly Stack<ForState> _forStack = new();
// Stack for managing GOSUB/RETURN calls.
private readonly Stack<(int lineNumber, int tokenPos, int progIdx)> _gosubStack = new();
// Stores user-defined functions (DEF FN) by name.
private readonly Dictionary<string, UserFunction> _userFunctions = new();
// Stores DATA values collected from the program.
private readonly List<string> _dataValues = new();
// Points to the next DATA value to be READ.
private int _dataPointer;
// Random number generator for RND function.
private Random _random = new();
// Tokenizer for parsing program lines and statements.
private readonly Tokenizer _tokenizer = new();
// Evaluator for parsing and evaluating expressions.
private readonly ExpressionEvaluator _evaluator;

// The current list of tokens being executed.
private List<Token> _currentTokens = new();
// The current token position in the token list.
private int _tokenPos;
// The current line number being executed.
private int _currentLineNumber;
// Indicates whether the interpreter is currently running a program.
private bool _running;
// List of line numbers in the current program.
private List<int> _lineNumbers = new();
// The current index in the line number list.
private int _programIndex;

    // Memory simulation for PEEK/POKE
// Simulated memory for PEEK and POKE operations.
private readonly byte[] _memory = new byte[65536];

// LORES graphics (40 columns x 40 rows, 16 colors)
private byte[,] _loRes = new byte[40, 40];
private int _loResColor;
private bool _graphicsMode;

// Apple II LORES 16-color palette (approximate RGB)
private static readonly (int R, int G, int B)[] LoResColors =
{
    (  0,   0,   0),  // 0  Black
    (221,   0,  51),  // 1  Red
    (  0,   0, 204),  // 2  Dark Blue
    (187,   0, 204),  // 3  Purple
    (  0, 170,   0),  // 4  Dark Green
    (128, 128, 128),  // 5  Gray 1
    ( 34,  34, 221),  // 6  Medium Blue
    (102, 170, 255),  // 7  Light Blue
    ( 85,  51,   0),  // 8  Brown
    (255, 119,   0),  // 9  Orange
    (170, 170, 170),  // 10 Gray 2
    (255, 119, 119),  // 11 Pink
    (  0, 221,   0),  // 12 Light Green
    (238, 238,   0),  // 13 Yellow
    (  0, 238, 238),  // 14 Aqua
    (255, 255, 255),  // 15 White
};

// Path to the Disk folder used for SAVE, LOAD, and CATALOG.
// Resolves the Disk folder from the base directory or the current working directory.
private static readonly string DiskPath = ResolveDiskPath();

// Finds the disk folder, checking the current working directory first, then the application base directory.
// This ensures SAVE writes to the project's disk folder (visible in the source tree) when running via dotnet run.
private static string ResolveDiskPath()
{
    // Try current working directory first (project root, used by dotnet run)
    string cwdDisk = Path.Combine(Directory.GetCurrentDirectory(), "disk");
    if (Directory.Exists(cwdDisk)) return cwdDisk;

    // Try AppContext.BaseDirectory (build output, for standalone execution)
    string baseDisk = Path.Combine(AppContext.BaseDirectory, "disk");
    if (Directory.Exists(baseDisk)) return baseDisk;

    // Default to current working directory (will be created on SAVE)
    return cwdDisk;
}

// Resolves a filename within the Disk folder using case-insensitive matching.
// On case-sensitive filesystems (Linux), finds the actual file regardless of casing.
// Returns the full path if found, or the original-cased path if not.
private static string ResolveFilePath(string fileName)
{
    string path = Path.Combine(DiskPath, fileName);
    // If exact match exists or directory doesn't exist, return as-is
    if (File.Exists(path) || !Directory.Exists(DiskPath))
        return path;

    // Case-insensitive search for the file on Linux
    var match = Directory.GetFiles(DiskPath)
        .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    return match ?? path;
}

// Initializes a new instance of the Interpreter class.
public Interpreter()
    {
        _evaluator = new ExpressionEvaluator(this);
    }

    #region Public API

// Stores or removes a program line.
// lineNumber: The line number to store or remove.
// text: The program text. If empty, the line is removed.
public void StoreLine(int lineNumber, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            _program.Remove(lineNumber);
        else
            _program[lineNumber] = text;
    }

// Runs the stored Applesoft BASIC program.
// startLine: The line number to start execution from, or -1 for the first line.
public void Run(int startLine = -1)
    {
        _variables.Clear();
        _arrays.Clear();
        _arrayDimensions.Clear();
        _forStack.Clear();
        _gosubStack.Clear();
        _dataPointer = 0;
        CollectData();

        _lineNumbers = new List<int>(_program.Keys);
        _programIndex = 0;

        if (startLine > 0)
        {
            _programIndex = _lineNumbers.IndexOf(startLine);
            if (_programIndex < 0)
            {
                // Find next line >= startLine
                _programIndex = _lineNumbers.FindIndex(n => n >= startLine);
                if (_programIndex < 0)
                {
                    Console.WriteLine("?UNDEF'D STATEMENT ERROR");
                    return;
                }
            }
        }

        _running = true;

        try
        {
            while (_running && _programIndex < _lineNumbers.Count)
            {
                _currentLineNumber = _lineNumbers[_programIndex];
                string line = _program[_currentLineNumber];
                _currentTokens = _tokenizer.Tokenize(line);
                _tokenPos = 0;
                _programIndex++;

                ExecuteStatements();
            }
        }
        catch (StopException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (BasicException ex)
        {
            Console.WriteLine($"{ex.Message} IN {_currentLineNumber}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?ERROR: {ex.Message} IN {_currentLineNumber}");
        }
    }

// Executes a single line of Applesoft BASIC code directly (immediate mode).
// line: The line of code to execute.
public void ExecuteDirect(string line)
    {
        _currentTokens = _tokenizer.Tokenize(line);
        _tokenPos = 0;
        _currentLineNumber = 0;
        _running = true;
        _lineNumbers = new List<int>(_program.Keys);

        try
        {
            ExecuteStatements();
        }
        catch (BasicException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

// Lists the program lines between the specified start and end line numbers.
// startLine: The starting line number.
// endLine: The ending line number.
public void ListProgram(int startLine = 0, int endLine = int.MaxValue)
    {
        foreach (var kvp in _program)
        {
            if (kvp.Key >= startLine && kvp.Key <= endLine)
                Console.WriteLine($"{kvp.Key}  {kvp.Value}");
        }
    }

// Clears the current program and all variables.
public void NewProgram()
    {
        _program.Clear();
        _variables.Clear();
        _arrays.Clear();
        _arrayDimensions.Clear();
        _forStack.Clear();
        _gosubStack.Clear();
        _userFunctions.Clear();
        _dataValues.Clear();
        _dataPointer = 0;
    }

// Saves the current program to the Disk folder.
// In DOS 3.3, SAVE is silent on success — no confirmation message is printed.
// name: The program name (with or without .bas extension).
public void SaveProgram(string name)
    {
        Directory.CreateDirectory(DiskPath); // create Disk folder if it doesn't exist
        if (!name.EndsWith(".bas", StringComparison.OrdinalIgnoreCase))
            name += ".bas";
        string path = ResolveFilePath(name);
        using var writer = new StreamWriter(path);
        foreach (var kvp in _program)
            writer.WriteLine($"{kvp.Key} {kvp.Value}"); // write each line as "linenum text"
        // DOS 3.3: silent on success, no output
    }

// Loads a program from the Disk folder, replacing the current program.
// In DOS 3.3, LOAD is silent on success. Errors use DOS-style messages (no '?' prefix).
// name: The program name (with or without .bas extension).
public void LoadProgram(string name)
    {
        if (!name.EndsWith(".bas", StringComparison.OrdinalIgnoreCase))
            name += ".bas";
        string path = ResolveFilePath(name);
        if (!File.Exists(path))
        {
            Console.WriteLine("FILE NOT FOUND");
            return;
        }
        NewProgram(); // clear existing program before loading
        foreach (string line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ParseAndStore(line); // parse "linenum text" and store in program
        }
        // DOS 3.3: silent on success, no output
    }

// Lists all .bas programs available in the Disk folder, emulating the DOS 3.3 CATALOG command.
// Output format matches the Apple ][ DOS 3.3 display:
//   blank line
//   DISK VOLUME 254
//   blank line
//   [lock][type] [sectors] [filename]
// Where lock is '*' (locked) or ' ' (unlocked), type is 'A' (Applesoft),
// sectors is a 3-digit zero-padded count based on file size (256 bytes/sector),
// and filename is up to 30 characters (the DOS 3.3 maximum).
public void CatalogDisk()
    {
        if (!Directory.Exists(DiskPath))
        {
            Console.WriteLine();
            Console.WriteLine("DISK VOLUME 254");
            Console.WriteLine();
            return;
        }
        // Collect and sort program files alphabetically
        var files = Directory.GetFiles(DiskPath, "*.bas")
                             .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                             .ToList();
        Console.WriteLine();
        Console.WriteLine("DISK VOLUME 254");
        Console.WriteLine();
        foreach (var filePath in files)
        {
            var fileInfo = new FileInfo(filePath);
            // Calculate sector count: 1 sector for the track/sector list + data sectors
            // Each sector is 256 bytes; minimum 2 sectors per file
            int dataSectors = Math.Max(1, (int)Math.Ceiling(fileInfo.Length / 256.0));
            int totalSectors = dataSectors + 1; // +1 for Track/Sector list sector
            string name = Path.GetFileNameWithoutExtension(filePath).ToUpper();
            // DOS 3.3 filenames are max 30 characters
            if (name.Length > 30) name = name[..30];
            // Format: " A NNN FILENAME" (space = unlocked, A = Applesoft BASIC)
            Console.WriteLine($" A {totalSectors:D3} {name}");
        }
        Console.WriteLine();
    }

// Deletes program lines between the specified start and end line numbers.
// start: The starting line number.
// end: The ending line number.
public void DeleteLines(int start, int end)
    {
        var toRemove = _program.Keys.Where(k => k >= start && k <= end).ToList();
        foreach (var key in toRemove)
            _program.Remove(key);
    }

// Gets a value indicating whether a program is currently loaded.
public bool HasProgram => _program.Count > 0;

    #endregion

    #region Variable Access (used by ExpressionEvaluator)

// Gets the value of a scalar variable.
// name: The variable name.
// Returns: The value of the variable, or a default value if not set.
public BasicValue GetVariable(string name)
    {
        if (_variables.TryGetValue(name.ToUpper(), out var val))
            return val;
        // Default: 0 for numeric, "" for string
        return name.EndsWith('$') ? BasicValue.FromString("") : BasicValue.FromNumber(0);
    }

// Sets the value of a scalar variable.
// name: The variable name.
// value: The value to set.
public void SetVariable(string name, BasicValue value)
    {
        _variables[name.ToUpper()] = value;
    }

// Gets the value of an array element.
// name: The array name.
// indices: The indices of the element.
// Returns: The value of the array element.
public BasicValue GetArrayValue(string name, List<int> indices)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        return _arrays[key][flatIndex];
    }

// Sets the value of an array element.
// name: The array name.
// indices: The indices of the element.
// value: The value to set.
public void SetArrayValue(string name, List<int> indices, BasicValue value)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        _arrays[key][flatIndex] = value;
    }

// Returns the LORES color index at the given pixel coordinate.
// x: The column (0-39). y: The row (0-39).
// Returns: Color index 0-15, or 0 if out of range or not in graphics mode.
public int GetScrnColor(int x, int y)
{
    if (!_graphicsMode || x < 0 || x >= 40 || y < 0 || y >= 40) return 0;
    return _loRes[x, y];
}

// Returns a pseudo-random number between 0 and 1.
// arg: If negative, seeds the generator; otherwise, returns a random value.
// Returns: A random double between 0 and 1.
public double GetRandom(double arg)
    {
        if (arg < 0) _random = new Random(unchecked((int)(arg * int.MaxValue)));
        return _random.NextDouble();
    }

// Returns the value at the specified memory address.
// address: The memory address to peek.
// Returns: The byte value at the address, or 0 if out of range.
public int Peek(int address)
    {
        if (address >= 0 && address < 65536)
            return _memory[address];
        return 0;
    }

// Calls a user-defined function (DEF FN) with the specified argument.
// name: The function name.
// arg: The argument to pass to the function.
// Returns: The result of the function call.
public BasicValue CallUserFunction(string name, BasicValue arg)
    {
        string key = name.ToUpper();
        if (!_userFunctions.TryGetValue(key, out var func))
            throw new BasicException($"?UNDEF'D FUNCTION ERROR: FN{name}");

        // Save old value, set parameter, evaluate, restore
        var oldVal = GetVariable(func.ParamName);
        SetVariable(func.ParamName, arg);

        var eval = new ExpressionEvaluator(this);
        eval.Init(func.BodyTokens, 0);
        var result = eval.Evaluate();

        SetVariable(func.ParamName, oldVal);
        return result;
    }

    #endregion

    #region Statement Execution

// Executes all statements in the current line.
private void ExecuteStatements()
    {
        while (_running && _tokenPos < _currentTokens.Count && _currentTokens[_tokenPos].Type != TokenType.EndOfLine)
        {
            ExecuteStatement();

            // Handle colon-separated statements.
            // Skip ALL consecutive colons — multiple colons (e.g. :::) are valid empty
            // statements in Applesoft BASIC and must not be passed to ExecuteStatement.
            bool foundColon = false;
            while (_tokenPos < _currentTokens.Count && _currentTokens[_tokenPos].Type == TokenType.Colon)
            {
                _tokenPos++;
                foundColon = true;
            }
            if (foundColon) continue;
            break;
        }
    }

// Gets the current token being processed.
private Token CurrentToken => _currentTokens[_tokenPos];

// Executes a single BASIC statement at the current token position.
private void ExecuteStatement()
    {
        var tok = CurrentToken;

        switch (tok.Type)
        {
            case TokenType.PRINT: ExecutePrint(); break;
            case TokenType.INPUT: ExecuteInput(); break;
            case TokenType.GET: ExecuteGet(); break;
            case TokenType.LET: _tokenPos++; ExecuteLet(); break;
            case TokenType.IF: ExecuteIf(); break;
            case TokenType.GOTO: ExecuteGoto(); break;
            case TokenType.GOSUB: ExecuteGosub(); break;
            case TokenType.RETURN: ExecuteReturn(); break;
            case TokenType.FOR: ExecuteFor(); break;
            case TokenType.NEXT: ExecuteNext(); break;
            case TokenType.REM: _tokenPos = _currentTokens.Count - 1; break;
            case TokenType.END: _running = false; break;
            case TokenType.STOP: throw new StopException(_currentLineNumber);
            case TokenType.DIM: ExecuteDim(); break;
            case TokenType.DATA: _tokenPos = _currentTokens.Count - 1; break; // skip DATA at runtime
            case TokenType.READ: ExecuteRead(); break;
            case TokenType.RESTORE: _dataPointer = 0; _tokenPos++; break;
            case TokenType.DEF: ExecuteDef(); break;
            case TokenType.ON: ExecuteOn(); break;
            case TokenType.LOMEM: ExecuteLomem(); break;
            case TokenType.INHash:
            case TokenType.PRHash:
                ExecuteChannelSelect();
                break;
            case TokenType.HOME:
                if (_graphicsMode)
                {
                    // In GR mode, clear only the 4-line text area at the bottom
                    int savedL = Console.CursorLeft, savedT = Console.CursorTop;
                    for (int r = 20; r < 24; r++)
                    {
                        try { Console.SetCursorPosition(0, r); Console.Write(new string(' ', 40)); } catch { }
                    }
                    try { Console.SetCursorPosition(0, 20); } catch { }
                }
                else
                {
                    Console.Clear();
                }
                _tokenPos++;
                break;
            case TokenType.HTAB: ExecuteHtab(); break;
            case TokenType.VTAB: ExecuteVtab(); break;
            case TokenType.POKE: ExecutePoke(); break;
            case TokenType.CALL: ExecuteCall(); break;
            case TokenType.GR: ExecuteGr(); break;
            case TokenType.TEXT: ExecuteText(); break;
            case TokenType.COLOR: ExecuteColor(); break;
            case TokenType.PLOT: ExecutePlot(); break;
            case TokenType.HLIN: ExecuteHlin(); break;
            case TokenType.VLIN: ExecuteVlin(); break;
            case TokenType.RUN: ExecuteRun(); break;
            case TokenType.LIST: ExecuteList(); break;
            case TokenType.NEW: NewProgram(); _tokenPos++; break;
            case TokenType.SAVE: ExecuteSave(); break;
            case TokenType.LOAD: ExecuteLoadCmd(); break;
            case TokenType.DEL: ExecuteDel(); break;
            case TokenType.CATALOG: CatalogDisk(); _tokenPos++; break;
            case TokenType.Identifier:
                // Implicit LET
                ExecuteLet();
                break;
            default:
                throw new BasicException($"?SYNTAX ERROR: UNEXPECTED {tok.Type}");
        }
    }

// Executes the PRINT statement.
private void ExecutePrint()
    {
        _tokenPos++; // skip PRINT
        bool needNewline = true;

        while (_tokenPos < _currentTokens.Count &&
               CurrentToken.Type != TokenType.EndOfLine &&
               CurrentToken.Type != TokenType.Colon)
        {
            if (CurrentToken.Type == TokenType.Semicolon)
            {
                _tokenPos++;
                needNewline = false;
                continue;
            }
            if (CurrentToken.Type == TokenType.Comma)
            {
                _tokenPos++;
                // Tab to next 16-column zone
                int col = Console.CursorLeft;
                int nextTab = ((col / 16) + 1) * 16;
                Console.Write(new string(' ', nextTab - col));
                needNewline = false;
                continue;
            }

            _evaluator.Init(_currentTokens, _tokenPos);
            var val = _evaluator.Evaluate();
            _tokenPos = _evaluator.Position;

            if (val.IsString)
                Console.Write(val.StringValue);
            else
                Console.Write(BasicValue.FormatNumber(val.NumberValue));

            needNewline = true;
        }

        if (needNewline)
            Console.WriteLine();
    }

// Executes the GET statement, reading a single keypress without waiting for Enter.
// On the Apple II, GET reads one character from the keyboard.
private void ExecuteGet()
    {
        _tokenPos++; // skip GET
        string varName = CurrentToken.Text;
        _tokenPos++;

        // Read a single key without displaying it
        var key = Console.ReadKey(true);
        string ch = key.KeyChar == '\r' || key.KeyChar == '\n' ? "\r" : key.KeyChar.ToString();

        if (varName.EndsWith('$'))
            SetVariable(varName, BasicValue.FromString(ch.ToUpper()));
        else
        {
            // Numeric GET returns the ASCII value... but on real Apple II
            // GET to a numeric var is unusual. Store 0 if not a digit.
            if (double.TryParse(ch, out double num))
                SetVariable(varName, BasicValue.FromNumber(num));
            else
                SetVariable(varName, BasicValue.FromNumber(0));
        }
    }

// Executes the INPUT statement.
private void ExecuteInput()
    {
        _tokenPos++; // skip INPUT

        // Optional prompt string
        string prompt = "? ";
        if (CurrentToken.Type == TokenType.StringLiteral)
        {
            prompt = CurrentToken.Text;
            _tokenPos++;
            if (CurrentToken.Type == TokenType.Semicolon)
            {
                _tokenPos++;
                prompt += "? ";
            }
            else if (CurrentToken.Type == TokenType.Comma)
            {
                _tokenPos++;
            }
        }

        // Collect INPUT targets (variables or array elements)
        var targets = new List<InputTarget>();
        targets.Add(ParseInputTarget());
        while (CurrentToken.Type == TokenType.Comma)
        {
            _tokenPos++;
            targets.Add(ParseInputTarget());
        }

        bool done = false;
        while (!done)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine() ?? "";
            string[] parts = input.Split(',');

            if (parts.Length < targets.Count)
            {
                // Not enough values, ask again for remaining
                for (int i = 0; i < Math.Min(parts.Length, targets.Count); i++)
                    AssignInputValue(targets[i], parts[i].Trim());

                if (parts.Length < targets.Count)
                {
                    Console.Write("?? ");
                    string? more = Console.ReadLine() ?? "";
                    var remaining = more.Split(',');
                    for (int i = 0; i < Math.Min(remaining.Length, targets.Count - parts.Length); i++)
                        AssignInputValue(targets[parts.Length + i], remaining[i].Trim());
                }
                done = true;
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                    AssignInputValue(targets[i], parts[i].Trim());
                done = true;
            }
        }
    }

// Represents an INPUT assignment target (scalar variable or array element).
private sealed class InputTarget
{
    public string Name { get; set; } = "";
    public List<int>? Indices { get; set; }
}

// Parses a single INPUT target from the current token position.
// Supports scalar variables and array references like A(I) or B$(I,J).
private InputTarget ParseInputTarget()
    {
        if (CurrentToken.Type != TokenType.Identifier)
            throw new BasicException("?SYNTAX ERROR: EXPECTED VARIABLE");

        string name = CurrentToken.Text;
        _tokenPos++;

        if (CurrentToken.Type != TokenType.LeftParen)
            return new InputTarget { Name = name };

        _tokenPos++; // skip '('
        var indices = new List<int>();

        _evaluator.Init(_currentTokens, _tokenPos);
        indices.Add((int)_evaluator.Evaluate().NumberValue);
        _tokenPos = _evaluator.Position;

        while (CurrentToken.Type == TokenType.Comma)
        {
            _tokenPos++;
            _evaluator.Init(_currentTokens, _tokenPos);
            indices.Add((int)_evaluator.Evaluate().NumberValue);
            _tokenPos = _evaluator.Position;
        }

        if (CurrentToken.Type != TokenType.RightParen)
            throw new BasicException("?SYNTAX ERROR: EXPECTED )");
        _tokenPos++;

        return new InputTarget { Name = name, Indices = indices };
    }

// Assigns a value to a variable as a result of INPUT.
// target: The INPUT target (variable or array element).
// value: The value to assign.
private void AssignInputValue(InputTarget target, string value)
    {
        string varName = target.Name;
        BasicValue parsedValue;

        if (varName.EndsWith('$'))
            parsedValue = BasicValue.FromString(value);
        else
        {
            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double num))
                parsedValue = BasicValue.FromNumber(num);
            else
                parsedValue = BasicValue.FromNumber(0);
        }

        if (target.Indices is null)
            SetVariable(varName, parsedValue);
        else
            SetArrayValue(varName, target.Indices, parsedValue);
    }

// Executes a LET or implicit assignment statement.
private void ExecuteLet()
    {
        string varName = CurrentToken.Text;
        _tokenPos++;

        if (CurrentToken.Type == TokenType.LeftParen)
        {
            // Array assignment
            _tokenPos++;
            var indices = new List<int>();
            _evaluator.Init(_currentTokens, _tokenPos);
            indices.Add((int)_evaluator.Evaluate().NumberValue);
            _tokenPos = _evaluator.Position;

            while (CurrentToken.Type == TokenType.Comma)
            {
                _tokenPos++;
                _evaluator.Init(_currentTokens, _tokenPos);
                indices.Add((int)_evaluator.Evaluate().NumberValue);
                _tokenPos = _evaluator.Position;
            }

            if (CurrentToken.Type != TokenType.RightParen)
                throw new BasicException("?SYNTAX ERROR: EXPECTED )");
            _tokenPos++;

            if (CurrentToken.Type != TokenType.Equal)
                throw new BasicException("?SYNTAX ERROR: EXPECTED =");
            _tokenPos++;

            _evaluator.Init(_currentTokens, _tokenPos);
            var value = _evaluator.Evaluate();
            _tokenPos = _evaluator.Position;

            SetArrayValue(varName, indices, value);
        }
        else
        {
            // Simple variable assignment
            if (CurrentToken.Type != TokenType.Equal)
                throw new BasicException("?SYNTAX ERROR: EXPECTED =");
            _tokenPos++;

            _evaluator.Init(_currentTokens, _tokenPos);
            var value = _evaluator.Evaluate();
            _tokenPos = _evaluator.Position;

            SetVariable(varName, value);
        }
    }

// Executes the IF...THEN statement.
private void ExecuteIf()
    {
        _tokenPos++; // skip IF

        _evaluator.Init(_currentTokens, _tokenPos);
        var condition = _evaluator.Evaluate();
        _tokenPos = _evaluator.Position;

        if (CurrentToken.Type != TokenType.THEN)
            throw new BasicException("?SYNTAX ERROR: EXPECTED THEN");
        _tokenPos++;

        if (condition.NumberValue != 0)
        {
            // Condition true - check if THEN is followed by a line number
            if (CurrentToken.Type == TokenType.Number)
            {
                GotoLine((int)CurrentToken.NumericValue);
            }
            else
            {
                // Execute remaining statements
                ExecuteStatements();
            }
        }
        else
        {
            // Condition false - skip rest of line
            _tokenPos = _currentTokens.Count - 1;
        }
    }

// Executes the GOTO statement.
private void ExecuteGoto()
    {
        _tokenPos++; // skip GOTO
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        GotoLine(lineNum);
    }

// Executes the GOSUB statement.
private void ExecuteGosub()
    {
        _tokenPos++; // skip GOSUB
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        _gosubStack.Push((_currentLineNumber, _tokenPos, _programIndex));
        GotoLine(lineNum);
    }

// Executes the RETURN statement.
private void ExecuteReturn()
    {
        _tokenPos++;
        if (_gosubStack.Count == 0)
            throw new BasicException("?RETURN WITHOUT GOSUB ERROR");

        var (lineNum, tokPos, progIdx) = _gosubStack.Pop();
        _programIndex = progIdx;

        // Skip any remaining statements on this line
        _tokenPos = _currentTokens.Count - 1;
    }

// Executes the FOR statement, initializing a FOR/NEXT loop.
private void ExecuteFor()
    {
        _tokenPos++; // skip FOR
        string varName = CurrentToken.Text;
        _tokenPos++;

        if (CurrentToken.Type != TokenType.Equal)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        _evaluator.Init(_currentTokens, _tokenPos);
        var startVal = _evaluator.Evaluate();
        _tokenPos = _evaluator.Position;

        if (CurrentToken.Type != TokenType.TO)
            throw new BasicException("?SYNTAX ERROR: EXPECTED TO");
        _tokenPos++;

        _evaluator.Init(_currentTokens, _tokenPos);
        var endVal = _evaluator.Evaluate();
        _tokenPos = _evaluator.Position;

        double step = 1;
        if (CurrentToken.Type == TokenType.STEP)
        {
            _tokenPos++;
            _evaluator.Init(_currentTokens, _tokenPos);
            step = _evaluator.Evaluate().NumberValue;
            _tokenPos = _evaluator.Position;
        }

        SetVariable(varName, BasicValue.FromNumber(startVal.NumberValue));

        // Detect empty-body delay loop: skip colons and check if NEXT follows immediately.
        bool isEmpty = false;
        int peek = _tokenPos;
        while (peek < _currentTokens.Count && _currentTokens[peek].Type == TokenType.Colon)
            peek++;
        if (peek < _currentTokens.Count && _currentTokens[peek].Type == TokenType.NEXT)
            isEmpty = true;

        _forStack.Push(new ForState
        {
            Variable = varName.ToUpper(),
            Limit = endVal.NumberValue,
            StepValue = step,
            LineNumber = _currentLineNumber,
            TokenPosition = _tokenPos,
            ProgramIndex = _programIndex,
            IsEmptyBody = isEmpty
        });
    }

// Executes the NEXT statement, advancing a FOR/NEXT loop.
private void ExecuteNext()
    {
        _tokenPos++; // skip NEXT

        string? varName = null;
        if (CurrentToken.Type == TokenType.Identifier)
        {
            varName = CurrentToken.Text.ToUpper();
            _tokenPos++;
        }

        if (_forStack.Count == 0)
            throw new BasicException("?NEXT WITHOUT FOR ERROR");

        // Find matching FOR
        ForState? forState = null;
        if (varName != null)
        {
            var tempStack = new Stack<ForState>();
            while (_forStack.Count > 0)
            {
                var top = _forStack.Peek();
                if (top.Variable == varName)
                {
                    forState = top;
                    break;
                }
                tempStack.Push(_forStack.Pop());
            }
            if (forState == null)
            {
                // Restore stack
                while (tempStack.Count > 0) _forStack.Push(tempStack.Pop());
                throw new BasicException("?NEXT WITHOUT FOR ERROR");
            }
            // Discard any inner FOR loops
        }
        else
        {
            forState = _forStack.Peek();
        }

        double currentVal = GetVariable(forState.Variable).NumberValue + forState.StepValue;
        SetVariable(forState.Variable, BasicValue.FromNumber(currentVal));

        // Approximate Apple II timing (1 MHz → ~1000 FOR/NEXT iterations/sec) for empty
        // delay loops so that delays like "FOR I = 1 TO 2000: NEXT" pause visibly.
        if (forState.IsEmptyBody)
            System.Threading.Thread.Sleep(1);

        bool done = forState.StepValue > 0
            ? currentVal > forState.Limit
            : currentVal < forState.Limit;

        if (!done)
        {
            // Loop back
            _programIndex = forState.ProgramIndex;
            // For same-line FOR/NEXT, restore token position to loop body start.
            // Without this, the remaining tokens after NEXT on the same line execute
            // instead of the loop body, so the loop only runs once.
            if (forState.LineNumber == _currentLineNumber)
            {
                _tokenPos = forState.TokenPosition;
            }
        }
        else
        {
            // Done with loop
            _forStack.Pop();
        }
    }

// Executes the DIM statement, defining array dimensions.
private void ExecuteDim()
    {
        _tokenPos++; // skip DIM

        do
        {
            string name = CurrentToken.Text.ToUpper();
            _tokenPos++;

            if (CurrentToken.Type != TokenType.LeftParen)
                throw new BasicException("?SYNTAX ERROR");
            _tokenPos++;

            var dims = new List<int>();
            _evaluator.Init(_currentTokens, _tokenPos);
            dims.Add((int)_evaluator.Evaluate().NumberValue);
            _tokenPos = _evaluator.Position;

            while (CurrentToken.Type == TokenType.Comma)
            {
                _tokenPos++;
                _evaluator.Init(_currentTokens, _tokenPos);
                dims.Add((int)_evaluator.Evaluate().NumberValue);
                _tokenPos = _evaluator.Position;
            }

            if (CurrentToken.Type != TokenType.RightParen)
                throw new BasicException("?SYNTAX ERROR: EXPECTED )");
            _tokenPos++;

            // Applesoft arrays are 0-based with size = dim+1
            int totalSize = 1;
            var dimSizes = new int[dims.Count];
            for (int i = 0; i < dims.Count; i++)
            {
                dimSizes[i] = dims[i] + 1;
                totalSize *= dimSizes[i];
            }

            var array = new BasicValue[totalSize];
            bool isStr = name.EndsWith('$');
            for (int i = 0; i < totalSize; i++)
                array[i] = isStr ? BasicValue.FromString("") : BasicValue.FromNumber(0);

            _arrays[name] = array;
            _arrayDimensions[name] = dimSizes;

        } while (CurrentToken.Type == TokenType.Comma && (++_tokenPos > 0));
    }

// Executes the READ statement, reading values from DATA.
private void ExecuteRead()
    {
        _tokenPos++; // skip READ

        do
        {
            if (_dataPointer >= _dataValues.Count)
                throw new BasicException("?OUT OF DATA ERROR");

            string varName = CurrentToken.Text;
            _tokenPos++;
            string value = _dataValues[_dataPointer++];

            if (varName.EndsWith('$'))
                SetVariable(varName, BasicValue.FromString(value));
            else
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double num))
                    SetVariable(varName, BasicValue.FromNumber(num));
                else
                    throw new BasicException("?TYPE MISMATCH ERROR");
            }
        } while (CurrentToken.Type == TokenType.Comma && (++_tokenPos > 0));
    }

// Executes the DEF FN statement, defining a user function.
private void ExecuteDef()
    {
        _tokenPos++; // skip DEF
        if (CurrentToken.Type != TokenType.FN)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++; // skip FN

        string funcName = CurrentToken.Text.ToUpper();
        _tokenPos++;

        if (CurrentToken.Type != TokenType.LeftParen)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        string paramName = CurrentToken.Text;
        _tokenPos++;

        if (CurrentToken.Type != TokenType.RightParen)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        if (CurrentToken.Type != TokenType.Equal)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        // Capture remaining tokens as function body
        var bodyTokens = new List<Token>();
        while (_tokenPos < _currentTokens.Count &&
               CurrentToken.Type != TokenType.EndOfLine &&
               CurrentToken.Type != TokenType.Colon)
        {
            bodyTokens.Add(CurrentToken);
            _tokenPos++;
        }
        bodyTokens.Add(new Token(TokenType.EndOfLine, ""));

        _userFunctions[funcName] = new UserFunction
        {
            ParamName = paramName,
            BodyTokens = bodyTokens
        };
    }

// Executes the ON...GOTO/GOSUB statement.
private void ExecuteOn()
    {
        _tokenPos++; // skip ON

        _evaluator.Init(_currentTokens, _tokenPos);
        int index = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        bool isGosub = CurrentToken.Type == TokenType.GOSUB;
        if (CurrentToken.Type != TokenType.GOTO && CurrentToken.Type != TokenType.GOSUB)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        // Read line number list
        var lineNums = new List<int>();
        _evaluator.Init(_currentTokens, _tokenPos);
        lineNums.Add((int)_evaluator.Evaluate().NumberValue);
        _tokenPos = _evaluator.Position;

        while (CurrentToken.Type == TokenType.Comma)
        {
            _tokenPos++;
            _evaluator.Init(_currentTokens, _tokenPos);
            lineNums.Add((int)_evaluator.Evaluate().NumberValue);
            _tokenPos = _evaluator.Position;
        }

        if (index >= 1 && index <= lineNums.Count)
        {
            int target = lineNums[index - 1];
            if (isGosub)
            {
                _gosubStack.Push((_currentLineNumber, _tokenPos, _programIndex));
            }
            GotoLine(target);
        }
        // If index out of range, just continue to next statement
    }

// Executes the HTAB statement, setting the horizontal cursor position.
private void ExecuteHtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int col = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorLeft = Math.Clamp(col - 1, 0, Console.BufferWidth - 1); } catch { }
    }

// Executes the VTAB statement, setting the vertical cursor position.
private void ExecuteVtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int row = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorTop = Math.Clamp(row - 1, 0, Console.BufferHeight - 1); } catch { }
    }

// Executes the POKE statement, writing a value to memory.
private void ExecutePoke()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int addr = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        if (CurrentToken.Type != TokenType.Comma)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        _evaluator.Init(_currentTokens, _tokenPos);
        int val = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        if (addr >= 0 && addr < 65536)
            _memory[addr] = (byte)(val & 0xFF);
    }

// Executes the CALL statement (no-op in this emulator).
private void ExecuteCall()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        _evaluator.Evaluate(); // evaluate and discard - no real machine code to run
        _tokenPos = _evaluator.Position;
    }

// Executes LOMEM as a compatibility no-op.
// Applesoft commonly uses "LOMEM: <expr>"; consume any provided expression.
private void ExecuteLomem()
    {
        _tokenPos++; // skip LOMEM

        if (CurrentToken.Type == TokenType.Colon || CurrentToken.Type == TokenType.Equal)
            _tokenPos++;

        if (CurrentToken.Type != TokenType.EndOfLine && CurrentToken.Type != TokenType.Colon)
        {
            _evaluator.Init(_currentTokens, _tokenPos);
            _evaluator.Evaluate(); // evaluate and discard
            _tokenPos = _evaluator.Position;
        }
    }

// Executes IN# / PR# channel-select statements as compatibility no-ops.
private void ExecuteChannelSelect()
    {
        _tokenPos++; // skip IN# or PR# token

        if (CurrentToken.Type != TokenType.EndOfLine && CurrentToken.Type != TokenType.Colon)
        {
            _evaluator.Init(_currentTokens, _tokenPos);
            _evaluator.Evaluate(); // evaluate and discard
            _tokenPos = _evaluator.Position;
        }
    }

// Executes the RUN statement, starting program execution optionally from a line.
private void ExecuteRun()
    {
        _tokenPos++;
        int startLine = -1;
        if (CurrentToken.Type == TokenType.Number)
        {
            startLine = (int)CurrentToken.NumericValue;
            _tokenPos++;
        }
        Run(startLine);
    }

// Executes the LIST statement, listing program lines.
private void ExecuteList()
    {
        _tokenPos++;
        int start = 0, end = int.MaxValue;
        if (CurrentToken.Type == TokenType.Number)
        {
            start = (int)CurrentToken.NumericValue;
            end = start;
            _tokenPos++;
        }
        if (CurrentToken.Type == TokenType.Comma || CurrentToken.Type == TokenType.Minus)
        {
            _tokenPos++;
            if (CurrentToken.Type == TokenType.Number)
            {
                end = (int)CurrentToken.NumericValue;
                _tokenPos++;
            }
            else
            {
                end = int.MaxValue;
            }
        }
        ListProgram(start, end);
    }

// Executes the SAVE statement, saving the program to the Disk folder.
private void ExecuteSave()
    {
        _tokenPos++;
        if (CurrentToken.Type == TokenType.StringLiteral)
        {
            SaveProgram(CurrentToken.Text);
            _tokenPos++;
        }
        else if (CurrentToken.Type == TokenType.Identifier)
        {
            SaveProgram(CurrentToken.Text);
            _tokenPos++;
        }
        else
            throw new BasicException("?SYNTAX ERROR: FILENAME EXPECTED");
    }

// Executes the LOAD statement, loading a program from the Disk folder.
private void ExecuteLoadCmd()
    {
        _tokenPos++;
        if (CurrentToken.Type == TokenType.StringLiteral)
        {
            LoadProgram(CurrentToken.Text);
            _tokenPos++;
        }
        else if (CurrentToken.Type == TokenType.Identifier)
        {
            LoadProgram(CurrentToken.Text);
            _tokenPos++;
        }
        else
            throw new BasicException("?SYNTAX ERROR: FILENAME EXPECTED");
    }

// Renders one terminal row (covers two LORES pixel rows) using half-block Unicode characters
// and 24-bit ANSI color. termRow 0 covers LORES rows 0-1, termRow 1 covers rows 2-3, etc.
private void RenderLoResRow(int termRow)
{
    try { Console.SetCursorPosition(0, termRow); } catch { return; }
    var sb = new System.Text.StringBuilder(40 * 40);
    for (int x = 0; x < 40; x++)
    {
        int topIdx = _loRes[x, termRow * 2];
        int botIdx = termRow * 2 + 1 < 40 ? _loRes[x, termRow * 2 + 1] : 0;
        var top = LoResColors[topIdx];  // background = top half
        var bot = LoResColors[botIdx];  // foreground = bottom half
        // ▄ (U+2584) lower-half block: fg paints bottom, bg paints top
        sb.Append($"\x1b[38;2;{bot.R};{bot.G};{bot.B}m\x1b[48;2;{top.R};{top.G};{top.B}m\u2584");
    }
    sb.Append("\x1b[0m");
    Console.Write(sb.ToString());
}

// Sets one LORES pixel and immediately redraws the affected terminal row.
private void PlotPixel(int x, int y, int color)
{
    if (x < 0 || x >= 40 || y < 0 || y >= 40) return;
    _loRes[x, y] = (byte)(color & 0xF);
    int savedLeft = Console.CursorLeft, savedTop = Console.CursorTop;
    RenderLoResRow(y / 2);
    try { Console.SetCursorPosition(savedLeft, savedTop); } catch { }
}

// Executes the GR statement: switch to LORES graphics mode.
// Clears the screen and renders a 40x20 terminal block (40x40 LORES pixels)
// with 4 text rows below (rows 20-23), matching Apple II mixed GR mode.
private void ExecuteGr()
{
    _tokenPos++;
    _graphicsMode = true;
    _loRes = new byte[40, 40];
    _loResColor = 0;
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.Clear();
    for (int row = 0; row < 20; row++)
        RenderLoResRow(row);
    try { Console.SetCursorPosition(0, 20); } catch { }
}

// Executes the TEXT statement: return to full text mode.
private void ExecuteText()
{
    _tokenPos++;
    _graphicsMode = false;
    Console.Clear();
}

// Executes the COLOR= statement: set the current drawing color (0-15).
private void ExecuteColor()
{
    _tokenPos++; // skip COLOR
    if (CurrentToken.Type != TokenType.Equal)
        throw new BasicException("?SYNTAX ERROR");
    _tokenPos++; // skip =
    _evaluator.Init(_currentTokens, _tokenPos);
    int color = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;
    _loResColor = Math.Clamp(color, 0, 15);
}

// Executes the PLOT x,y statement: draw a pixel at (x,y) in the current color.
private void ExecutePlot()
{
    _tokenPos++;
    _evaluator.Init(_currentTokens, _tokenPos);
    int x = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (CurrentToken.Type != TokenType.Comma)
        throw new BasicException("?SYNTAX ERROR");
    _tokenPos++;

    _evaluator.Init(_currentTokens, _tokenPos);
    int y = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    PlotPixel(x, y, _loResColor);
}

// Executes HLIN x1,x2 AT y: draw a horizontal line from x1 to x2 at row y.
private void ExecuteHlin()
{
    _tokenPos++;
    _evaluator.Init(_currentTokens, _tokenPos);
    int x1 = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (CurrentToken.Type != TokenType.Comma)
        throw new BasicException("?SYNTAX ERROR");
    _tokenPos++;

    _evaluator.Init(_currentTokens, _tokenPos);
    int x2 = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (CurrentToken.Type != TokenType.AT)
        throw new BasicException("?SYNTAX ERROR: EXPECTED AT");
    _tokenPos++;

    _evaluator.Init(_currentTokens, _tokenPos);
    int y = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (y < 0 || y >= 40) return;
    int savedLeft = Console.CursorLeft, savedTop = Console.CursorTop;
    for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
        if (x >= 0 && x < 40)
            _loRes[x, y] = (byte)(_loResColor & 0xF);
    RenderLoResRow(y / 2);
    try { Console.SetCursorPosition(savedLeft, savedTop); } catch { }
}

// Executes VLIN y1,y2 AT x: draw a vertical line from y1 to y2 at column x.
private void ExecuteVlin()
{
    _tokenPos++;
    _evaluator.Init(_currentTokens, _tokenPos);
    int y1 = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (CurrentToken.Type != TokenType.Comma)
        throw new BasicException("?SYNTAX ERROR");
    _tokenPos++;

    _evaluator.Init(_currentTokens, _tokenPos);
    int y2 = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (CurrentToken.Type != TokenType.AT)
        throw new BasicException("?SYNTAX ERROR: EXPECTED AT");
    _tokenPos++;

    _evaluator.Init(_currentTokens, _tokenPos);
    int x = (int)_evaluator.Evaluate().NumberValue;
    _tokenPos = _evaluator.Position;

    if (x < 0 || x >= 40) return;
    int savedLeft = Console.CursorLeft, savedTop = Console.CursorTop;
    int lo = Math.Max(0, Math.Min(y1, y2));
    int hi = Math.Min(39, Math.Max(y1, y2));
    for (int y = lo; y <= hi; y++)
        _loRes[x, y] = (byte)(_loResColor & 0xF);
    for (int r = lo / 2; r <= hi / 2; r++)
        RenderLoResRow(r);
    try { Console.SetCursorPosition(savedLeft, savedTop); } catch { }
}

// Executes the DEL statement, deleting program lines in a range.
private void ExecuteDel()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int start = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        if (CurrentToken.Type != TokenType.Comma)
            throw new BasicException("?SYNTAX ERROR");
        _tokenPos++;

        _evaluator.Init(_currentTokens, _tokenPos);
        int end = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        DeleteLines(start, end);
    }

    #endregion

    #region Helpers

// Jumps execution to the specified line number.
// lineNumber: The line number to jump to.
private void GotoLine(int lineNumber)
    {
        int idx = _lineNumbers.IndexOf(lineNumber);
        if (idx < 0)
        {
            // Find the exact line
            if (!_program.ContainsKey(lineNumber))
                throw new BasicException("?UNDEF'D STATEMENT ERROR");
            _lineNumbers = new List<int>(_program.Keys);
            idx = _lineNumbers.IndexOf(lineNumber);
        }
        _programIndex = idx;

        // Set up for immediate execution of that line
        _currentLineNumber = lineNumber;
        _currentTokens = _tokenizer.Tokenize(_program[lineNumber]);
        _tokenPos = 0;
        _programIndex = idx + 1;

        ExecuteStatements();

        // After executing the goto target, continue from there
    }

// Collects all DATA values from the program into the _dataValues list.
private void CollectData()
    {
        _dataValues.Clear();
        foreach (var kvp in _program)
        {
            var tokens = _tokenizer.Tokenize(kvp.Value);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type == TokenType.DATA)
                {
                    // Reconstruct raw text after DATA keyword
                    string line = kvp.Value;
                    int dataIdx = line.IndexOf("DATA", StringComparison.OrdinalIgnoreCase);
                    if (dataIdx >= 0)
                    {
                        string rawData = line.Substring(dataIdx + 4);
                        foreach (string item in SplitDataItems(rawData))
                            _dataValues.Add(item);
                    }
                    break;
                }
            }
        }
    }

// Splits a DATA statement's raw text into individual items.
// data: The raw DATA string.
// Returns: An enumerable of DATA items.
private IEnumerable<string> SplitDataItems(string data)
    {
        var items = new List<string>();
        bool inQuote = false;
        string current = "";

        foreach (char c in data)
        {
            if (c == '"') { inQuote = !inQuote; continue; }
            if (c == ',' && !inQuote)
            {
                items.Add(current.Trim());
                current = "";
                continue;
            }
            if (c == ':' && !inQuote) break;
            current += c;
        }
        items.Add(current.Trim());
        return items;
    }

// Ensures that an array exists and is properly dimensioned.
// name: The array name.
// indices: The indices for the array.
private void EnsureArray(string name, List<int> indices)
    {
        if (!_arrays.ContainsKey(name))
        {
            // Auto-dimension with default size 11 (0-10)
            var dims = new int[indices.Count];
            for (int i = 0; i < indices.Count; i++)
                dims[i] = 11;

            int total = 1;
            foreach (int d in dims) total *= d;

            var arr = new BasicValue[total];
            bool isStr = name.EndsWith('$');
            for (int i = 0; i < total; i++)
                arr[i] = isStr ? BasicValue.FromString("") : BasicValue.FromNumber(0);

            _arrays[name] = arr;
            _arrayDimensions[name] = dims;
        }
    }

// Converts multidimensional indices to a flat array index.
// name: The array name.
// indices: The multidimensional indices.
// Returns: The flat array index.
private int GetFlatIndex(string name, List<int> indices)
    {
        var dims = _arrayDimensions[name];
        if (indices.Count != dims.Length)
            throw new BasicException("?BAD SUBSCRIPT ERROR");

        int flat = 0;
        int multiplier = 1;
        for (int i = dims.Length - 1; i >= 0; i--)
        {
            if (indices[i] < 0 || indices[i] >= dims[i])
                throw new BasicException("?BAD SUBSCRIPT ERROR");
            flat += indices[i] * multiplier;
            multiplier *= dims[i];
        }
        return flat;
    }

// Parses a line of text and stores it as a program line if it starts with a number.
// line: The line of text to parse and store.
public void ParseAndStore(string line)
    {
        // Check if line starts with a number
        int i = 0;
        while (i < line.Length && line[i] == ' ') i++;
        if (i < line.Length && char.IsDigit(line[i]))
        {
            int start = i;
            while (i < line.Length && char.IsDigit(line[i])) i++;
            int lineNum = int.Parse(line[start..i]);
            string rest = line[i..].TrimStart();
            StoreLine(lineNum, rest);
        }
    }

    #endregion

}
