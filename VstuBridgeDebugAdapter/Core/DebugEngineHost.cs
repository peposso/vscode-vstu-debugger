using Microsoft.VisualStudio.Debugger.Interop;
using SyntaxTree.VisualStudio.Unity.Debugger;
using SyntaxTree.VisualStudio.Unity.Projects;

namespace VstuBridgeDebugAdaptor.Core;

sealed class DebugEngineHost : IDebugEngineHost
{
    DebuggerSession session;

    public DebugEngineHost()
    {
        session = DebuggerSession.Instance ?? throw new InvalidOperationException("DebuggerSession.Instance is null");
    }

    public IProjectFileMapper FileMapper => session.FileMapper;

    public string ExceptionIdentifier => session.ExceptionIdentifier;

    public bool ExceptionSupport => session.ExceptionSupport;

    public bool RequiresDocumentContext => session.RequiresDocumentContext;

    public object GetExtendedInfo(Guid guidExtendedInfo, IDebugProperty3 property)
        => session.GetExtendedInfo(guidExtendedInfo, property);

    public void OnBreakpointConditionError(BreakpointConditionError error, out string message, out enum_MESSAGETYPE messageType)
        => session.OnBreakpointConditionError(error, out message, out messageType);

    public void OnDebuggerConnected()
        => session.OnDebuggerConnected();

    public void OnDebuggerConnectionFailed(Exception? e)
        => session.OnDebuggerConnectionFailed(e);

    public void OnGracefulSessionTermination()
        => session.OnGracefulSessionTermination();

    public void OnUnexpectedSessionTermination(Exception? e)
        => session.OnUnexpectedSessionTermination(e);

    public void OnUnexpectedSessionTermination()
        => session.OnUnexpectedSessionTermination();

    public void RefreshExceptionSettings()
        => session.RefreshExceptionSettings();

    public bool TryGetExceptionState(string fullName, out enum_EXCEPTION_STATE state)
        => session.TryGetExceptionState(fullName, out state);
}
