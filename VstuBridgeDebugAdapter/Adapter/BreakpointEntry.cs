using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using VstuBridgeDebugAdaptor.Interfaces;

namespace VstuBridgeDebugAdaptor.Adapter;

sealed record BreakpointEntry
{
    public BreakpointEntry(SourceBreakpoint source, int id)
    {
        Id = id;
        Line = source.Line;
        Column = source.Column ?? 0;
        Condition = source.Condition;
        (HitCount, HitCondition) = ParseHitCondition(source.HitCondition);
    }

    public BreakpointEntry(FunctionBreakpoint function, int id)
    {
        Id = id;
        Name = function.Name;
        Condition = function.Condition;
        (HitCount, HitCondition) = ParseHitCondition(function.HitCondition);
    }

    public int Id { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string Name { get; init; } = "";
    public bool Verified { get; set; }
    public string Condition { get; set; } = "";
    public int HitCount { get; set; }
    public HitConditionKind HitCondition { get; set; }

    static readonly Regex Regex = new(@"^(?<kind>=|==|>|>=|%)?\s*(?<count>\d+)$", RegexOptions.Compiled);

    internal bool EqualsPosition(BreakpointEntry other)
    {
        if (other is null
            || Line != other.Line
            || Column != other.Column
            || Name != other.Name)
            return false;

        return true;
    }

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

    internal bool IsConditionChanged(BreakpointEntry other)
    {
        if (other.Condition != Condition
            || other.HitCount != HitCount
            || other.HitCondition != HitCondition)
            return true;

        return false;
    }

    internal bool IsConditionChanged(FunctionBreakpoint source)
    {
        if ((source.Condition ?? "") != (Condition ?? ""))
            return true;

        if (ParseHitCondition(source.HitCondition) != (HitCount, HitCondition))
            return true;

        return false;
    }

    internal Breakpoint ToResponse()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            return new()
            {
                Id = Id,
                Verified = Verified,
            };
        }

        return new()
        {
            Id = Id,
            Line = Line,
            Column = Column,
            Verified = Verified,
        };
    }
}
