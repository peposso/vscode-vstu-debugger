using SyntaxTree.VisualStudio.Unity.Debugger;

namespace VstuBridgeDebugAdaptor.Vstu;

[GenerateImplement]
public sealed partial class DebugEngineHost : IDebugEngineHost
{
    DebuggerSession session;

    public DebugEngineHost()
    {
        session = DebuggerSession.Instance ?? throw new InvalidOperationException("DebuggerSession.Instance is null");
    }
}
