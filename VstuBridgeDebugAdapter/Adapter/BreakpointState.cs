using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using VstuBridgeDebugAdaptor.Interfaces;

namespace VstuBridgeDebugAdaptor.Adapter;

sealed class BreakpointState
{
    public required int Id { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public bool Verified { get; set; }
    public string Condition { get; set; } = "";
    public int HitCount { get; set; }
    public HitConditionKind HitCondition { get; set; }

    static readonly Regex Regex = new(@"^(?<kind>=|==|>|>=|%)?\s*(?<count>\d+)$", RegexOptions.Compiled);

    internal static (int count, HitConditionKind kind) ParseHitCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return (0, HitConditionKind.None);

        condition = condition.Trim();
        var m = Regex.Match(condition);
        if (!m.Success)
            throw new FormatException($"not supported format: {condition}");

        var count = int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture);
        var kind = m.Groups["kind"].Value switch
        {
            "" or "=" or "==" => HitConditionKind.Equal,
            ">" => HitConditionKind.GreaterThan,
            ">=" => HitConditionKind.GreaterThanOrEqual,
            "%" => HitConditionKind.Modular,
            _ => HitConditionKind.None,
        };

        if (count == 0 && kind != HitConditionKind.GreaterThan)
            return (0, HitConditionKind.None);

        return (count, kind);
    }

    internal bool IsConditionChanged(SourceBreakpoint source)
    {
        if ((source.Condition ?? "") != (Condition ?? ""))
            return true;

        if (ParseHitCondition(source.HitCondition) != (HitCount, HitCondition))
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
