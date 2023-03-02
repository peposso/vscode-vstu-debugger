namespace VstuBridgeDebugAdaptor.Interfaces;

record VariableDto(string Name, string Value, int VariablesReference)
{
    public static VariableDto Empty { get; } = new("", "", 0);
}
