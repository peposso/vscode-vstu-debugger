using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace VstuBridgeDebugAdaptor.Core;

interface IListener
{
    void OnDebuggerConnected();

    void OnDebuggerConnectionFailed(Exception? e);

    void OnLoadCompleted();

    void OnOutput(string s);

    void OnThreadStarted(int threadId);

    void OnThreadExited(int threadId);

    void OnAssemblyLoaded(string assemblyUri);

    void OnBreakpointVerified(string file, int line, int column, bool verified);

    void OnStoppedByPause(int threadId);

    void OnStoppedByStep(int threadId);

    void OnStoppedByBreakpoint(int threadId, string file, int line, int column);
}
