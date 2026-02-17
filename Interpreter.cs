namespace ApplesoftEmulator;

public class BasicException : Exception
{
    public BasicException(string message) : base(message) { }
}

public class StopException : Exception
{
    public int LineNumber { get; }
    public StopException(int lineNumber) : base($"BREAK IN {lineNumber}") { LineNumber = lineNumber; }
}

/// <summary>
/// FOR/NEXT loop state
/// </summary>
public class ForState
{
    public string Variable { get; set; } = "";
    public double Limit { get; set; }
    public double StepValue { get; set; }
    public int LineNumber { get; set; }
    public int TokenPosition { get; set; }
    public int ProgramIndex { get; set; }
}

/// <summary>
/// User-defined function (DEF FN)
/// </summary>
public class UserFunction
{
    public string ParamName { get; set; } = "";
    public List<Token> BodyTokens { get; set; } = new();
}

/// <summary>
/// The main Applesoft BASIC interpreter.
/// </summary>
public class Interpreter
{
    private readonly SortedDictionary<int, string> _program = new();
    private readonly Dictionary<string, BasicValue> _variables = new();
    private readonly Dictionary<string, BasicValue[]> _arrays = new();
    private readonly Dictionary<string, int[]> _arrayDimensions = new();
    private readonly Stack<ForState> _forStack = new();
    private readonly Stack<(int lineNumber, int tokenPos, int progIdx)> _gosubStack = new();
    private readonly Dictionary<string, UserFunction> _userFunctions = new();
    private readonly List<string> _dataValues = new();
    private int _dataPointer;
    private Random _random = new();
    private readonly Tokenizer _tokenizer = new();
    private readonly ExpressionEvaluator _evaluator;

    private List<Token> _currentTokens = new();
    private int _tokenPos;
    private int _currentLineNumber;
    private bool _running;
    private List<int> _lineNumbers = new();
    private int _programIndex;

    // Memory simulation for PEEK/POKE
    private readonly byte[] _memory = new byte[65536];

    public Interpreter()
    {
        _evaluator = new ExpressionEvaluator(this);
    }

    #region Public API

    public void StoreLine(int lineNumber, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            _program.Remove(lineNumber);
        else
            _program[lineNumber] = text;
    }

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

    public void ListProgram(int startLine = 0, int endLine = int.MaxValue)
    {
        foreach (var kvp in _program)
        {
            if (kvp.Key >= startLine && kvp.Key <= endLine)
                Console.WriteLine($"{kvp.Key}  {kvp.Value}");
        }
    }

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

    public void SaveProgram(string filename)
    {
        using var writer = new StreamWriter(filename);
        foreach (var kvp in _program)
            writer.WriteLine($"{kvp.Key} {kvp.Value}");
        Console.WriteLine($"SAVED {filename}");
    }

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

    public void DeleteLines(int start, int end)
    {
        var toRemove = _program.Keys.Where(k => k >= start && k <= end).ToList();
        foreach (var key in toRemove)
            _program.Remove(key);
    }

    public bool HasProgram => _program.Count > 0;

    #endregion

    #region Variable Access (used by ExpressionEvaluator)

    public BasicValue GetVariable(string name)
    {
        if (_variables.TryGetValue(name.ToUpper(), out var val))
            return val;
        // Default: 0 for numeric, "" for string
        return name.EndsWith('$') ? BasicValue.FromString("") : BasicValue.FromNumber(0);
    }

    public void SetVariable(string name, BasicValue value)
    {
        _variables[name.ToUpper()] = value;
    }

    public BasicValue GetArrayValue(string name, List<int> indices)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        return _arrays[key][flatIndex];
    }

    public void SetArrayValue(string name, List<int> indices, BasicValue value)
    {
        string key = name.ToUpper();
        EnsureArray(key, indices);
        int flatIndex = GetFlatIndex(key, indices);
        _arrays[key][flatIndex] = value;
    }

    public double GetRandom(double arg)
    {
        if (arg < 0) _random = new Random(unchecked((int)(arg * int.MaxValue)));
        return _random.NextDouble();
    }

    public int Peek(int address)
    {
        if (address >= 0 && address < 65536)
            return _memory[address];
        return 0;
    }

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

    private Token CurrentToken => _currentTokens[_tokenPos];

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

    private void ExecuteGoto()
    {
        _tokenPos++; // skip GOTO
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        GotoLine(lineNum);
    }

    private void ExecuteGosub()
    {
        _tokenPos++; // skip GOSUB
        _evaluator.Init(_currentTokens, _tokenPos);
        int lineNum = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;

        _gosubStack.Push((_currentLineNumber, _tokenPos, _programIndex));
        GotoLine(lineNum);
    }

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

    private void ExecuteHtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int col = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorLeft = Math.Max(0, col - 1); } catch { }
    }

    private void ExecuteVtab()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        int row = (int)_evaluator.Evaluate().NumberValue;
        _tokenPos = _evaluator.Position;
        try { Console.CursorTop = Math.Max(0, row - 1); } catch { }
    }

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

    private void ExecuteCall()
    {
        _tokenPos++;
        _evaluator.Init(_currentTokens, _tokenPos);
        _evaluator.Evaluate(); // evaluate and discard - no real machine code to run
        _tokenPos = _evaluator.Position;
    }

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
