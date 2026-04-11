using System.Text;

namespace ApplesoftEmulator;

public sealed class InputExhaustedException : Exception
{
    public InputExhaustedException(bool keyInputRequired)
        : base(keyInputRequired ? "key input required" : "line input required")
    {
        KeyInputRequired = keyInputRequired;
    }

    public bool KeyInputRequired { get; }
}

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
            throw new InputExhaustedException(false);
        }

        return _lineInputs.Dequeue();
    }

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        if (_charInputs.Count == 0)
        {
            throw new InputExhaustedException(true);
        }

        var ch = _charInputs.Dequeue();
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

public sealed class StreamingRuntimeIO : IRuntimeIO
{
    private readonly Action<string> _onOutput;
    private readonly Action<string, bool> _onInputRequested;
    private readonly TimeSpan _inputTimeout;
    private readonly object _sync = new();
    private TaskCompletionSource<string>? _pendingLineInput;
    private TaskCompletionSource<char>? _pendingCharInput;
    private readonly StringBuilder _output = new();

    public StreamingRuntimeIO(Action<string> onOutput, Action<string, bool> onInputRequested, TimeSpan? inputTimeout = null)
    {
        _onOutput = onOutput;
        _onInputRequested = onInputRequested;
        _inputTimeout = inputTimeout ?? TimeSpan.FromMinutes(5);
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

        _onOutput(value);
    }

    public void WriteLine(string value = "")
    {
        Write(value);
        Write("\n");
    }

    public string? ReadLine()
    {
        var tcs = CreatePendingLineInput();
        _onInputRequested("? ", false);

        if (!tcs.Task.Wait(_inputTimeout))
        {
            ClearPendingInput(tcs);
            throw new TimeoutException("Timed out waiting for line input.");
        }

        return tcs.Task.Result;
    }

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        var tcs = CreatePendingKeyInput();
        _onInputRequested("", true);

        if (!tcs.Task.Wait(_inputTimeout))
        {
            ClearPendingInput(tcs);
            throw new TimeoutException("Timed out waiting for key input.");
        }

        var ch = tcs.Task.Result;
        return new ConsoleKeyInfo(ch, ConsoleKey.NoName, false, false, false);
    }

    public bool TrySubmitInput(string input)
    {
        lock (_sync)
        {
            if (_pendingCharInput is not null)
            {
                var ch = string.IsNullOrEmpty(input) ? '\n' : input[0];
                var pending = _pendingCharInput;
                _pendingCharInput = null;
                return pending.TrySetResult(ch);
            }

            if (_pendingLineInput is not null)
            {
                var pending = _pendingLineInput;
                _pendingLineInput = null;
                return pending.TrySetResult(input ?? string.Empty);
            }
        }

        return false;
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

    private TaskCompletionSource<string> CreatePendingLineInput()
    {
        lock (_sync)
        {
            if (_pendingLineInput is not null || _pendingCharInput is not null)
            {
                throw new InvalidOperationException("Already waiting for input.");
            }

            _pendingLineInput = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pendingLineInput;
        }
    }

    private TaskCompletionSource<char> CreatePendingKeyInput()
    {
        lock (_sync)
        {
            if (_pendingLineInput is not null || _pendingCharInput is not null)
            {
                throw new InvalidOperationException("Already waiting for input.");
            }

            _pendingCharInput = new TaskCompletionSource<char>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pendingCharInput;
        }
    }

    private void ClearPendingInput(TaskCompletionSource<string> expected)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_pendingLineInput, expected))
            {
                _pendingLineInput = null;
            }
        }
    }

    private void ClearPendingInput(TaskCompletionSource<char> expected)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_pendingCharInput, expected))
            {
                _pendingCharInput = null;
            }
        }
    }
}
