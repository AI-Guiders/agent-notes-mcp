using System.Text.Json;

namespace AgentNotesMcp.Status;

internal static class AgentNotesStatusRuntimeFile
{
    internal static void TryWrite(string workspacePath, string statusUrl, string configPath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        try
        {
            var dir = Path.Combine(Path.GetFullPath(workspacePath.Trim()), ".cascade-ide");
            Directory.CreateDirectory(dir);
            var payload = new
            {
                pid = Environment.ProcessId,
                port = new Uri(statusUrl).Port,
                url = statusUrl,
                config_source = configPath,
                written_at_utc = DateTimeOffset.UtcNow.ToString("O")
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(dir, "agent-notes-status.runtime.json"), json);
        }
        catch
        {
            // Best-effort; status HTTP must not fail if runtime file cannot be written.
        }
    }
}
