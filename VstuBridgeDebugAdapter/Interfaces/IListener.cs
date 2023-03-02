namespace VstuBridgeDebugAdaptor.Interfaces;

interface IListener
{
    void OnDebuggerConnected();

    void OnDebuggerConnectionFailed(Exception? e);

    void OnLoadCompleted();

    void OnOutput(string s);

    void OnThreadStarted(int threadId);

    void OnThreadExited(int threadId);

    void OnAssemblyLoaded(string assemblyUri);

    void OnSessionTermination(Exception? e);

    void OnBreakpointVerified(string file, int line, int column, bool verified);

    void OnStoppedByPause(int threadId);

    void OnStoppedByStep(int threadId);

    void OnStoppedByBreakpoint(int threadId, string file, int line, int column);
}
