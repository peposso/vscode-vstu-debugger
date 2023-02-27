using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace VstuBridgeDebugAdaptor.Helpers;

static class UnityDiscoveryHelper
{
    public static (string address, int port, int processId) GetConnectionInfo(string? projectPath)
    {
        if (projectPath is not null)
        {
            var editorInstanceJsonPath = Path.Combine(projectPath, "Library", "EditorInstance.json");
            if (!File.Exists(editorInstanceJsonPath))
            {
                throw new FileNotFoundException(editorInstanceJsonPath);
            }

            var editorInstance = JsonNode.Parse(File.ReadAllText(editorInstanceJsonPath))
                                    ?? throw new InvalidDataException(editorInstanceJsonPath);
            var processIdValue = editorInstance["process_id"]?.ToString();
            if (string.IsNullOrEmpty(processIdValue))
            {
                throw new ArgumentException("missing processId");
            }

            // https://github.com/Unity-Technologies/MonoDevelop.Debugger.Soft.Unity/blob/7a99cf7c707d1d60e968c42a9aec8a55413e5deb/UnityProcessDiscovery.cs#L81
            var processId = int.Parse(processIdValue, CultureInfo.InvariantCulture);
            var port = 56000 + processId % 1000;
            var address = IPAddress.Loopback;
            return (address.ToString(), port, processId);
        }

        throw new NotSupportedException("'projectPath' is needed");
    }
}
