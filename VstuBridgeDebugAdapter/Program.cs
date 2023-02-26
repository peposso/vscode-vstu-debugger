using System.Text;
using VstuBridgeDebugAdaptor.Core;
using System.Globalization;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using System.Reflection;

var port = 0;
var logFile = "";

var argQueue = new Queue<string>(args);
while (argQueue.Count > 0)
{
    var arg = argQueue.Dequeue();
    switch (arg)
    {
        case "--port":
            port = int.Parse(argQueue.Dequeue(), CultureInfo.InvariantCulture);
            break;
        case "--log-file":
            logFile = argQueue.Dequeue();
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
if (logFile != "")
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

if (port == 0)
{
    var adapter = new VstuDebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), logWriter);
    adapters.TryAdd(0, adapter);
    adapter.Protocol.Run();
    adapter.Terminate();
    return;
}

logWriter.WriteLine($"ProcessId: {Environment.ProcessId}");
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
