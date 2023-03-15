using Microsoft.VisualStudio.Debugger.Interop;
#pragma warning disable CA1822

namespace VstuBridgeDebugAdaptor.Vstu;

sealed record BreakpointInfo
{
    public required BreakpointKind Kind { get; set; }
    public string File { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string FunctionName { get; set; } = "";
    public IDebugPendingBreakpoint2 PendingBreakpoint { get; set; } = null!;
    public BreakpointPendingState PendingState { get; set; }
}
