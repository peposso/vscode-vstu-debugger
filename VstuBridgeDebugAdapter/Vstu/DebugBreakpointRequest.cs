using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;
using VstuBridgeDebugAdaptor.Interfaces;

namespace VstuBridgeDebugAdaptor.Vstu;

sealed class DebugBreakpointRequest : IDebugBreakpointRequest2
{
    readonly object position;
    readonly string condition;
    readonly int hitCount;
    readonly HitConditionKind hitCondition;

    public DebugBreakpointRequest(DebugDocumentPosition position, string condition, int hitCount, HitConditionKind hitCondition)
    {
        this.position = position;
        this.condition = condition;
        this.hitCount = hitCount;
        this.hitCondition = hitCondition;
        LocationType = enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE;
    }

    public DebugBreakpointRequest(DebugFunctionPosition position, string condition, int hitCount, HitConditionKind hitCondition)
    {
        this.position = position;
        this.condition = condition;
        this.hitCount = hitCount;
        this.hitCondition = hitCondition;
        LocationType = enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET;
    }

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

        if (!string.IsNullOrEmpty(condition))
        {
            info.bpCondition = new BP_CONDITION
            {
                bstrCondition = condition,
                styleCondition = enum_BP_COND_STYLE.BP_COND_WHEN_TRUE,
            };
        }

        if (hitCondition is not HitConditionKind.None)
        {
            info.bpPassCount = new BP_PASSCOUNT
            {
                dwPassCount = (uint)hitCount + (hitCondition is HitConditionKind.GreaterThan ? 1u : 0u),
                stylePassCount = hitCondition switch
                {
                    HitConditionKind.Equal => enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL,
                    HitConditionKind.GreaterThan => enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER,
                    HitConditionKind.GreaterThanOrEqual => enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER,
                    HitConditionKind.Modular => enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD,
                    _ => enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL,
                },
            };
        }

        pBPRequestInfo[0] = info;
        return 0;
    }
}
