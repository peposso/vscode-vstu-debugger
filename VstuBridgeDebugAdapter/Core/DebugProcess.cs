using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Core;

sealed class DebugProcess : IDebugProcess2
{
    readonly IDebugPort2 port;

    readonly AD_PROCESS_ID processId;

    public DebugProcess(IDebugPort2 port, AD_PROCESS_ID processId)
    {
        this.port = port;
        this.processId = processId;
    }

    public int GetInfo(enum_PROCESS_INFO_FIELDS fields, PROCESS_INFO[] pProcessInfo) => throw new NotImplementedException();

    public int EnumPrograms(out IEnumDebugPrograms2 ppEnum) => throw new NotImplementedException();

    public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName) => throw new NotImplementedException();

    public int GetServer(out IDebugCoreServer2 ppServer) => throw new NotImplementedException();

    public int Terminate() => throw new NotImplementedException();

    public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach)
        => throw new NotImplementedException();

    public int CanDetach() => throw new NotImplementedException();

    public int Detach() => throw new NotImplementedException();

    public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
    {
        pProcessId[0] = processId;
        return 0;
    }

    public int GetProcessId(out Guid pguidProcessId)
    {
        pguidProcessId = processId.guidProcessId;
        return 0;
    }

    public int GetAttachedSessionName(out string pbstrSessionName) => throw new NotImplementedException();

    public int EnumThreads(out IEnumDebugThreads2 ppEnum) => throw new NotImplementedException();

    public int CauseBreak() => throw new NotImplementedException();

    public int GetPort(out IDebugPort2 ppPort)
    {
        ppPort = port;
        return 0;
    }
}
