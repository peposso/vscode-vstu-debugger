namespace VstuBridgeDebugAdaptor.Core;

interface IStackFrame
{
    int Id { get; }

    string Name { get; }

    string SourcePath { get; }

    int Line { get; }

    int Column { get; }
}
