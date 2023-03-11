using System.Text;
using VstuBridgeDebugAdaptor.Vstu;
using System.Globalization;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using System.Reflection;
using VstuBridgeDebugAdaptor.Adapter;
using Newtonsoft.Json.Linq;

_ = typeof(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree);

var port = 0;
var logFile = "";
var version = false;

var argQueue = new Queue<string>(args);
while (argQueue.Count > 0)
{
    var arg = argQueue.Dequeue();
    switch (arg)
    {
        case "--port":
            port = int.Parse(argQueue.Dequeue(), CultureInfo.InvariantCulture);
            break;
        case "--log":
            logFile = argQueue.Dequeue();
            break;
        case "--version":
            version = true;
            break;
        default:
            throw new ArgumentException($"Unknown argument: {arg}");
    }
}

if (logFile == "" && port == 0)
{
    var exePath = Assembly.GetExecutingAssembly().Location;
    logFile = Path.ChangeExtension(exePath, ".log");
}

var logWriter = Console.Error;
if (logFile is not "" and not "-")
{
    var fs = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
    logWriter = new StreamWriter(fs, Encoding.UTF8, 1024, true);
}

var adapters = new ConcurrentDictionary<int, VstuDebugAdapter>();
Console.CancelKeyPress += (sender, e) =>
{
    foreach (var (_, adapter) in adapters)
        adapter?.Terminate();
};

#if NET7_0_OR_GREATER
var pid = Environment.ProcessId;
#else
var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
logWriter.WriteLine($"ProcessId: {pid}");
logWriter.WriteLine($"Using: {typeof(SyntaxTree.VisualStudio.Unity.Debugger.UnityEngine).Assembly.FullName}");
logWriter.WriteLine($"Using: {typeof(SyntaxTree.VisualStudio.Unity.Messaging.UnityProcess).Assembly.FullName}");
logWriter.WriteLine($"Using: {typeof(Mono.Debugger.Soft.VirtualMachine).Assembly.FullName}");

if (version)
{
    var packageVersion = JObject.Parse(File.ReadAllText("package.json"))?["version"]?.ToString();
    Console.WriteLine(packageVersion ?? "0.0.0");
    Environment.Exit(0);
}

if (port == 0)  // stdin/stdout
{
    var adapter = new VstuDebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), logWriter);
    adapters.TryAdd(0, adapter);
    adapter.Protocol.Run();
    adapter.Terminate();
    return;
}

logWriter.WriteLine($"Waiting for connections on port {port}...");
logWriter.Flush();

var listenThread = new Thread(() =>
{
    var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
    listener.Start();

    while (true)
    {
        var clientSocket = listener.AcceptSocket();
        var clientThread = new Thread(() =>
        {
            logWriter.WriteLine("Accepted connection");
            logWriter.Flush();

            using var stream = new NetworkStream(clientSocket);
            var adapter = new VstuDebugAdapter(stream, stream, logWriter);
            adapters.TryAdd(Environment.CurrentManagedThreadId, adapter);

            adapter.Protocol.Run();
            adapter.Protocol.WaitForReader();
            adapter.Terminate();

            adapters.TryRemove(Environment.CurrentManagedThreadId, out _);
            adapter = null;

            logWriter.WriteLine("Connection closed");
            logWriter.Flush();
        });

        clientThread.Name = "DebugServer connection thread";
        clientThread.Start();
    }
});

listenThread.Name = "DebugServer listener thread";
listenThread.Start();
listenThread.Join();
