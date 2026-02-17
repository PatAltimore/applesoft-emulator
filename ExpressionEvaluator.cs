namespace ApplesoftEmulator;

// Represents a value in the Applesoft BASIC runtime (number or string).
// Represents a value in the Applesoft BASIC runtime, which can be either a number or a string.
public class BasicValue
{
    // Gets the numeric value if this is a number; otherwise, 0.
    public double NumberValue { get; }
    // Gets the string value if this is a string; otherwise, an empty string.
    public string StringValue { get; }
    // Gets a value indicating whether this value is a string.
    public bool IsString { get; }

    // Initializes a new instance of the BasicValue class as a number.
    // number: The numeric value.
    private BasicValue(double number)
    {
        NumberValue = number;
        StringValue = "";
        IsString = false;
    }

    // Initializes a new instance of the BasicValue class as a string.
    // str: The string value.
    private BasicValue(string str)
    {
        NumberValue = 0;
        StringValue = str;
        IsString = true;
    }

    // Creates a BasicValue from a number.
    // n: The numeric value.
    // Returns: A <see cref="BasicValue"/> representing the number.
    public static BasicValue FromNumber(double n) => new(n);
    // Creates a BasicValue from a string.
    // s: The string value.
    // Returns: A <see cref="BasicValue"/> representing the string.
    public static BasicValue FromString(string s) => new(s);

    // Returns the string representation of the value.
    // Returns: The string value or formatted number.
    public override string ToString() => IsString ? StringValue : FormatNumber(NumberValue);

    // Formats a number as Applesoft BASIC would print it.
    // n: The number to format.
    // Returns: The formatted string.
    public static string FormatNumber(double n)
    {
        if (n == 0) return " 0 ";
        // Applesoft prints a leading space for positive numbers
        string prefix = n >= 0 ? " " : "-";
        double abs = Math.Abs(n);

        if (abs == Math.Floor(abs) && abs < 1e10)
            return prefix + ((long)abs).ToString() + " ";

        string formatted = abs.ToString("G9");
        return prefix + formatted + " ";
    }
}

// Recursive-descent expression evaluator for Applesoft BASIC expressions.
// Precedence (low to high): OR, AND, NOT, comparison, +/-, */÷, unary -, ^, functions/atoms
// Recursive-descent expression evaluator for Applesoft BASIC expressions.
// Handles parsing and evaluation of expressions with correct precedence and function support.
public class ExpressionEvaluator
{
    // The list of tokens to evaluate.
    private List<Token> _tokens = new();
    // The current position in the token list.
    private int _pos;
    // Reference to the interpreter for variable/function access.
    private readonly Interpreter _interpreter;

    // Initializes a new instance of the ExpressionEvaluator class.
    // interpreter: The interpreter instance for context.
    public ExpressionEvaluator(Interpreter interpreter)
    {
        _interpreter = interpreter;
    }

    // Initializes the evaluator with a list of tokens and a starting position.
    // tokens: The list of tokens to evaluate.
    // startPos: The starting position in the token list.
    public void Init(List<Token> tokens, int startPos)
    {
        _tokens = tokens;
        _pos = startPos;
    }

    // Gets the current position in the token list after evaluation.
    public int Position => _pos;
    private Token Current => _tokens[_pos];

    // Advances to the next token and returns the current token.
    // Returns: The current token before advancing.
    private Token Advance()
    {
        var t = _tokens[_pos];
        _pos++;
        return t;
    }

    // If the current token matches the given type, advances and returns true.
    // type: The token type to match.
    // Returns: True if matched and advanced; otherwise, false.
    private bool Match(TokenType type)
    {
        if (Current.Type == type)
        {
            _pos++;
            return true;
        }
        return false;
    }

    // Expects the current token to match the given type, otherwise throws a syntax error.
    // type: The expected token type.
    private void Expect(TokenType type)
    {
        if (Current.Type != type)
            throw new BasicException($"?SYNTAX ERROR: EXPECTED {type}, GOT {Current.Type}");
        _pos++;
    }

    // Evaluates the expression starting at the current position.
    // Returns: The result of the evaluated expression.
    public BasicValue Evaluate()
    {
        return ParseOr();
    }

    // Parses and evaluates an OR expression.
    // Returns: The result of the OR expression.
    private BasicValue ParseOr()
    {
        var left = ParseAnd();
        while (Current.Type == TokenType.OR)
        {
            Advance();
            var right = ParseAnd();
            left = BasicValue.FromNumber((left.NumberValue != 0 || right.NumberValue != 0) ? 1 : 0);
        }
        return left;
    }

    // Parses and evaluates an AND expression.
    // Returns: The result of the AND expression.
    private BasicValue ParseAnd()
    {
        var left = ParseNot();
        while (Current.Type == TokenType.AND)
        {
            Advance();
            var right = ParseNot();
            left = BasicValue.FromNumber((left.NumberValue != 0 && right.NumberValue != 0) ? 1 : 0);
        }
        return left;
    }

    // Parses and evaluates a NOT expression.
    // Returns: The result of the NOT expression.
    private BasicValue ParseNot()
    {
        if (Current.Type == TokenType.NOT)
        {
            Advance();
            var val = ParseComparison();
            return BasicValue.FromNumber(val.NumberValue == 0 ? 1 : 0);
        }
        return ParseComparison();
    }

    // Parses and evaluates a comparison expression.
    // Returns: The result of the comparison expression.
    private BasicValue ParseComparison()
    {
        var left = ParseAddSub();

        while (Current.Type is TokenType.Equal or TokenType.NotEqual or TokenType.Less
               or TokenType.Greater or TokenType.LessEqual or TokenType.GreaterEqual)
        {
            var op = Advance();
            var right = ParseAddSub();

            if (left.IsString && right.IsString)
            {
                int cmp = string.Compare(left.StringValue, right.StringValue, StringComparison.Ordinal);
                left = BasicValue.FromNumber(op.Type switch
                {
                    TokenType.Equal => cmp == 0 ? 1 : 0,
                    TokenType.NotEqual => cmp != 0 ? 1 : 0,
                    TokenType.Less => cmp < 0 ? 1 : 0,
                    TokenType.Greater => cmp > 0 ? 1 : 0,
                    TokenType.LessEqual => cmp <= 0 ? 1 : 0,
                    TokenType.GreaterEqual => cmp >= 0 ? 1 : 0,
                    _ => 0
                });
            }
            else
            {
                double l = left.NumberValue, r = right.NumberValue;
                left = BasicValue.FromNumber(op.Type switch
                {
                    TokenType.Equal => l == r ? 1 : 0,
                    TokenType.NotEqual => l != r ? 1 : 0,
                    TokenType.Less => l < r ? 1 : 0,
                    TokenType.Greater => l > r ? 1 : 0,
                    TokenType.LessEqual => l <= r ? 1 : 0,
                    TokenType.GreaterEqual => l >= r ? 1 : 0,
                    _ => 0
                });
            }
        }
        return left;
    }

    // Parses and evaluates an addition or subtraction expression.
    // Returns: The result of the addition or subtraction expression.
    private BasicValue ParseAddSub()
    {
        var left = ParseMulDiv();

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance();
            var right = ParseMulDiv();

            if (op.Type == TokenType.Plus && (left.IsString || right.IsString))
            {
                // String concatenation
                left = BasicValue.FromString(left.ToString() + right.ToString());
            }
            else if (op.Type == TokenType.Plus)
                left = BasicValue.FromNumber(left.NumberValue + right.NumberValue);
            else
                left = BasicValue.FromNumber(left.NumberValue - right.NumberValue);
        }
        return left;
    }

    // Parses and evaluates a multiplication or division expression.
    // Returns: The result of the multiplication or division expression.
    private BasicValue ParseMulDiv()
    {
        var left = ParseUnary();

        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            var op = Advance();
            var right = ParseUnary();

            if (op.Type == TokenType.Star)
                left = BasicValue.FromNumber(left.NumberValue * right.NumberValue);
            else
            {
                if (right.NumberValue == 0)
                    throw new BasicException("?DIVISION BY ZERO ERROR");
                left = BasicValue.FromNumber(left.NumberValue / right.NumberValue);
            }
        }
        return left;
    }

    // Parses and evaluates a unary plus or minus expression.
    // Returns: The result of the unary expression.
    private BasicValue ParseUnary()
    {
        if (Current.Type == TokenType.Minus)
        {
            Advance();
            var val = ParsePower();
            return BasicValue.FromNumber(-val.NumberValue);
        }
        if (Current.Type == TokenType.Plus)
        {
            Advance();
            return ParsePower();
        }
        return ParsePower();
    }

    // Parses and evaluates an exponentiation expression.
    // Returns: The result of the exponentiation expression.
    private BasicValue ParsePower()
    {
        var left = ParseAtom();

        if (Current.Type == TokenType.Caret)
        {
            Advance();
            var right = ParseUnary(); // right-associative
            return BasicValue.FromNumber(Math.Pow(left.NumberValue, right.NumberValue));
        }
        return left;
    }

    // Parses and evaluates an atomic value (number, string, variable, or function call).
    // Returns: The result of the atomic value.
    private BasicValue ParseAtom()
    {
        var tok = Current;

        switch (tok.Type)
        {
            case TokenType.Number:
                Advance();
                return BasicValue.FromNumber(tok.NumericValue);

            case TokenType.StringLiteral:
                Advance();
                return BasicValue.FromString(tok.Text);

            case TokenType.LeftParen:
                Advance();
                var val = Evaluate();
                Expect(TokenType.RightParen);
                return val;

            // Numeric functions
            case TokenType.ABS: return CallNumericFunc1(Math.Abs);
            case TokenType.INT: return CallNumericFunc1(Math.Floor);
            case TokenType.SQR: return CallNumericFunc1(Math.Sqrt);
            case TokenType.SGN: return CallNumericFunc1(v => Math.Sign(v));
            case TokenType.SIN: return CallNumericFunc1(Math.Sin);
            case TokenType.COS: return CallNumericFunc1(Math.Cos);
            case TokenType.TAN: return CallNumericFunc1(Math.Tan);
            case TokenType.ATN: return CallNumericFunc1(Math.Atan);
            case TokenType.LOG: return CallNumericFunc1(Math.Log);
            case TokenType.EXP: return CallNumericFunc1(Math.Exp);

            case TokenType.RND:
                Advance();
                Expect(TokenType.LeftParen);
                var rndArg = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromNumber(_interpreter.GetRandom(rndArg.NumberValue));

            case TokenType.PEEK:
                Advance();
                Expect(TokenType.LeftParen);
                var addr = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromNumber(_interpreter.Peek((int)addr.NumberValue));

            case TokenType.POS:
                Advance();
                Expect(TokenType.LeftParen);
                Evaluate(); // argument is ignored in real Applesoft
                Expect(TokenType.RightParen);
                return BasicValue.FromNumber(Console.CursorLeft);

            // String functions
            case TokenType.LEN:
                Advance();
                Expect(TokenType.LeftParen);
                var lenVal = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromNumber(lenVal.StringValue.Length);

            case TokenType.VAL:
                Advance();
                Expect(TokenType.LeftParen);
                var valStr = Evaluate();
                Expect(TokenType.RightParen);
                double.TryParse(valStr.StringValue.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed);
                return BasicValue.FromNumber(parsed);

            case TokenType.STR:
                Advance();
                Expect(TokenType.LeftParen);
                var strVal = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromString(BasicValue.FormatNumber(strVal.NumberValue).Trim());

            case TokenType.CHR:
                Advance();
                Expect(TokenType.LeftParen);
                var chrVal = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromString(((char)(int)chrVal.NumberValue).ToString());

            case TokenType.ASC:
                Advance();
                Expect(TokenType.LeftParen);
                var ascVal = Evaluate();
                Expect(TokenType.RightParen);
                if (ascVal.StringValue.Length == 0)
                    throw new BasicException("?ILLEGAL QUANTITY ERROR");
                return BasicValue.FromNumber((int)ascVal.StringValue[0]);

            case TokenType.LEFT:
                return CallStringFunc2((s, n) => s.Substring(0, Math.Min((int)n, s.Length)));

            case TokenType.RIGHT:
                return CallStringFunc2((s, n) =>
                {
                    int count = Math.Min((int)n, s.Length);
                    return s.Substring(s.Length - count);
                });

            case TokenType.MID:
                Advance();
                Expect(TokenType.LeftParen);
                var midStr = Evaluate();
                Expect(TokenType.Comma);
                var midStart = Evaluate();
                int midLen = midStr.StringValue.Length;
                if (Current.Type == TokenType.Comma)
                {
                    Advance();
                    midLen = (int)Evaluate().NumberValue;
                }
                Expect(TokenType.RightParen);
                int start = Math.Max(0, (int)midStart.NumberValue - 1);
                string s2 = midStr.StringValue;
                if (start >= s2.Length) return BasicValue.FromString("");
                return BasicValue.FromString(s2.Substring(start, Math.Min(midLen, s2.Length - start)));

            case TokenType.TAB:
                Advance();
                Expect(TokenType.LeftParen);
                var tabVal = Evaluate();
                Expect(TokenType.RightParen);
                int tabPos = Math.Max(0, (int)tabVal.NumberValue - 1);
                int spaces = Math.Max(0, tabPos - Console.CursorLeft);
                return BasicValue.FromString(new string(' ', spaces));

            case TokenType.SPC:
                Advance();
                Expect(TokenType.LeftParen);
                var spcVal = Evaluate();
                Expect(TokenType.RightParen);
                return BasicValue.FromString(new string(' ', Math.Max(0, (int)spcVal.NumberValue)));

            case TokenType.FN:
                return CallUserFunction();

            case TokenType.Identifier:
                return ReadVariable();

            default:
                throw new BasicException($"?SYNTAX ERROR: UNEXPECTED {tok.Type}");
        }
    }

    // Calls a numeric function with one argument.
    // func: The function to call.
    // Returns: The result as a <see cref="BasicValue"/>.
    private BasicValue CallNumericFunc1(Func<double, double> func)
    {
        Advance();
        Expect(TokenType.LeftParen);
        var arg = Evaluate();
        Expect(TokenType.RightParen);
        return BasicValue.FromNumber(func(arg.NumberValue));
    }

    // Calls a string function with a string and numeric argument.
    // func: The function to call.
    // Returns: The result as a <see cref="BasicValue"/>.
    private BasicValue CallStringFunc2(Func<string, double, string> func)
    {
        Advance();
        Expect(TokenType.LeftParen);
        var str = Evaluate();
        Expect(TokenType.Comma);
        var num = Evaluate();
        Expect(TokenType.RightParen);
        return BasicValue.FromString(func(str.StringValue, num.NumberValue));
    }

    // Calls a user-defined function (DEF FN).
    // Returns: The result of the user function.
    private BasicValue CallUserFunction()
    {
        Advance(); // skip FN
        var name = Current.Text;
        Advance();
        Expect(TokenType.LeftParen);
        var arg = Evaluate();
        Expect(TokenType.RightParen);
        return _interpreter.CallUserFunction(name, arg);
    }

    // Reads the value of a variable or array element.
    // Returns: The value of the variable or array element.
    private BasicValue ReadVariable()
    {
        string name = Current.Text;
        Advance();

        bool isString = name.EndsWith('$');

        // Check for array access
        if (Current.Type == TokenType.LeftParen)
        {
            Advance();
            var indices = new List<int>();
            indices.Add((int)Evaluate().NumberValue);
            while (Current.Type == TokenType.Comma)
            {
                Advance();
                indices.Add((int)Evaluate().NumberValue);
            }
            Expect(TokenType.RightParen);
            return _interpreter.GetArrayValue(name, indices);
        }

        return _interpreter.GetVariable(name);
    }
}
