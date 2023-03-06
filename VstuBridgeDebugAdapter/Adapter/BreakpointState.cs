using System.Globalization;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace VstuBridgeDebugAdaptor.Adapter;

sealed class BreakpointState
{
    public required int Id { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public bool Verified { get; set; }
    public string Condition { get; set; } = "";
    public int HitCount { get; set; }

    internal bool IsConditionChanged(SourceBreakpoint source)
    {
        if ((source.Condition ?? "") != (Condition ?? ""))
            return true;

        var sourceHitCount = string.IsNullOrEmpty(source.HitCondition)
                                ? 0 : int.Parse(source.HitCondition, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (sourceHitCount != HitCount)
            return true;

        return false;
    }

    internal Breakpoint ToResponse()
    {
        return new()
        {
            Id = Id,
            Line = Line,
            Column = Column,
            Verified = Verified,
        };
    }
}
