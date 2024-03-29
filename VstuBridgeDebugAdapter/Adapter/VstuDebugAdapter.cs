using System.Dynamic;
using System.Globalization;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using VstuBridgeDebugAdaptor.Helpers;
using VstuBridgeDebugAdaptor.Interfaces;
using VstuBridgeDebugAdaptor.Vstu;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace VstuBridgeDebugAdaptor.Adapter;

sealed class VstuDebugAdapter : DebugAdapterBase, IListener
{
    readonly TextWriter logger;
    readonly Dictionary<string, List<BreakpointEntry>> breakpointsBySource = new(StringComparer.OrdinalIgnoreCase);
    readonly List<BreakpointEntry> functionBreakpoints = new();
    readonly Dictionary<int, (string, int)> gotoTargets = new();
    DebuggerSession session = null!;
    int breakpointIdCounter;
    int gotoTargetIdCounter;
    bool threadStartedAtFirst;

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
            SupportsFunctionBreakpoints = true,

            SupportsConditionalBreakpoints = true,
            SupportsHitConditionalBreakpoints = true,

            SupportsExceptionConditions = false,
            SupportsExceptionOptions = false,

            SupportsEvaluateForHovers = true,
            SupportsSetVariable = false,
            SupportsTerminateRequest = true,

            ExceptionBreakpointFilters = new(),

            SupportsGotoTargetsRequest = true,
        };
    }

    protected override AttachResponse HandleAttachRequest(AttachArguments arguments)
    {
        AttachCore(arguments.ConfigurationProperties);
        return new();
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        AttachCore(arguments.ConfigurationProperties);
        return new();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        session?.Terminate();
        session = null!;
        return new();
    }

    protected override TerminateResponse HandleTerminateRequest(TerminateArguments arguments)
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
        var sourcePath = arguments.Source.Path;
        if (!breakpointsBySource.TryGetValue(sourcePath, out var breakpoints))
        {
            breakpointsBySource.Add(sourcePath, breakpoints = new());
        }

        var args = arguments.Breakpoints.Select(x => new BreakpointEntry(x, 0)).ToList();
        SetBreakpointsCore(
            breakpoints,
            args,
            x => session.AddBreakpoint(sourcePath, x.Line, x.Column, x.Condition, x.HitCount, x.HitCondition),
            x => session.DeleteBreakpoint(sourcePath, x.Line, x.Column));

        return new()
        {
            Breakpoints = breakpoints.Select(x => x.ToResponse()).ToList(),
        };
    }

    protected override SetFunctionBreakpointsResponse HandleSetFunctionBreakpointsRequest(SetFunctionBreakpointsArguments arguments)
    {
        var args = arguments.Breakpoints.Select(x => new BreakpointEntry(x, 0)).ToList();
        SetBreakpointsCore(
            functionBreakpoints,
            args,
            x => session.AddBreakpoint(x.Name, x.Condition, x.HitCount, x.HitCondition),
            x => session.DeleteBreakpoint(x.Name));

        return new()
        {
            Breakpoints = functionBreakpoints.Select(x => x.ToResponse()).ToList(),
        };
    }

    void SetBreakpointsCore(List<BreakpointEntry> currentEntries, List<BreakpointEntry> arguments, Action<BreakpointEntry> addAction, Action<BreakpointEntry> deleteAction)
    {
        for (var i = 0; i < currentEntries.Count; ++i)
        {
            var entry = currentEntries[i];
            var found = arguments.FirstOrDefault(x => x.EqualsPosition(entry));
            if (found == null || entry.IsConditionChanged(found))
            {
                deleteAction(entry);
                currentEntries.RemoveAt(i);
                --i;
            }
        }

        var temp = currentEntries.ToArray();
        currentEntries.Clear();
        foreach (var arg in arguments)
        {
            var found = temp.FirstOrDefault(x => x.EqualsPosition(arg));
            if (found != null)
            {
                currentEntries.Add(found);  // reuse
                continue;
            }

            var id = Interlocked.Increment(ref breakpointIdCounter);
            var newEntry = arg with { Id = id };
            currentEntries.Add(newEntry);
            addAction(newEntry);
        }
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        if (arguments.Filters.Count > 0)
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

    protected override GotoTargetsResponse HandleGotoTargetsRequest(GotoTargetsArguments arguments)
    {
        if (!session.CanGoto(arguments.Source.Path, arguments.Line))
        {
            return new();
        }

        var targetId = Interlocked.Increment(ref gotoTargetIdCounter);
        gotoTargets.Add(targetId, (arguments.Source.Path, arguments.Line));

        return new()
        {
            Targets = new()
            {
                new()
                {
                    Id = targetId,
                    Line = arguments.Line,
                },
            },
        };
    }

    protected override GotoResponse HandleGotoRequest(GotoArguments arguments)
    {
        var threadId = arguments.ThreadId;
        var targetId = arguments.TargetId;
        var (path, line) = gotoTargets[targetId];
        if (session.Goto(threadId, path, line))
        {
            var stopEvent = new StoppedEvent(StoppedEvent.ReasonValue.Goto)
            {
                ThreadId = threadId,
            };

            Task.Delay(100)
                .ContinueWith(_ => Protocol.SendEvent(stopEvent));
        }

        return new();
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
    }

    public void OnOutput(string s)
    {
        SendOutput(s);
    }

    public void OnThreadStarted(int threadId)
    {
        if (!threadStartedAtFirst)
        {
            // ready to accept breakpoints
            threadStartedAtFirst = true;
            SendOutput($"Initialized");
            Protocol.SendEvent(new InitializedEvent());
        }

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

    public void OnBreakpointVerified(string file, int line, int column, string functionName, bool verified)
    {
        var bp = FindBreakpoint(file, line, column, functionName);
        if (bp == null)
        {
            SendOutput($"unknown breakpoint: {file}:{line}:{column}");
            return;
        }

        bp.Verified = verified;
        Protocol.SendEvent(new BreakpointEvent(BreakpointEvent.ReasonValue.Changed, bp.ToResponse()));
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

    public void OnStoppedByBreakpoint(int threadId, string file, int line, int column, string functionName)
    {
        var bp = FindBreakpoint(file, line, column, functionName);
        var ids = bp == null ? new List<int>() : new List<int>() { bp.Id };

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

    void AttachCore(Dictionary<string, JToken> conf)
    {
        if (conf.TryGetValue("waitDebuggerAttached", out var wait) && (bool)wait)
        {
            SendOutput($"Waiting for debugger to attach...");
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }

            System.Diagnostics.Debugger.Break();
        }

        conf.TryGetValue("projectPath", out var projectPathToken);
        var projectPath = projectPathToken?.ToString();

        var (address, port, processId) = UnityDiscoveryHelper.GetConnectionInfo(projectPath);
        SendOutput($"Attaching to process {processId} ({address}:{port})...");

        session = new(this);
        session.Attach(address, port);
    }

    BreakpointEntry? FindBreakpoint(string file, int line, int column, string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
        {
            if (breakpointsBySource.TryGetValue(file, out var breakpoints))
            {
                return breakpoints.FirstOrDefault(b => b.Line == line && b.Column == column);
            }
        }
        else
        {
            return functionBreakpoints.FirstOrDefault(b => b.Name == functionName);
        }

        return null;
    }
}
