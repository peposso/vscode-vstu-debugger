using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class FrameInfo
{
    public required string File { get; set; }
    public required int Line { get; set; }
    public required FRAMEINFO Info { get; set; }
    public required IDebugStackFrame2 Frame { get; set; }
    public required int ThreadId { get; set; }
}
