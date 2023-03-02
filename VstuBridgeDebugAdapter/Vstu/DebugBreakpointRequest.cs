using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class DebugBreakpointRequest : IDebugBreakpointRequest2
{
    readonly DebugDocumentPosition position;

    public DebugBreakpointRequest(DebugDocumentPosition position)
    {
        this.position = position;
        LocationType = enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE;
    }

    public DebugDocumentPosition Position => position;

    public enum_BP_LOCATION_TYPE LocationType { get; set; }

    public int GetLocationType(enum_BP_LOCATION_TYPE[] pBPLocationType)
    {
        pBPLocationType[0] = LocationType;
        return 0;
    }

    public int GetRequestInfo(enum_BPREQI_FIELDS dwFields, BP_REQUEST_INFO[] pBPRequestInfo)
    {
        var intPtr = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? Marshal.GetIUnknownForObject(position)
                        : GCHandle.ToIntPtr(GCHandle.Alloc(position, GCHandleType.Normal));
        var info = new BP_REQUEST_INFO
        {
            bpLocation = new()
            {
                bpLocationType = (uint)LocationType,
                unionmember2 = intPtr,
            }
        };

        // TODO: Condition, HitCount
        pBPRequestInfo[0] = info;
        return 0;
    }
}
