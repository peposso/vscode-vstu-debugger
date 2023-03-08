using System.Globalization;
using System.Net;
using Newtonsoft.Json.Linq;

namespace VstuBridgeDebugAdaptor.Helpers;

static class UnityDiscoveryHelper
{
    public static (string address, int port, int processId) GetConnectionInfo(string? projectPath)
    {
        if (projectPath is not null)
        {
            if (!Directory.Exists(projectPath))
            {
                throw new FileNotFoundException($"'{projectPath}' directory does not exist.");
            }

            var editorInstanceJsonPath = Path.Combine(projectPath, "Library", "EditorInstance.json");
            if (!File.Exists(editorInstanceJsonPath))
            {
                throw new FileNotFoundException($"Unity Editor not running at '{projectPath}'");
            }

            var editorInstance = JObject.Parse(File.ReadAllText(editorInstanceJsonPath))
                                    ?? throw new InvalidDataException(editorInstanceJsonPath);
            var processIdValue = editorInstance["process_id"]?.ToString();
            if (string.IsNullOrEmpty(processIdValue))
            {
                throw new InvalidDataException("EditorInstance.json is invalid.");
            }

            var processId = int.Parse(processIdValue, CultureInfo.InvariantCulture);
            var unityProcess = System.Diagnostics.Process.GetProcessesByName("Unity").FirstOrDefault(p => p.Id == processId);
            if (unityProcess is null)
            {
                throw new InvalidOperationException($"Unity Editor (pid:{processId}) is not running at '{projectPath}'.");
            }

            // https://github.com/Unity-Technologies/MonoDevelop.Debugger.Soft.Unity/blob/7a99cf7c707d1d60e968c42a9aec8a55413e5deb/UnityProcessDiscovery.cs#L81
            var port = 56000 + processId % 1000;
            var address = IPAddress.Loopback;
            return (address.ToString(), port, processId);
        }

        throw new NotSupportedException("'projectPath' is needed");
    }
}
