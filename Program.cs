using System.Collections.Concurrent;
using ApplesoftEmulator;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ??
    [
        "http://localhost:4280",
        "http://127.0.0.1:4280",
        "http://localhost:5500",
        "http://127.0.0.1:5500"
    ];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors("frontend");

app.MapGet("/", () => Results.Ok(new
{
    name = "Applesoft BASIC Emulator API",
    endpoints = new[]
    {
        "GET /health",
        "POST /api/session",
        "POST /api/session/{sessionId}/execute",
        "POST /api/session/{sessionId}/reset",
        "HUB /hubs/emulator"
    }
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok"
}));

app.MapPost("/api/session", (SessionStore sessions) =>
{
    var session = sessions.CreateSession();
    return Results.Ok(new { sessionId = session.Id });
});

app.MapPost("/api/session/{sessionId}/reset", (string sessionId, SessionStore sessions) =>
{
    var session = sessions.GetOrCreate(sessionId);
    lock (session.Gate)
    {
        session.Reset();
    }

    return Results.Ok(new { sessionId = session.Id, reset = true });
});

app.MapPost("/api/session/{sessionId}/execute", (string sessionId, ExecuteRequest request, SessionStore sessions) =>
{
    if (string.IsNullOrWhiteSpace(request.Command))
    {
        return Results.BadRequest(new { error = "command is required" });
    }

    var session = sessions.GetOrCreate(sessionId);
    lock (session.Gate)
    {
        var io = new BufferedRuntimeIO(request.Inputs, request.KeyInputs?.ToCharArray());
        session.Interpreter.SetRuntimeIO(io);

        try
        {
            EmulatorCommandRunner.ExecuteCommand(session.Interpreter, request.Command);
        }
        catch (InputExhaustedException ex)
        {
            if (!string.IsNullOrEmpty(io.Output) && !io.Output.EndsWith('\n'))
            {
                io.WriteLine();
            }

            io.WriteLine(ex.KeyInputRequired
                ? "[KEY INPUT REQUIRED. ADD CHARACTERS TO THE KEY STREAM BOX, THEN RUN AGAIN.]"
                : "[INPUT REQUIRED. ADD VALUES TO THE INPUT QUEUE BOX, THEN RUN AGAIN.]");

            return Results.Ok(new ExecuteResponse(io.Output, true));
        }
        catch (Exception ex)
        {
            io.WriteLine($"?ERROR: {ex.Message}");
        }

        return Results.Ok(new ExecuteResponse(io.Output));
    }
});

app.MapHub<ExecutionHub>("/hubs/emulator");

app.Run();

static class EmulatorCommandRunner
{
    public static void ExecuteCommand(Interpreter interpreter, string input)
    {
        var trimmedInput = input.TrimEnd();
        if (string.IsNullOrWhiteSpace(trimmedInput))
        {
            return;
        }

        if (trimmedInput.Equals("QUIT", StringComparison.OrdinalIgnoreCase) ||
            trimmedInput.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var trimmed = trimmedInput.TrimStart();
        if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
        {
            int i = 0;
            while (i < trimmed.Length && char.IsDigit(trimmed[i]))
            {
                i++;
            }

            if (int.TryParse(trimmed[..i], out int lineNum))
            {
                string rest = trimmed[i..].TrimStart();
                interpreter.StoreLine(lineNum, rest);
                return;
            }
        }

        interpreter.ExecuteDirect(trimmedInput.ToUpperInvariant() == "RUN" ? "RUN" : trimmedInput);
    }
}

sealed record ExecuteRequest(string Command, string[]? Inputs, string? KeyInputs);
sealed record ExecuteResponse(string Output, bool AwaitingInput = false);

sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, EmulatorSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _seedDiskPath;
    private readonly string _sessionRoot;

    public SessionStore()
    {
        _seedDiskPath = Path.Combine(Directory.GetCurrentDirectory(), "disk");
        _sessionRoot = Path.Combine(Path.GetTempPath(), "applesoft-emulator", "session-data");
        Directory.CreateDirectory(_sessionRoot);
    }

    public EmulatorSession CreateSession()
    {
        var id = Guid.NewGuid().ToString("N");
        return GetOrCreate(id);
    }

    public EmulatorSession GetOrCreate(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, CreateInternal);
    }

    private EmulatorSession CreateInternal(string sessionId)
    {
        var diskPath = Path.Combine(_sessionRoot, sessionId, "disk");
        Directory.CreateDirectory(diskPath);
        SeedDisk(diskPath);

        var interpreter = new Interpreter(new BufferedRuntimeIO(), diskPath);
        return new EmulatorSession(sessionId, diskPath, interpreter);
    }

    private void SeedDisk(string destination)
    {
        if (!Directory.Exists(_seedDiskPath))
        {
            return;
        }

        foreach (var source in Directory.GetFiles(_seedDiskPath, "*.bas"))
        {
            var target = Path.Combine(destination, Path.GetFileName(source));
            if (!File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
    }
}

sealed class EmulatorSession
{
    public EmulatorSession(string id, string diskPath, Interpreter interpreter)
    {
        Id = id;
        DiskPath = diskPath;
        Interpreter = interpreter;
    }

    public string Id { get; }
    public string DiskPath { get; }
    public object Gate { get; } = new();
    public Interpreter Interpreter { get; private set; }
    public string? OwnerConnectionId { get; set; }
    public Task? ActiveExecution { get; set; }
    public StreamingRuntimeIO? StreamingIO { get; set; }

    public void Reset()
    {
        StreamingIO = null;
        ActiveExecution = null;
        Interpreter = new Interpreter(new BufferedRuntimeIO(), DiskPath);
    }
}
