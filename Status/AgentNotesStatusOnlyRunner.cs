using AgentNotes.Core;

namespace AgentNotesMcp.Status;

internal static class AgentNotesStatusOnlyRunner
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var startupCode = AgentNotesBootstrap.TryLoadSettings(args, out var localSettings, out var startupError);
        if (startupCode != 0)
        {
            Console.Error.WriteLine(startupError);
            return startupCode;
        }

        AgentNotesRuntime.Initialize(localSettings!, AgentNotesBootstrap.LoadedConfigPath);

        if (!localSettings!.Status.Enabled)
        {
            Console.Error.WriteLine("agent-notes-mcp --status-only requires [status].enabled = true in --config TOML.");
            return AgentNotesBootstrap.ExitInvalidConfig;
        }

        var storage = new NotesStorage();
        var startedAt = DateTimeOffset.UtcNow;
        var (bindHost, bindWarning) = AgentNotesStatusBind.Resolve(localSettings.Status.Bind);
        var plannedUrl = AgentNotesStatusBind.BuildBaseUrl(bindHost, localSettings.Status.Port);
        var host = new AgentNotesStatusHost(storage, startedAt, plannedUrl, bindWarning);

        string? statusUrl;
        string? error;
        try
        {
            if (!host.TryStartForeground(out statusUrl, out error))
            {
                Console.Error.WriteLine($"AgentNotesStatus: failed to start ({error}).");
                return AgentNotesBootstrap.ExitInvalidConfig;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AgentNotesStatus: failed to start ({ex.Message}).");
            return AgentNotesBootstrap.ExitInvalidConfig;
        }

        Console.Error.WriteLine($"AgentNotesStatus (--status-only): {statusUrl}");
        Console.Error.WriteLine("Press Ctrl+C to stop.");

        if (localSettings.Status.PreviewWorkspace is { } preview && statusUrl is not null)
            AgentNotesStatusRuntimeFile.TryWrite(preview, statusUrl, AgentNotesBootstrap.LoadedConfigPath ?? "");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }

        return 0;
    }
}
