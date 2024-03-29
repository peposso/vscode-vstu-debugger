using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Debugger.Interop;
using SyntaxTree.VisualStudio.Unity.Debugger;
using SyntaxTree.VisualStudio.Unity.Projects;
using VstuBridgeDebugAdapter.Helpers;
using VstuBridgeDebugAdaptor.Interfaces;
#pragma warning disable CA1822

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class DebuggerSession : IDebugEventCallback2, IDebugPortNotify2, IProjectFileMapper
{
    [ThreadStatic]
    static DebuggerSession? threadStaticInstance;

    public static DebuggerSession? Instance => threadStaticInstance;

    public IProjectFileMapper FileMapper => this;

    public string ExceptionIdentifier => "__ExceptionIdentifier";

    public bool ExceptionSupport => false;

    public bool RequiresDocumentContext => false;

    private readonly IListener listener;
    private readonly IDebugEngineLaunch2 engineLaunch;
    private readonly IDebugEngine2 engine;
    private readonly IDebugProgram3 program;
    private readonly IDebugPort2 port;
    readonly ConcurrentDictionary<int, IDebugThread2> threads = new();
    readonly ConcurrentDictionary<IDebugPendingBreakpoint2, BreakpointInfo> pendingBreakpoints = new();
    readonly ConcurrentDictionary<int, FrameInfo> frames = new();
    readonly ConcurrentDictionary<int, IDebugProperty2> properties = new();
    IDebugProcess2 process = null!;
    int frameIdCounter;
    int propertyIdCounter;

    public DebuggerSession(IListener listener)
        : this(listener, new UnityEngine())
    {
    }

    DebuggerSession(IListener listener, UnityEngine engine)
        : this(listener, engine, engine, engine)
    {
    }

    DebuggerSession(IListener listener, IDebugEngineLaunch2 engineLaunch, IDebugEngine2 engine, IDebugProgram3 program)
    {
        this.listener = listener;
        this.engineLaunch = engineLaunch;
        this.engine = engine;
        this.program = program;
        port = new DebugPort(this);
        threadStaticInstance = this;
    }

    public int Event(IDebugEngine2 pEngine, IDebugProcess2 pProcess, IDebugProgram2 pProgram, IDebugThread2 pThread, IDebugEvent2 pEvent, ref Guid riidEvent, uint dwAttrib)
    {
        if (riidEvent == typeof(IDebugEngineCreateEvent2).GUID)
        {
            listener.OnOutput($"EngineCreated");
            return 0;
        }

        if (riidEvent == typeof(IDebugLoadCompleteEvent2).GUID)
        {
            listener.OnLoadCompleted();
            return 0;
        }

        if (riidEvent == typeof(IDebugProgramCreateEvent2).GUID)
        {
            listener.OnOutput($"ProgramCreated");
            return 0;
        }

        if (riidEvent == typeof(IDebugProgramDestroyEvent2).GUID)
        {
            listener.OnOutput($"ProgramDestroyed");
            return 0;
        }

        if (riidEvent == typeof(IDebugThreadCreateEvent2).GUID)
        {
            var tid = GetThreadId(pThread);
            threads[tid] = pThread;
            listener.OnThreadStarted(tid);
            return 0;
        }

        if (riidEvent == typeof(IDebugThreadDestroyEvent2).GUID)
        {
            var tid = GetThreadId(pThread);
            threads.TryRemove(tid, out IDebugThread2 _);
            listener.OnThreadExited(tid);
            return 0;
        }

        if (riidEvent == typeof(IDebugModuleLoadEvent2).GUID)
        {
            var ev = (IDebugModuleLoadEvent2)pEvent;
            var pbstrDebugMessage = "";
            var pbLoad = 0;
            if (ev.GetModule(out IDebugModule2 pModule, ref pbstrDebugMessage, ref pbLoad) == 0 && pbLoad > 0)
            {
                var info = new MODULE_INFO[1];
                if (pModule.GetInfo(enum_MODULE_INFO_FIELDS.MIF_URL, info) == 0)
                {
                    listener.OnAssemblyLoaded(info[0].m_bstrUrl);
                }
            }

            return 0;
        }

        if (riidEvent == typeof(IDebugBreakEvent2).GUID)
        {
            var tid = GetThreadId(pThread);
            threads[tid] = pThread;
            listener.OnStoppedByPause(tid);
            return 0;
        }

        if (riidEvent == typeof(IDebugBreakpointBoundEvent2).GUID)
        {
            var ev = (IDebugBreakpointBoundEvent2)pEvent;
            ev.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBP);
            if (pendingBreakpoints.TryGetValue(ppPendingBP, out var info))
            {
                info.PendingState = BreakpointPendingState.Verified;

                switch (info.Kind)
                {
                    case BreakpointKind.FileLine:
                        listener.OnOutput($"Breakpoint Bound: {info.File}:{info.Line}:{info.Column}");
                        break;
                    case BreakpointKind.Function:
                        listener.OnOutput($"Breakpoint Bound: {info.FunctionName}");
                        break;
                }

                listener.OnBreakpointVerified(info.File, info.Line, info.Column, info.FunctionName, verified: true);
            }
            else
            {
                listener.OnOutput($"unknown Breakpoint Bound");
            }

            return 0;
        }

        if (riidEvent == typeof(IDebugBreakpointErrorEvent2).GUID)
        {
            var ev = (IDebugBreakpointErrorEvent2)pEvent;
            ev.GetErrorBreakpoint(out IDebugErrorBreakpoint2 ppErrorBP);
            ppErrorBP.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBP);
            if (pendingBreakpoints.TryGetValue(ppPendingBP, out var info))
            {
                listener.OnOutput($"Breakpoint Error: {info.File}:{info.Line}:{info.Column}");
                info.PendingState = BreakpointPendingState.Error;
            }

            return 0;
        }

        if (riidEvent == typeof(IDebugBreakpointEvent2).GUID)
        {
            var ev = (IDebugBreakpointEvent2)pEvent;
            ev.EnumBreakpoints(out var ppEnum);
            var bp = new IDebugBoundBreakpoint2[1];
            var fetched = 0u;
            ppEnum.Next(1u, bp, ref fetched);
            bp[0].GetPendingBreakpoint(out var pendingBreakpoint);
            var tid = GetThreadId(pThread);
            if (!pendingBreakpoints.TryGetValue(pendingBreakpoint, out var info))
            {
                listener.OnOutput("DebugBreak at unknown breakpoint...");
                listener.OnStoppedByBreakpoint(tid, "", 0, 0, "");
                return 0;
            }

            listener.OnStoppedByBreakpoint(tid, info.File, info.Line, info.Column, info.FunctionName);

            return 0;
        }

        if (riidEvent == typeof(IDebugStepCompleteEvent2).GUID)
        {
            listener.OnOutput("StepCompleted");
            var tid = GetThreadId(pThread);
            listener.OnStoppedByStep(tid);
            return 0;
        }

        if (riidEvent == typeof(IDebugExceptionEvent2).GUID)
        {
            throw new NotSupportedException();
        }

        if (riidEvent == typeof(IDebugOutputStringEvent2).GUID)
        {
            var ev = (IDebugOutputStringEvent2)pEvent;
            ev.GetString(out var pbstrString);
            listener.OnOutput(pbstrString);
            return 0;
        }

        listener.OnOutput($"unknown Event: {riidEvent} ({pEvent.GetType().Name})");
        return 0;
    }

    public int AddProgramNode(IDebugProgramNode2 pProgramNode)
    {
        var programs = new IDebugProgram2[] { program };
        var nodes = new IDebugProgramNode2[] { pProgramNode };

        /// <see cref="UnityEngine.Attach" />
        return engine.Attach(programs, nodes, 1, this, enum_ATTACH_REASON.ATTACH_REASON_LAUNCH);
    }

    public int RemoveProgramNode(IDebugProgramNode2 pProgramNode)
        => 0;

    internal void Attach(string address, int port)
    {
        /// 1. <see cref="UnityEngine.LaunchSuspended" />
        /// <see cref="SyntaxTree.VisualStudio.Unity.Debugger.UnityDebuggerConnection.Connect" />
        /// 2. <see cref="UnityEngine.ResumeProcess" />
        /// 3. <see cref="AddProgramNode" />
        var options = $"{address}:{port}|{typeof(DebugEngineHost).AssemblyQualifiedName}";

        threadStaticInstance = this;
        var ret = engineLaunch.LaunchSuspended("", this.port, "", "", "", "", options, default, 0, 00, 0, this, out process);
        if (ret != 0)
        {
            throw new InvalidOperationException($"Failed to LaunchSuspended: {ret}");
        }

        ret = engineLaunch.ResumeProcess(process);
        if (ret != 0)
        {
            throw new InvalidOperationException($"Failed to ResumeProcess: {ret}");
        }
    }

    internal void Pause()
    {
        if (program.CauseBreak() != 0)
        {
            throw new InvalidOperationException("Failed to CauseBreak");
        }
    }

    internal void Continue()
    {
        frames.Clear();
        properties.Clear();

        // UnityEngineではスレッド関係ない.
        program.Continue(null!);
    }

    internal void StepOver(int threadId) => Step(threadId, enum_STEPKIND.STEP_OVER);

    internal void StepIn(int threadId) => Step(threadId, enum_STEPKIND.STEP_INTO);

    internal void StepOut(int threadId) => Step(threadId, enum_STEPKIND.STEP_OUT);

    void Step(int threadId, enum_STEPKIND kind)
    {
        if (!threads.TryGetValue(threadId, out var thread))
        {
            throw new InvalidOperationException($"Unknown thread: {threadId}");
        }

        frames.Clear();
        properties.Clear();

        var ret = program.Step(thread, kind, enum_STEPUNIT.STEP_STATEMENT);
        if (ret != 0)
        {
            throw new InvalidOperationException($"Failed to Step: {ret}");
        }
    }

    internal void Terminate()
    {
        /// <see cref="UnityEngine.Detach" />
        try
        {
            program.Detach();
        }
        catch (NullReferenceException)
        {
        }
    }

    public bool TryGetExceptionState(string fullName, out enum_EXCEPTION_STATE state)
    {
        throw new NotImplementedException();
    }

    public void OnDebuggerConnected() => listener.OnDebuggerConnected();

    public void OnDebuggerConnectionFailed(Exception? e) => listener.OnDebuggerConnectionFailed(e);

    public void OnGracefulSessionTermination() => listener.OnSessionTermination(null);

    public void OnUnexpectedSessionTermination(Exception? e) => listener.OnSessionTermination(e);

    public void OnUnexpectedSessionTermination() => listener.OnSessionTermination(null);

    public object GetExtendedInfo(Guid guidExtendedInfo, IDebugProperty3 property)
    {
        throw new NotImplementedException();
    }

    public string MappedFileName(string fileName)
    {
        if (File.Exists(fileName))
        {
            return fileName;
        }

        return null!;
    }

    internal IEnumerable<ThreadDto> GetThreads()
    {
        if (program.EnumThreads(out var ppEnum) != 0 || ppEnum == null)
        {
            // for safety.
            Terminate();
            throw new InvalidOperationException("Failed to EnumThreads");
        }

        threads.Clear();
        foreach (var debugThread in VsDebugHelper.ToArray(ppEnum))
        {
            var tid = GetThreadId(debugThread);
            threads[tid] = debugThread;
        }

        var result = new List<ThreadDto>();
        foreach (var (id, thread) in threads)
        {
            thread.GetName(out var name);
            result.Add(new(id, name));
        }
        return result;
    }

    internal IEnumerable<FrameDto> GetStackFrames(int threadId)
    {
        if (!threads.TryGetValue(threadId, out var thread))
        {
            return Enumerable.Empty<FrameDto>();
        }

        IEnumDebugFrameInfo2? ppEnum;
        var flags = enum_FRAMEINFO_FLAGS.FIF_FUNCNAME
                    | enum_FRAMEINFO_FLAGS.FIF_LANGUAGE
                    | enum_FRAMEINFO_FLAGS.FIF_MODULE
                    | enum_FRAMEINFO_FLAGS.FIF_FRAME
                    | enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO;
        var ret = thread.EnumFrameInfo(flags, 0, out ppEnum);
        if (ret != 0)
        {
            // maybe dead thread
            return Enumerable.Empty<FrameDto>();
        }

        var result = new List<FrameDto>();
        var begin = new TEXT_POSITION[1];
        var end = new TEXT_POSITION[1];
        foreach (var frameInfo in VsDebugHelper.ToArray(ppEnum))
        {
            var frameId = Interlocked.Increment(ref frameIdCounter);

            var name = frameInfo.m_bstrFuncName;
            var frame = frameInfo.m_pFrame;
            frame.GetDocumentContext(out var docContext);
            if (docContext == null)
            {
                result.Add(new(frameId, name, "", 0, 0));
                frames[frameId] = new()
                {
                    File = "",
                    Line = 0,
                    Info = frameInfo,
                    Frame = frame,
                    ThreadId = threadId,
                };
                continue;
            }

            docContext.GetName(enum_GETNAME_TYPE.GN_FILENAME, out var sourcePath);
            docContext.GetStatementRange(begin, end);
            var line = (int)(begin[0].dwLine + 1);
            var column = (int)begin[0].dwColumn;
            result.Add(new(frameId, name, sourcePath, line, column));
            frames[frameId] = new()
            {
                File = sourcePath,
                Line = line,
                Info = frameInfo,
                Frame = frame,
                ThreadId = threadId,
            };
        }

        return result;
    }

    internal int GetFrameVariablesReference(int frameId)
    {
        if (!frames.TryGetValue(frameId, out var info))
        {
            return 0;
        }

        info.Frame.GetDebugProperty(out var property);
        if (property is null)
        {
            return 0;
        }

        var propertyId = Interlocked.Increment(ref propertyIdCounter);
        properties[propertyId] = property;
        return propertyId;  // == variablesReference
    }

    internal IEnumerable<VariableDto> GetVariables(int variablesReference)
    {
        // variablesReference is propertyId
        if (!properties.TryGetValue(variablesReference, out var property))
        {
            return Enumerable.Empty<VariableDto>();
        }

        var variables = new List<VariableDto>();
        foreach (var info in VsDebugHelper.GetChildren(property))
        {
            var name = info.bstrName;
            var value = info.bstrValue;
            var type = info.bstrType;
            var propertyId = 0;
            if (info.pProperty != null && !IsWellKnownValueLikeType(type))
            {
                // listener.OnOutput($"name='{name}', value='{value}', type='{type}'");
                propertyId = Interlocked.Increment(ref propertyIdCounter);
                properties[propertyId] = info.pProperty;
            }

            variables.Add(new(name, value, propertyId));
        }

        return variables;
    }

    static bool IsWellKnownValueLikeType(string type)
    {
        switch (type)
        {
            case "bool":
            case "sbyte":
            case "byte":
            case "short":
            case "ushort":
            case "int":
            case "uint":
            case "long":
            case "ulong":
            case "char":
            case "string":
                return true;
            default:
                return false;
        }
    }

    static int GetThreadId(IDebugThread2 thread)
    {
        thread.GetThreadId(out var tid);
        return (int)(0x7fff_ffff & tid);
    }

    internal void AddBreakpoint(string path, int line, int column, string condition, int hitCount, HitConditionKind hitCondition)
    {
        foreach (var (_, x) in pendingBreakpoints)
        {
            if (x.Kind == BreakpointKind.FileLine
                && x.File == path
                && x.Line == line
                && x.Column == column)
            {
                return;
            }
        }

        var position = new DebugDocumentPosition(path, line, column);
        var request = new DebugBreakpointRequest(position, condition, hitCount, hitCondition);
        var info = new BreakpointInfo()
        {
            Kind = BreakpointKind.FileLine,
            File = path,
            Line = line,
            Column = column,
        };

        AddBreakpointCore(request, info);
    }

    internal void AddBreakpoint(string functionName, string condition, int hitCount, HitConditionKind hitCondition)
    {
        foreach (var (_, x) in pendingBreakpoints)
        {
            if (x.Kind == BreakpointKind.Function
                && x.FunctionName == functionName)
            {
                return;
            }
        }

        var position = new DebugFunctionPosition(functionName);
        var request = new DebugBreakpointRequest(position, condition, hitCount, hitCondition);
        var info = new BreakpointInfo()
        {
            Kind = BreakpointKind.Function,
            FunctionName = functionName,
        };

        AddBreakpointCore(request, info);
    }

    void AddBreakpointCore(DebugBreakpointRequest request, BreakpointInfo info)
    {
        var ret = engine.CreatePendingBreakpoint(request, out var pendingBreakpoint);
        if (ret != 0)
        {
            throw new InvalidOperationException($"Failed to CreatePendingBreakpoint: {ret}");
        }

        pendingBreakpoints[pendingBreakpoint] = info with
        {
            PendingBreakpoint = pendingBreakpoint,
            PendingState = BreakpointPendingState.Pending,
        };

        pendingBreakpoint.Bind();

        // may be, need to Enable even if Bind fails...
        pendingBreakpoint.Enable(1);
    }

    internal void DeleteBreakpoint(string path, int line, int column)
    {
        BreakpointInfo? info = null;
        foreach (var (_, x) in pendingBreakpoints)
        {
            if (x.Kind == BreakpointKind.FileLine
                && x.File == path
                && x.Line == line
                && x.Column == column)
            {
                info = x;
                break;
            }
        }

        if (info is null)
            return;

        info.PendingBreakpoint.Delete();
        pendingBreakpoints.TryRemove(info.PendingBreakpoint, out _);
    }

    internal void DeleteBreakpoint(string functionName)
    {
        BreakpointInfo? info = null;
        foreach (var (_, x) in pendingBreakpoints)
        {
            if (x.Kind == BreakpointKind.Function
                && x.FunctionName == functionName)
            {
                info = x;
                break;
            }
        }

        if (info is null)
            return;

        info.PendingBreakpoint.Delete();
        pendingBreakpoints.TryRemove(info.PendingBreakpoint, out _);
    }

    public void RefreshExceptionSettings()
    {
        throw new NotImplementedException();
    }

    public void OnBreakpointConditionError(object error, out string message, out enum_MESSAGETYPE messageType)
    {
        message = $"OnBreakpointConditionError: {error}";
        messageType = enum_MESSAGETYPE.MT_OUTPUTSTRING;
    }

    internal VariableDto EvaluateExpression(int frameId, string expression)
    {
        if (!frames.TryGetValue(frameId, out var frame))
            return VariableDto.Empty;

        var ret = frame.Info.m_pFrame.GetExpressionContext(out var expressionContext);
        if (ret != 0 || expressionContext == null)
            return VariableDto.Empty;

        ret = expressionContext.ParseText(expression, enum_PARSEFLAGS.PARSE_EXPRESSION, 10, out var ppExpr, out var err, out var pichError);
        if (ret != 0 || ppExpr == null)
            return new("", err, 0);

        ret = ppExpr.EvaluateSync(enum_EVALFLAGS.EVAL_DESIGN_TIME_EXPR_EVAL, 1000, null, out var result);
        if (ret != 0)
            return new("", "eval error...", 0);

        var propInfo = new DEBUG_PROPERTY_INFO[1];
        ret = result.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD | enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP, 10u, 1000, null, 0, propInfo);
        if (ret != 0)
            return new("", "timeout...", 0);

        var info = propInfo[0];
        var name = info.bstrName;
        var value = info.bstrValue;
        var type = info.bstrType;
        var propertyId = 0;
        if (info.pProperty != null && !IsWellKnownValueLikeType(type))
        {
            propertyId = Interlocked.Increment(ref propertyIdCounter);
            properties[propertyId] = info.pProperty;
        }

        return new(name, value, propertyId);
    }

    internal bool CanGoto(string path, int line)
    {
        foreach (var (_, frameInfo) in frames)
        {
            var file = frameInfo.File;
            if (string.IsNullOrEmpty(file) || !string.Equals(file, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destCodeContext = GetCodeContextFrom(frameInfo, line);
            if (destCodeContext == null)
            {
                continue;
            }

            var thread = threads[frameInfo.ThreadId];
            if (thread.CanSetNextStatement(frameInfo.Info.m_pFrame, destCodeContext) == 0)
            {
                return true;
            }
        }

        return false;
    }

    internal bool Goto(int threadId, string path, int line)
    {
        var success = false;
        foreach (var (_, frameInfo) in frames)
        {
            if (frameInfo.ThreadId != threadId)
                continue;

            var file = frameInfo.File;
            if (string.IsNullOrEmpty(file) || !string.Equals(file, path, StringComparison.OrdinalIgnoreCase))
                continue;

            var destCodeContext = GetCodeContextFrom(frameInfo, line);
            var thread = threads[frameInfo.ThreadId];
            if (thread.CanSetNextStatement(frameInfo.Frame, destCodeContext) == 0)
            {
                if (thread.SetNextStatement(frameInfo.Frame, destCodeContext) != 0)
                {
                    throw new InvalidOperationException("Failed to SetNextStatement");
                }

                success = true;
            }
        }

        if (success)
        {
            frames.Clear();
            properties.Clear();
        }

        return success;
    }

    IDebugCodeContext2? GetCodeContextFrom(FrameInfo frameInfo, int line)
    {
        var frame = frameInfo.Frame;
        frame.GetCodeContext(out var codeContext);
        if (codeContext == null)
        {
            return null;
        }

        var nextLine = line;
        var currentLine = frameInfo.Line;
        codeContext.Add((uint)(nextLine - currentLine), out var memContext);

        return memContext as IDebugCodeContext2;
    }
}
