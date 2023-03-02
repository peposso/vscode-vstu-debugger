using System.Dynamic;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using VstuBridgeDebugAdaptor.Helpers;
using VstuBridgeDebugAdaptor.Interfaces;
using VstuBridgeDebugAdaptor.Vstu;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace VstuBridgeDebugAdaptor.Adapter;

sealed class VstuDebugAdapter : DebugAdapterBase, IListener
{
    readonly TextWriter logger;
    readonly Dictionary<string, List<Breakpoint>> breakpointsBySource = new();
    DebuggerSession session = null!;
    int breakpointIdCounter;

    public VstuDebugAdapter(Stream input, Stream output, TextWriter logger)
    {
        InitializeProtocolClient(input, output);

        Protocol.LogMessage += (sender, args) =>
        {
            logger.WriteLine(args.Message);
            logger.Flush();
        };

        Protocol.DispatcherError += (sender, e) =>
        {
            logger.WriteLine(e.Exception.Message);
            logger.WriteLine(e.Exception.StackTrace);
            logger.Flush();
        };
        this.logger = logger;
    }

    protected override ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
    {
        try
        {
            return base.HandleProtocolRequest(requestType, requestArgs);
        }
        catch (ProtocolException)
        {
            throw;
        }
        catch (Exception e)
        {
            SendOutput(e.ToString(), isError: true);
            throw new ProtocolException(e.Message, e);
        }
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
    {
        // https://github.com/deitry/vscode-unity-debug/blob/preview/UnityDebug/UnityDebugSession.cs#L224
        return new()
        {
            SupportsConfigurationDoneRequest = false,
            SupportsFunctionBreakpoints = false,

            SupportsConditionalBreakpoints = false,
            SupportsEvaluateForHovers = true,

            SupportsExceptionConditions = false,
            SupportsExceptionOptions = false,

            SupportsHitConditionalBreakpoints = false,

            SupportsSetVariable = false,
            SupportsTerminateRequest = true,

            ExceptionBreakpointFilters = new(),
        };
    }

    protected override AttachResponse HandleAttachRequest(AttachArguments arguments)
    {
        arguments.ConfigurationProperties.TryGetValue("projectPath", out var projectPathToken);
        var projectPath = projectPathToken?.ToString();

        var (address, port, processId) = UnityDiscoveryHelper.GetConnectionInfo(projectPath);
        SendOutput($"Attaching to process {processId} ({address}:{port})...");

        session = new(this);
        session.Attach(address, port);

        return new();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        session?.Terminate();
        session = null!;
        return new();
    }

    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
    {
        var threads = session.GetThreads();
        return new()
        {
            Threads = threads.Select(t => new Thread(t.Id, $"Thread #{(uint)t.Id} {t.Name}")).ToList(),
        };
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        var source = arguments.Source;
        if (!breakpointsBySource.TryGetValue(source.Path, out var breakpoints))
        {
            breakpoints = new List<Breakpoint>();
            breakpointsBySource[source.Path] = breakpoints;
        }

        for (var i = 0; i < breakpoints.Count; ++i)
        {
            var bp = breakpoints[i];
            var line = bp.Line ?? 0;
            var column = bp.Column ?? 0;
            var found = arguments.Breakpoints.FirstOrDefault(b => b.Line == line && (b.Column ?? 0) == column);
            if (found == null)
            {
                SendOutput($"Request Delete Breakpoint: {source.Path}:{line}:{column}");
                session.DeleteBreakpoint(source.Path, line, column);
                breakpoints.RemoveAt(i);
                --i;
            }
        }

        foreach (var bp in arguments.Breakpoints)
        {
            var column = bp.Column ?? 0;
            var found = breakpoints.FirstOrDefault(x => x.Line == bp.Line && x.Column == column);
            if (found != null)
            {
                continue;
            }

            // note. breakpoints.Add() -> session.SetBreakpoint()
            var id = Interlocked.Increment(ref breakpointIdCounter);
            breakpoints.Add(new Breakpoint(verified: false)
            {
                Id = id,
                Line = bp.Line,
                Column = column,
            });

            SendOutput($"Request Add Breakpoint: {source.Path}:{bp.Line}:{column}");
            session.AddBreakpoint(source.Path, bp.Line, column);
        }

        breakpoints.Sort((x, y) => (x.Line!.Value, x.Column!.Value).CompareTo((y.Line!.Value, y.Column!.Value)));
        return new()
        {
            Breakpoints = breakpoints,
        };
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        if (arguments.Filters is not [])
            throw new NotSupportedException();

        return new();
    }

    protected override PauseResponse HandlePauseRequest(PauseArguments arguments)
    {
        session.Pause();
        return new();
    }

    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
    {
        var startFrame = arguments.StartFrame ?? 0;
        var levels = arguments.Levels ?? 10;

        var stackFrames = new List<StackFrame>();
        var level = -1;
        foreach (var sf in session.GetStackFrames(arguments.ThreadId))
        {
            level++;
            if (level < startFrame)
                continue;
            if (level >= startFrame + levels)
                continue;

            var stackFrame = new StackFrame(sf.Id, sf.Name, sf.Line, sf.Column);
            if (!string.IsNullOrEmpty(sf.SourcePath))
            {
                stackFrame.Source = new Source()
                {
                    Path = sf.SourcePath,
                    Name = Path.GetFileName(sf.SourcePath),
                };
            }

            stackFrames.Add(stackFrame);
        }

        return new()
        {
            StackFrames = stackFrames,
        };
    }

    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        throw new ProtocolException("source request is not supported");
    }

    protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
    {
        var scopes = new List<Scope>();

        var variablesReference = session.GetFrameVariablesReference(arguments.FrameId);
        if (variablesReference != 0)
        {
            var scope = new Scope("Local", variablesReference, false);
            scopes.Add(scope);
        }

        return new()
        {
            Scopes = scopes,
        };
    }

    protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
    {
        var variables = new List<Variable>();
        foreach (var v in session.GetVariables(arguments.VariablesReference))
        {
            var variable = new Variable(v.Name, v.Value, v.VariablesReference);
            variables.Add(variable);
        }

        return new()
        {
            Variables = variables,
        };
    }

    protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
    {
        session.Continue();
        return new();
    }

    protected override NextResponse HandleNextRequest(NextArguments arguments)
    {
        session.StepOver(arguments.ThreadId);
        return new();
    }

    protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
    {
        session.StepIn(arguments.ThreadId);
        return new();
    }

    protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
    {
        session.StepOut(arguments.ThreadId);
        return new();
    }

    protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
    {
        var frameId = arguments.FrameId ?? 0;
        if (frameId == 0)
        {
            return new() { Result = "" };
        }

        var expr = arguments.Expression;
        var result = session.EvaluateExpression(frameId, expr);
        return new()
        {
            Result = result.Value,
            VariablesReference = result.VariablesReference,
        };
    }

    void SendOutput(string text, bool isError = false)
    {
        Protocol.SendEvent(new OutputEvent(text + "\n")
        {
            Category = isError ? OutputEvent.CategoryValue.Stderr : OutputEvent.CategoryValue.Stdout,
            Severity = isError ? OutputEvent.SeverityValue.Error : OutputEvent.SeverityValue.Ok,
        });

        logger.WriteLine(text);
        logger.Flush();
    }

    internal void Terminate()
    {
        session?.Terminate();
    }

    public void OnDebuggerConnected()
    {
        SendOutput("OnDebuggerConnected.");
    }

    public void OnDebuggerConnectionFailed(Exception? e)
    {
        throw new InvalidOperationException("DebuggerConnectionFailed: " + e?.ToString());
    }

    public void OnLoadCompleted()
    {
        // ready to accept breakpoints
        Protocol.SendEvent(new InitializedEvent());
    }

    public void OnOutput(string s)
    {
        SendOutput(s);
    }

    public void OnThreadStarted(int threadId)
    {
        SendOutput($"OnThreadStarted: #{threadId}");
        Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Started, threadId));
    }

    public void OnThreadExited(int threadId)
    {
        SendOutput($"OnThreadExited: #{threadId}");
        Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Exited, threadId));
    }

    public void OnAssemblyLoaded(string assemblyUri)
    {
        var name = Path.GetFileNameWithoutExtension(assemblyUri);
        SendOutput($"OnAssemblyLoaded: {name}");
        var module = new Module(name, name);
        Protocol.SendEvent(new ModuleEvent(ModuleEvent.ReasonValue.New, module));
    }

    public void OnBreakpointVerified(string file, int line, int column, bool verified)
    {
        var bp = FindBreakpoint(file, line, column);
        if (bp == null)
        {
            SendOutput($"unknown breakpoint: {file}:{line}:{column}");
            return;
        }

        bp.Verified = verified;
        Protocol.SendEvent(new BreakpointEvent(BreakpointEvent.ReasonValue.Changed, bp));
    }

    public void OnStoppedByPause(int threadId)
    {
        Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Pause)
        {
            ThreadId = threadId,
        });
    }

    public void OnStoppedByStep(int threadId)
    {
        Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step)
        {
            ThreadId = threadId,
        });
    }

    public void OnStoppedByBreakpoint(int threadId, string file, int line, int column)
    {
        var bp = FindBreakpoint(file, line, column);
        var ids = bp == null ? new List<int>() : new List<int>() { bp.Id!.Value };

        Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Breakpoint)
        {
            ThreadId = threadId,
            HitBreakpointIds = ids,
            AllThreadsStopped = true,
        });
    }

    public void OnSessionTermination(Exception? e)
    {
        logger.WriteLine("OnSessionTermination");
        if (e is not null)
        {
            logger.WriteLine(e.ToString());
        }

        logger.Flush();
        Protocol.Stop();
    }

    Breakpoint? FindBreakpoint(string file, int line, int column)
    {
        if (breakpointsBySource.TryGetValue(file, out var breakpoints))
        {
            return breakpoints.FirstOrDefault(b => b.Line == line && b.Column == column);
        }

        return null;
    }
}
