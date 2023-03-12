using Microsoft.VisualStudio.Debugger.Interop;
#pragma warning disable CA1822

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class BreakpointInfo
{
    public required string File { get; set; }
    public required int Line { get; set; }
    public required int Column { get; set; }
    public required IDebugPendingBreakpoint2 PendingBreakpoint { get; set; }
    public BreakpointPendingKind Kind { get; set; }
}
