using ApplesoftEmulator;
using Microsoft.AspNetCore.SignalR;

sealed class ExecutionHub : Hub
{
    private readonly SessionStore _sessions;
    private readonly IHubContext<ExecutionHub> _hubContext;

    public ExecutionHub(SessionStore sessions, IHubContext<ExecutionHub> hubContext)
    {
        _sessions = sessions;
        _hubContext = hubContext;
    }

    public Task AttachSession(string sessionId)
    {
        var session = _sessions.GetOrCreate(sessionId);
        lock (session.Gate)
        {
            session.OwnerConnectionId = Context.ConnectionId;
        }

        return Task.CompletedTask;
    }

    public async Task StartExecution(string sessionId, string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            await Clients.Caller.SendAsync("ExecutionError", "Command is required.");
            return;
        }

        var session = _sessions.GetOrCreate(sessionId);
        StreamingRuntimeIO io;

        lock (session.Gate)
        {
            if (session.ActiveExecution is { IsCompleted: false })
            {
                _ = Clients.Caller.SendAsync("ExecutionError", "Execution already in progress.");
                return;
            }

            session.OwnerConnectionId = Context.ConnectionId;
            io = new StreamingRuntimeIO(
                output => SendToOwner(session, "ReceiveOutput", output),
                (prompt, isKeyInput) => SendToOwner(session, "InputRequested", new { prompt, isKeyInput }),
                TimeSpan.FromMinutes(5));

            session.StreamingIO = io;
            session.Interpreter.SetRuntimeIO(io);
            session.ActiveExecution = Task.Run(() => RunExecutionAsync(session, command));
        }

        await Clients.Caller.SendAsync("ExecutionStarted", new { sessionId });
    }

    public async Task SubmitInput(string sessionId, string input)
    {
        var session = _sessions.GetOrCreate(sessionId);

        StreamingRuntimeIO? io;
        lock (session.Gate)
        {
            io = session.StreamingIO;
        }

        if (io is null)
        {
            await Clients.Caller.SendAsync("ExecutionError", "No active execution is waiting for input.");
            return;
        }

        if (!io.TrySubmitInput(input))
        {
            await Clients.Caller.SendAsync("ExecutionError", "Execution is not currently waiting for input.");
        }
    }

    private async Task RunExecutionAsync(EmulatorSession session, string command)
    {
        try
        {
            EmulatorCommandRunner.ExecuteCommand(session.Interpreter, command);
        }
        catch (TimeoutException)
        {
            await SendToOwner(session, "ExecutionError", "Input timed out. Run the program again to continue.");
        }
        catch (Exception ex)
        {
            await SendToOwner(session, "ExecutionError", ex.Message);
        }
        finally
        {
            lock (session.Gate)
            {
                session.StreamingIO = null;
                session.ActiveExecution = null;
            }

            await SendToOwner(session, "ExecutionComplete", new { sessionId = session.Id });
        }
    }

    private Task SendToOwner(EmulatorSession session, string method, object payload)
    {
        string? ownerConnectionId;
        lock (session.Gate)
        {
            ownerConnectionId = session.OwnerConnectionId;
        }

        if (string.IsNullOrWhiteSpace(ownerConnectionId))
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients.Client(ownerConnectionId).SendAsync(method, payload);
    }
}
