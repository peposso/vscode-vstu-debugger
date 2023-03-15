using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Vstu;

internal class DebugFunctionPosition : IDebugFunctionPosition2
{
    public DebugFunctionPosition(string functionName)
    {
        this.FunctionName = functionName;
    }

    public string FunctionName { get; }

    public int GetFunctionName(out string pbstrFunctionName)
    {
        pbstrFunctionName = FunctionName;
        return 0;
    }

    public int GetOffset(TEXT_POSITION[] pPosition)
    {
        throw new NotImplementedException();
    }
}
