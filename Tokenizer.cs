namespace ApplesoftEmulator;

public enum TokenType
{
    // Literals
    Number,
    StringLiteral,
    Identifier,

    // Operators
    Plus, Minus, Star, Slash, Caret,
    Equal, NotEqual, Less, Greater, LessEqual, GreaterEqual,
    LeftParen, RightParen, Comma, Semicolon, Colon,

    // Keywords
    PRINT, INPUT, LET, IF, THEN, GOTO, GOSUB, RETURN,
    FOR, TO, STEP, NEXT,
    REM, END, STOP, DIM, NEW, RUN, LIST, SAVE, LOAD, DEL,
    AND, OR, NOT,
    DATA, READ, RESTORE,
    DEF, FN,
    ON,
    HOME, HTAB, VTAB,
    TAB, SPC,
    PEEK, POKE, CALL,

    // Built-in functions
    ABS, INT, SQR, RND, SGN, SIN, COS, TAN, ATN, LOG, EXP,
    LEN, VAL, STR, CHR, ASC, LEFT, RIGHT, MID, POS,

    // Special
    EndOfLine,
    Dollar,
}

public class Token
{
    public TokenType Type { get; }
    public string Text { get; }
    public double NumericValue { get; }

    public Token(TokenType type, string text, double numericValue = 0)
    {
        Type = type;
        Text = text;
        NumericValue = numericValue;
    }

    public override string ToString() => $"{Type}({Text})";
}

public class Tokenizer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PRINT"] = TokenType.PRINT, ["?"] = TokenType.PRINT,
        ["INPUT"] = TokenType.INPUT, ["LET"] = TokenType.LET,
        ["IF"] = TokenType.IF, ["THEN"] = TokenType.THEN,
        ["GOTO"] = TokenType.GOTO, ["GOSUB"] = TokenType.GOSUB,
        ["RETURN"] = TokenType.RETURN,
        ["FOR"] = TokenType.FOR, ["TO"] = TokenType.TO, ["STEP"] = TokenType.STEP,
        ["NEXT"] = TokenType.NEXT,
        ["REM"] = TokenType.REM, ["END"] = TokenType.END, ["STOP"] = TokenType.STOP,
        ["DIM"] = TokenType.DIM, ["NEW"] = TokenType.NEW,
        ["RUN"] = TokenType.RUN, ["LIST"] = TokenType.LIST,
        ["SAVE"] = TokenType.SAVE, ["LOAD"] = TokenType.LOAD, ["DEL"] = TokenType.DEL,
        ["AND"] = TokenType.AND, ["OR"] = TokenType.OR, ["NOT"] = TokenType.NOT,
        ["DATA"] = TokenType.DATA, ["READ"] = TokenType.READ, ["RESTORE"] = TokenType.RESTORE,
        ["DEF"] = TokenType.DEF, ["FN"] = TokenType.FN,
        ["ON"] = TokenType.ON,
        ["HOME"] = TokenType.HOME, ["HTAB"] = TokenType.HTAB, ["VTAB"] = TokenType.VTAB,
        ["TAB"] = TokenType.TAB, ["SPC"] = TokenType.SPC,
        ["PEEK"] = TokenType.PEEK, ["POKE"] = TokenType.POKE, ["CALL"] = TokenType.CALL,
        ["ABS"] = TokenType.ABS, ["INT"] = TokenType.INT, ["SQR"] = TokenType.SQR,
        ["RND"] = TokenType.RND, ["SGN"] = TokenType.SGN,
        ["SIN"] = TokenType.SIN, ["COS"] = TokenType.COS, ["TAN"] = TokenType.TAN,
        ["ATN"] = TokenType.ATN, ["LOG"] = TokenType.LOG, ["EXP"] = TokenType.EXP,
        ["LEN"] = TokenType.LEN, ["VAL"] = TokenType.VAL,
        ["STR$"] = TokenType.STR, ["CHR$"] = TokenType.CHR, ["ASC"] = TokenType.ASC,
        ["LEFT$"] = TokenType.LEFT, ["RIGHT$"] = TokenType.RIGHT, ["MID$"] = TokenType.MID,
        ["POS"] = TokenType.POS,
    };

    private string _input = "";
    private int _pos;

    public List<Token> Tokenize(string input)
    {
        _input = input;
        _pos = 0;
        var tokens = new List<Token>();

        while (_pos < _input.Length)
        {
            SkipSpaces();
            if (_pos >= _input.Length) break;

            char c = _input[_pos];

            if (char.IsDigit(c) || (c == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber());
            }
            else if (c == '"')
            {
                tokens.Add(ReadString());
            }
            else if (char.IsLetter(c))
            {
                tokens.Add(ReadIdentifierOrKeyword());
            }
            else if (c == '?')
            {
                tokens.Add(new Token(TokenType.PRINT, "?"));
                _pos++;
            }
            else
            {
                tokens.Add(ReadOperator());
            }
        }

        tokens.Add(new Token(TokenType.EndOfLine, ""));
        return tokens;
    }

    private void SkipSpaces()
    {
        while (_pos < _input.Length && _input[_pos] == ' ')
            _pos++;
    }

    private Token ReadNumber()
    {
        int start = _pos;
        while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
            _pos++;

        // Handle scientific notation
        if (_pos < _input.Length && (_input[_pos] == 'E' || _input[_pos] == 'e'))
        {
            _pos++;
            if (_pos < _input.Length && (_input[_pos] == '+' || _input[_pos] == '-'))
                _pos++;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
                _pos++;
        }

        string text = _input[start.._pos];
        double value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        return new Token(TokenType.Number, text, value);
    }

    private Token ReadString()
    {
        _pos++; // skip opening quote
        int start = _pos;
        while (_pos < _input.Length && _input[_pos] != '"')
            _pos++;
        string text = _input[start.._pos];
        if (_pos < _input.Length) _pos++; // skip closing quote
        return new Token(TokenType.StringLiteral, text);
    }

    private Token ReadIdentifierOrKeyword()
    {
        int start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '$'))
        {
            if (_input[_pos] == '$')
            {
                _pos++;
                break; // $ is always end of identifier
            }
            _pos++;
        }

        string text = _input[start.._pos];

        // Check for keywords (try with $ first for STR$, CHR$, etc.)
        if (Keywords.TryGetValue(text, out var kwType))
            return new Token(kwType, text);

        // Check for FN prefix
        if (text.Equals("FN", StringComparison.OrdinalIgnoreCase))
            return new Token(TokenType.FN, text);

        return new Token(TokenType.Identifier, text);
    }

    private Token ReadOperator()
    {
        char c = _input[_pos++];
        return c switch
        {
            '+' => new Token(TokenType.Plus, "+"),
            '-' => new Token(TokenType.Minus, "-"),
            '*' => new Token(TokenType.Star, "*"),
            '/' => new Token(TokenType.Slash, "/"),
            '^' => new Token(TokenType.Caret, "^"),
            '(' => new Token(TokenType.LeftParen, "("),
            ')' => new Token(TokenType.RightParen, ")"),
            ',' => new Token(TokenType.Comma, ","),
            ';' => new Token(TokenType.Semicolon, ";"),
            ':' => new Token(TokenType.Colon, ":"),
            '=' => new Token(TokenType.Equal, "="),
            '<' => PeekAndAdvance('=') ? new Token(TokenType.LessEqual, "<=") :
                   PeekAndAdvance('>') ? new Token(TokenType.NotEqual, "<>") :
                   new Token(TokenType.Less, "<"),
            '>' => PeekAndAdvance('=') ? new Token(TokenType.GreaterEqual, ">=") :
                   new Token(TokenType.Greater, ">"),
            _ => throw new Exception($"?SYNTAX ERROR: UNEXPECTED CHARACTER '{c}'")
        };
    }

    private bool PeekAndAdvance(char expected)
    {
        if (_pos < _input.Length && _input[_pos] == expected)
        {
            _pos++;
            return true;
        }
        return false;
    }
}
