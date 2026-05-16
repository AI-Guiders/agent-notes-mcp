using System.Net;

namespace AgentNotesMcp.Status;

internal static class AgentNotesStatusBind
{
    internal const string Loopback = "127.0.0.1";

    internal static (string Host, string? Warning) Resolve(string? configuredBind)
    {
        var bind = string.IsNullOrWhiteSpace(configuredBind) ? Loopback : configuredBind.Trim();
        if (bind is Loopback or "localhost" or "::1")
            return (Loopback, null);

        return (Loopback, $"Status bind '{bind}' is not allowed in v1; using {Loopback} (loopback only).");
    }

    internal static string BuildBaseUrl(string host, int port) =>
        $"http://{host}:{port}";
}
