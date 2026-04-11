using System.Text;

namespace ApplesoftEmulator;

public interface IRuntimeIO
{
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
    int BufferWidth { get; }
    int BufferHeight { get; }
    Encoding OutputEncoding { get; set; }

    void Write(string value);
    void WriteLine(string value = "");
    string? ReadLine();
    ConsoleKeyInfo ReadKey(bool intercept);
    void SetCursorPosition(int left, int top);
    void Clear();
}

public sealed class ConsoleRuntimeIO : IRuntimeIO
{
    public int CursorLeft
    {
        get => Console.CursorLeft;
        set => Console.CursorLeft = value;
    }

    public int CursorTop
    {
        get => Console.CursorTop;
        set => Console.CursorTop = value;
    }

    public int BufferWidth => Console.BufferWidth;
    public int BufferHeight => Console.BufferHeight;

    public Encoding OutputEncoding
    {
        get => Console.OutputEncoding;
        set => Console.OutputEncoding = value;
    }

    public void Write(string value) => Console.Write(value);
    public void WriteLine(string value = "") => Console.WriteLine(value);
    public string? ReadLine() => Console.ReadLine();
    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);
    public void Clear() => Console.Clear();
}

public sealed class BufferedRuntimeIO : IRuntimeIO
{
    private readonly Queue<string> _lineInputs;
    private readonly Queue<char> _charInputs;
    private readonly StringBuilder _output = new();

    public BufferedRuntimeIO(IEnumerable<string>? lineInputs = null, IEnumerable<char>? charInputs = null)
    {
        _lineInputs = new Queue<string>(lineInputs ?? Array.Empty<string>());
        _charInputs = new Queue<char>(charInputs ?? Array.Empty<char>());
    }

    public int CursorLeft { get; set; }
    public int CursorTop { get; set; }
    public int BufferWidth => 120;
    public int BufferHeight => 60;
    public Encoding OutputEncoding { get; set; } = Encoding.UTF8;

    public string Output => _output.ToString();

    public void Write(string value)
    {
        _output.Append(value);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\n')
            {
                CursorLeft = 0;
                CursorTop++;
            }
            else if (value[i] != '\r')
            {
                CursorLeft++;
            }
        }
    }

    public void WriteLine(string value = "")
    {
        Write(value);
        Write("\n");
    }

    public string? ReadLine()
    {
        if (_lineInputs.Count == 0)
        {
            return string.Empty;
        }

        return _lineInputs.Dequeue();
    }

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        var ch = _charInputs.Count > 0 ? _charInputs.Dequeue() : '\n';
        return new ConsoleKeyInfo(ch, ConsoleKey.NoName, false, false, false);
    }

    public void SetCursorPosition(int left, int top)
    {
        CursorLeft = Math.Max(0, left);
        CursorTop = Math.Max(0, top);
    }

    public void Clear()
    {
        _output.Clear();
        CursorLeft = 0;
        CursorTop = 0;
    }
}
