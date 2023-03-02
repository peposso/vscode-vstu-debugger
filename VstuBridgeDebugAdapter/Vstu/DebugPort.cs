using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class DebugPort : IDebugDefaultPort2, IDebugPort2
{
    readonly IDebugPortNotify2 portNotify;

    readonly Dictionary<AD_PROCESS_ID, DebugProcess> processes = new();

    public DebugPort(IDebugPortNotify2 portNotify)
    {
        this.portNotify = portNotify;
    }

    public int GetPortName(out string pbstrName) => throw new NotImplementedException();

    public int GetPortId(out Guid pguidPort) => throw new NotImplementedException();

    public int GetPortRequest(out IDebugPortRequest2 ppRequest) => throw new NotImplementedException();

    public int GetPortSupplier(out IDebugPortSupplier2 ppSupplier) => throw new NotImplementedException();

    public int GetProcess(AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess)
    {
        if (!processes.TryGetValue(ProcessId, out var value))
        {
            value = new(this, ProcessId);
            processes.Add(ProcessId, value);
        }

        ppProcess = value;
        return 0;
    }

    public int EnumProcesses(out IEnumDebugProcesses2 ppEnum) => throw new NotImplementedException();

    public int GetPortNotify(out IDebugPortNotify2 ppPortNotify)
    {
        ppPortNotify = portNotify;
        return 0;
    }

    public int GetServer(out IDebugCoreServer3 ppServer) => throw new NotImplementedException();

    public int QueryIsLocal() => throw new NotImplementedException();
}
