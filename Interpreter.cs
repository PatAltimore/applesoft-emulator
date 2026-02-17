namespace ApplesoftEmulator;

/// <summary>
/// Represents errors that occur during Applesoft BASIC interpretation.
/// </summary>
public class BasicException : Exception
{
    /// <summary>
/// Initializes a new instance of the <see cref="BasicException"/> class with a specified error message.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public BasicException(string message) : base(message) { }
}

/// <summary>
/// Represents a STOP or BREAK event in Applesoft BASIC execution.
/// </summary>
public class StopException : Exception
{
    /// <summary>
/// Gets the line number where the STOP or BREAK occurred.
/// </summary>
public int LineNumber { get; }
    /// <summary>
/// Initializes a new instance of the <see cref="StopException"/> class for a specific line number.
/// </summary>
/// <param name="lineNumber">The line number where execution stopped.</param>
public StopException(int lineNumber) : base($"BREAK IN {lineNumber}") { LineNumber = lineNumber; }
}

// FOR/NEXT loop state
public class ForState
{
    /// <summary>
/// Gets or sets the name of the loop variable.
/// </summary>
public string Variable { get; set; } = "";
    /// <summary>
/// Gets or sets the limit value for the loop.
/// </summary>
public double Limit { get; set; }
    /// <summary>
/// Gets or sets the step value for the loop.
/// </summary>
public double StepValue { get; set; }
    /// <summary>
/// Gets or sets the line number where the FOR statement appears.
/// </summary>
public int LineNumber { get; set; }
    /// <summary>
/// Gets or sets the token position in the line for the FOR statement.
/// </summary>
public int TokenPosition { get; set; }
    /// <summary>
/// Gets or sets the program index for the FOR statement.
/// </summary>
public int ProgramIndex { get; set; }
}

// User-defined function (DEF FN)
public class UserFunction
{
    /// <summary>
/// Gets or sets the parameter name for the user-defined function.
/// </summary>
public string ParamName { get; set; } = "";
    /// <summary>
/// Gets or sets the list of tokens representing the function body.
/// </summary>
public List<Token> BodyTokens { get; set; } = new();
}

// The main Applesoft BASIC interpreter.
public class Interpreter
{
    /// <summary>
/// Stores the program lines, keyed by line number.
/// </summary>
private readonly SortedDictionary<int, string> _program = new();
    /// <summary>
/// Stores scalar variables by name.
/// </summary>
private readonly Dictionary<string, BasicValue> _variables = new();
    /// <summary>
/// Stores array variables by name.
/// </summary>
private readonly Dictionary<string, BasicValue[]> _arrays = new();
    /// <summary>
/// Stores array dimensions by array name.
/// </summary>
private readonly Dictionary<string, int[]> _arrayDimensions = new();
    /// <summary>
/// Stack for managing nested FOR/NEXT loops.
/// </summary>
private readonly Stack<ForState> _forStack = new();
    /// <summary>
/// Stack for managing GOSUB/RETURN calls.
/// </summary>
private readonly Stack<(int lineNumber, int tokenPos, int progIdx)> _gosubStack = new();
    /// <summary>
/// Stores user-defined functions (DEF FN) by name.
/// </summary>
private readonly Dictionary<string, UserFunction> _userFunctions = new();
    /// <summary>
/// Stores DATA values collected from the program.
/// </summary>
private readonly List<string> _dataValues = new();
    /// <summary>
/// Points to the next DATA value to be READ.
/// </summary>
private int _dataPointer;
    /// <summary>
/// Random number generator for RND function.
/// </summary>
private Random _random = new();
    /// <summary>
/// Tokenizer for parsing program lines and statements.
/// </summary>
private readonly Tokenizer _tokenizer = new();
    /// <summary>
/// Evaluator for parsing and evaluating expressions.
/// </summary>
private readonly ExpressionEvaluator _evaluator;

    /// <summary>
/// The current list of tokens being executed.
/// </summary>
private List<Token> _currentTokens = new();
    /// <summary>
/// The current token position in the token list.
/// </summary>
private int _tokenPos;
    /// <summary>
/// The current line number being executed.
/// </summary>
private int _currentLineNumber;
    /// <summary>
/// Indicates whether the interpreter is currently running a program.
/// </summary>
private bool _running;
    /// <summary>
/// List of line numbers in the current program.
/// </summary>
private List<int> _lineNumbers = new();
    /// <summary>
/// The current index in the line number list.
/// </summary>
private int _programIndex;

    // Memory simulation for PEEK/POKE
    /// <summary>
/// Simulated memory for PEEK and POKE operations.
/// </summary>
private readonly byte[] _memory = new byte[65536];

    /// <summary>
/// Initializes a new instance of the <see cref="Interpreter"/> class.
/// </summary>
public Interpreter()
    {
        _evaluator = new ExpressionEvaluator(this);
    }

    #region Public API

    /// <summary>
/// Stores or removes a program line.
/// </summary>
/// <param name="lineNumber">The line number to store or remove.</param>
/// <param name="text">The program text. If empty, the line is removed.</param>
public void StoreLine(int lineNumber, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            _program.Remove(lineNumber);
        else
            _program[lineNumber] = text;
    }

    /// <summary>
/// Runs the stored Applesoft BASIC program.
/// </summary>
/// <param name="startLine">The line number to start execution from, or -1 for the first line.</param>
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

    /// <summary>
/// Executes a single line of Applesoft BASIC code directly (immediate mode).
/// </summary>
/// <param name="line">The line of code to execute.</param>
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

    /// <summary>
/// Lists the program lines between the specified start and end line numbers.
/// </summary>
/// <param name="startLine">The starting line number.</param>
/// <param name="endLine">The ending line number.</param>
public void ListProgram(int startLine = 0, int endLine = int.MaxValue)
    {
        foreach (var kvp in _program)
        {
            if (kvp.Key >= startLine && kvp.Key <= endLine)
                Console.WriteLine($"{kvp.Key}  {kvp.Value}");
        }
    }

    /// <summary>
/// Clears the current program and all variables.
/// </summary>
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

    /// <summary>
/// Saves the current program to a file.
/// </summary>
/// <param name="filename">The file to save the program to.</param>
public void SaveProgram(string filename)
    {
        using var writer = new StreamWriter(filename);
        foreach (var kvp in _program)
            writer.WriteLine($"{kvp.Key} {kvp.Value}");
        Console.WriteLine($"SAVED {filename}");
    }

    /// <summary>
/// Loads a program from a file, replacing the current program.
/// </summary>
/// <param name="filename">The file to load the program from.</param>
public void LoadProgram(string filename)
    {
        if (!File.Exists(filename))
        {
            Console.WriteLine("?FILE NOT FOUND");
            return;
        }
        NewProgram();
        foreach (string line in File.ReadAllLines(filename))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ParseAndStore(line);
        }
        Console.WriteLine($"LOADED {filename}");
    }

    /// <summary>
/// Deletes program lines between the specified start and end line numbers.
/// </summary>
/// <param name="start">The starting line number.</param>
/// <param name="end">The ending line number.</param>
public void DeleteLines(int start, int end)
    {
        var toRemove = _program.Keys.Where(k => k >= start && k <= end).ToList();
        foreach (var key in toRemove)
            _program.Remove(key);
    }

    /// <summary>
/// Gets a value indicating whether a program is currently loaded.
/// </summary>
public bool HasProgram => _program.Count > 0;

    #endregion

    #region Variable Access (used by ExpressionEvaluator)

    /// <summary>
/// Gets the value of a scalar variable.
/// </summary>
/// <param name="name">The variable name.</param>
/// <returns>The value of the variable, or a default value if not set.</returns>
public BasicValue GetVariable(string name)
    {
        if (_variables.TryGetValue(name.ToUpper(), out var val))
            return val;
        // Default: 0 for numeric, "" for string
        return name.EndsWith('$') ? BasicValue.FromString("") : BasicValue.FromNumber(0);
    }

    /// <summary>
/// Sets the value of a scalar variable.
/// </summary>
/// <param name="name">The variable name.</param>
/// <param name="value">The value to set.</param>
public void SetVariable(string name, BasicValue value)
    {
        _variables[name.ToUpper()] = value;
    }

    /// <summary>
/// Gets the value of an array element.
/// </summary>
/// <param name="name">The array name.</param>
/// <param name="indices">The indices of the element.</param>
/// <returns>The value of the array element.</returns>
public BasicValue GetArrayValue(string name, List<int> indices)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        return _arrays[key][flatIndex];
    }

    /// <summary>
/// Sets the value of an array element.
/// </summary>
/// <param name="name">The array name.</param>
/// <param name="indices">The indices of the element.</param>
/// <param name="value">The value to set.</param>
public void SetArrayValue(string name, List<int> indices, BasicValue value)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        _arrays[key][flatIndex] = value;
    }

    /// <summary>
/// Returns a pseudo-random number between 0 and 1.
/// </summary>
/// <param name="arg">If negative, seeds the generator; otherwise, returns a random value.</param>
/// <returns>A random double between 0 and 1.</returns>
public double GetRandom(double arg)
    {
        if (arg < 0) _random = new Random(unchecked((int)(arg * int.MaxValue)));
        return _random.NextDouble();
    }

    /// <summary>
/// Returns the value at the specified memory address.
/// </summary>
/// <param name="address">The memory address to peek.</param>
/// <returns>The byte value at the address, or 0 if out of range.</returns>
public int Peek(int address)
    {
        if (address >= 0 && address < 65536)
            return _memory[address];
        return 0;
    }

    /// <summary>
/// Calls a user-defined function (DEF FN) with the specified argument.
/// </summary>
/// <param name="name">The function name.</param>
/// <param name="arg">The argument to pass to the function.</param>
/// <returns>The result of the function call.</returns>
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

    /// <summary>
/// Executes all statements in the current line.
/// </summary>
private void ExecuteStatements()
    {
        while (_running && _tokenPos < _currentTokens.Count && _currentTokens[_tokenPos].Type != TokenType.EndOfLine)
        {
            ExecuteStatement();

            // Handle colon-separated statements
            if (_tokenPos < _currentTokens.Count && _currentTokens[_tokenPos].Type == TokenType.Colon)
            {
                _tokenPos++;
                continue;
            }
            break;
        }
    }

    /// <summary>
/// Gets the current token being processed.
/// </summary>
private Token CurrentToken => _currentTokens[_tokenPos];

    /// <summary>
/// Executes a single BASIC statement at the current token position.
/// </summary>
private void ExecuteStatement()
    {
        var tok = CurrentToken;

        switch (tok.Type)
        {
            case TokenType.PRINT: ExecutePrint(); break;
            case TokenType.INPUT: ExecuteInput(); break;
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
            case TokenType.HOME: Console.Clear(); _tokenPos++; break;
            case TokenType.HTAB: ExecuteHtab(); break;
            case TokenType.VTAB: ExecuteVtab(); break;
            case TokenType.POKE: ExecutePoke(); break;
            case TokenType.CALL: ExecuteCall(); break;
            case TokenType.RUN: ExecuteRun(); break;
            case TokenType.LIST: ExecuteList(); break;
            case TokenType.NEW: NewProgram(); _tokenPos++; break;
            case TokenType.SAVE: ExecuteSave(); break;
            case TokenType.LOAD: ExecuteLoadCmd(); break;
            case TokenType.DEL: ExecuteDel(); break;
            case TokenType.Identifier:
                // Implicit LET
                ExecuteLet();
                break;
            default:
                throw new BasicException($"?SYNTAX ERROR: UNEXPECTED {tok.Type}");
        }
    }

    /// <summary>
/// Executes the PRINT statement.
/// </summary>
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

    /// <summary>
/// Executes the INPUT statement.
/// </summary>
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

        // Collect variable names
        var varNames = new List<string>();
        varNames.Add(CurrentToken.Text);
        _tokenPos++;
        while (CurrentToken.Type == TokenType.Comma)
        {
            _tokenPos++;
            varNames.Add(CurrentToken.Text);
            _tokenPos++;
        }

        bool done = false;
        while (!done)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine() ?? "";
            string[] parts = input.Split(',');

            if (parts.Length < varNames.Count)
            {
                // Not enough values, ask again for remaining
                for (int i = 0; i < Math.Min(parts.Length, varNames.Count); i++)
                    AssignInputValue(varNames[i], parts[i].Trim());

                if (parts.Length < varNames.Count)
                {
                    Console.Write("?? ");
                    string? more = Console.ReadLine() ?? "";
                    var remaining = more.Split(',');
                    for (int i = 0; i < Math.Min(remaining.Length, varNames.Count - parts.Length); i++)
                        AssignInputValue(varNames[parts.Length + i], remaining[i].Trim());
                }
                done = true;
            }
            else
            {
                for (int i = 0; i < varNames.Count; i++)
                    AssignInputValue(varNames[i], parts[i].Trim());
                done = true;
            }
        }
    }

    /// <summary>
/// Assigns a value to a variable as a result of INPUT.
/// </summary>
/// <param name="varName">The variable name.</param>
/// <param name="value">The value to assign.</param>
private void AssignInputValue(string varName, string value)
    {
        if (varName.EndsWith('$'))
            SetVariable(varName, BasicValue.FromString(value));
        else
        {
            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double num))
                SetVariable(varName, BasicValue.FromNumber(num));
            else
                SetVariable(varName, BasicValue.FromNumber(0));
        }
    }

    /// <summary>
/// Executes a LET or implicit assignment statement.
/// </summary>
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

    /// <summary>
/// Executes the IF...THEN statement.
/// </summary>
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

    /// <summary>
/// Executes the GOTO statement.
/// </summary>
private void ExecuteGoto()
    {
        _tokenPos++; // skip GOTO
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        GotoLine(lineNum);
    }

    /// <summary>
/// Executes the GOSUB statement.
/// </summary>
private void ExecuteGosub()
    {
        _tokenPos++; // skip GOSUB
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        _gosubStack.Push((_currentLineNumber, _tokenPos, _programIndex));
        GotoLine(lineNum);
    }

    /// <summary>
/// Executes the RETURN statement.
/// </summary>
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

    /// <summary>
/// Executes the FOR statement, initializing a FOR/NEXT loop.
/// </summary>
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

        _forStack.Push(new ForState
        {
            Variable = varName.ToUpper(),
            Limit = endVal.NumberValue,
            StepValue = step,
            LineNumber = _currentLineNumber,
            TokenPosition = _tokenPos,
            ProgramIndex = _programIndex
        });
    }

    /// <summary>
/// Executes the NEXT statement, advancing a FOR/NEXT loop.
/// </summary>
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

        bool done = forState.StepValue > 0
            ? currentVal > forState.Limit
            : currentVal < forState.Limit;

        if (!done)
        {
            // Loop back
            _programIndex = forState.ProgramIndex;
        }
        else
        {
            // Done with loop
            _forStack.Pop();
        }
    }

    /// <summary>
/// Executes the DIM statement, defining array dimensions.
/// </summary>
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

    /// <summary>
/// Executes the READ statement, reading values from DATA.
/// </summary>
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

    /// <summary>
/// Executes the DEF FN statement, defining a user function.
/// </summary>
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

    /// <summary>
/// Executes the ON...GOTO/GOSUB statement.
/// </summary>
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

    /// <summary>
/// Executes the HTAB statement, setting the horizontal cursor position.
/// </summary>
private void ExecuteHtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int col = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorLeft = Math.Clamp(col - 1, 0, Console.BufferWidth - 1); } catch { }
    }

    /// <summary>
/// Executes the VTAB statement, setting the vertical cursor position.
/// </summary>
private void ExecuteVtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int row = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorTop = Math.Clamp(row - 1, 0, Console.BufferHeight - 1); } catch { }
    }

    /// <summary>
/// Executes the POKE statement, writing a value to memory.
/// </summary>
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

    /// <summary>
/// Executes the CALL statement (no-op in this emulator).
/// </summary>
private void ExecuteCall()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        _evaluator.Evaluate(); // evaluate and discard - no real machine code to run
        _tokenPos = _evaluator.Position;
    }

    /// <summary>
/// Executes the RUN statement, starting program execution optionally from a line.
/// </summary>
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

    /// <summary>
/// Executes the LIST statement, listing program lines.
/// </summary>
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

    /// <summary>
/// Executes the SAVE statement, saving the program to a file.
/// </summary>
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
            SaveProgram(CurrentToken.Text + ".bas");
            _tokenPos++;
        }
        else
            throw new BasicException("?SYNTAX ERROR: FILENAME EXPECTED");
    }

    /// <summary>
/// Executes the LOAD statement, loading a program from a file.
/// </summary>
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
            LoadProgram(CurrentToken.Text + ".bas");
            _tokenPos++;
        }
        else
            throw new BasicException("?SYNTAX ERROR: FILENAME EXPECTED");
    }

    /// <summary>
/// Executes the DEL statement, deleting program lines in a range.
/// </summary>
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

    /// <summary>
/// Jumps execution to the specified line number.
/// </summary>
/// <param name="lineNumber">The line number to jump to.</param>
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

    /// <summary>
/// Collects all DATA values from the program into the _dataValues list.
/// </summary>
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

    /// <summary>
/// Splits a DATA statement's raw text into individual items.
/// </summary>
/// <param name="data">The raw DATA string.</param>
/// <returns>An enumerable of DATA items.</returns>
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

    /// <summary>
/// Ensures that an array exists and is properly dimensioned.
/// </summary>
/// <param name="name">The array name.</param>
/// <param name="indices">The indices for the array.</param>
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

    /// <summary>
/// Converts multidimensional indices to a flat array index.
/// </summary>
/// <param name="name">The array name.</param>
/// <param name="indices">The multidimensional indices.</param>
/// <returns>The flat array index.</returns>
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

    /// <summary>
/// Parses a line of text and stores it as a program line if it starts with a number.
/// </summary>
/// <param name="line">The line of text to parse and store.</param>
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
