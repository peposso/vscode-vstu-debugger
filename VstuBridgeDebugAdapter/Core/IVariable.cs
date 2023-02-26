namespace VstuBridgeDebugAdaptor.Core;

interface IVariable
{
    string Name { get; }

    string Value { get; }

    int VariablesReference { get; }
}
