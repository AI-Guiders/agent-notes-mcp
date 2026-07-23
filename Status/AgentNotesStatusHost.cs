using System.Text.Json;
using AgentNotes.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentNotesMcp.Status;

internal sealed class AgentNotesStatusHost
{
    private readonly NotesStorage _storage;
    private readonly DateTimeOffset _startedAt;
    private string _statusUrl;
    private readonly string? _bindWarning;

    internal AgentNotesStatusHost(NotesStorage storage, DateTimeOffset startedAt, string statusUrl, string? bindWarning)
    {
        _storage = storage;
        _startedAt = startedAt;
        _statusUrl = statusUrl;
        _bindWarning = bindWarning;
    }

    internal static bool TryStartBackground(
        NotesStorage storage,
        DateTimeOffset startedAt,
        out AgentNotesStatusHost? host,
        out string? statusUrl,
        out string? error)
    {
        host = null;
        statusUrl = null;
        error = null;

        if (!AgentNotesRuntime.Settings.Status.Enabled)
            return false;

        try
        {
            var (bindHost, bindWarning) = AgentNotesStatusBind.Resolve(AgentNotesRuntime.Settings.Status.Bind);
            statusUrl = AgentNotesStatusBind.BuildBaseUrl(bindHost, AgentNotesRuntime.Settings.Status.Port);
            host = new AgentNotesStatusHost(storage, startedAt, statusUrl, bindWarning);
            host.Start();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal bool TryStartForeground(out string? statusUrl, out string? error)
    {
        statusUrl = null;
        error = null;

        try
        {
            var (bindHost, bindWarning) = AgentNotesStatusBind.Resolve(AgentNotesRuntime.Settings.Status.Bind);
            statusUrl = AgentNotesStatusBind.BuildBaseUrl(bindHost, AgentNotesRuntime.Settings.Status.Port);
            _statusUrl = statusUrl;
            Start();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    private void Start()
    {
        var settings = AgentNotesRuntime.Settings;
        var (bindHost, _) = AgentNotesStatusBind.Resolve(settings.Status.Bind);
        var url = AgentNotesStatusBind.BuildBaseUrl(bindHost, settings.Status.Port);
        _statusUrl = url;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = "AgentNotesMcp",
            Args = Array.Empty<string>()
        });

        builder.WebHost.UseUrls(url);
        builder.Logging.ClearProviders();

        var app = builder.Build();
        _app = app;

        app.MapGet("/health", () => Results.Text("OK", "text/plain"));

        app.MapGet("/status.json", (HttpContext ctx) => WriteJson(ctx));

        app.MapGet("/hot-preview", (HttpContext ctx) => WriteHotPreview(ctx));

        app.MapGet("/tools", () =>
        {
            var html = AgentNotesStatusHtmlRenderer.RenderToolsPage(
                ToolCatalog.ListSummaries()
                    .Select(t => new AgentNotesStatusSnapshot.ToolSummary(t.Name, t.Description))
                    .ToArray());
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapGet("/", (HttpContext ctx) =>
        {
            var workspace = ResolveWorkspaceQuery(ctx);
            var snapshot = BuildSnapshot(workspace, verbose: false);
            var html = AgentNotesStatusHtmlRenderer.Render(snapshot, workspace);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        _cts = new CancellationTokenSource();
        _runTask = StartAndWaitForShutdownAsync(app, _cts.Token);
    }

    private static async Task StartAndWaitForShutdownAsync(WebApplication app, CancellationToken cancellationToken)
    {
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        await ((IHost)app).WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task StopAsync()
    {
        if (_cts is null)
            return;

        try
        {
            await _cts.CancelAsync();
            if (_runTask is not null)
                await _runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            if (_app is not null)
                await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private IResult WriteJson(HttpContext ctx)
    {
        var workspace = ResolveWorkspaceQuery(ctx);
        var verbose = ctx.Request.Query.ContainsKey("verbose");
        var snapshot = BuildSnapshot(workspace, verbose);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            snapshot.WriteJson(writer, verbose);

        return Results.Content(
            System.Text.Encoding.UTF8.GetString(stream.ToArray()),
            "application/json; charset=utf-8");
    }

    private IResult WriteHotPreview(HttpContext ctx)
    {
        var workspace = ResolveWorkspaceQuery(ctx);
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return Results.Json(new
            {
                error = "workspace_path required (query or [status.preview].workspace in TOML)."
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        var json = _storage.HotPreview(workspace, activeScope: null);
        return Results.Content(json, "application/json; charset=utf-8");
    }

    private static string? ResolveWorkspaceQuery(HttpContext ctx)
    {
        var workspace = ctx.Request.Query["workspace_path"].ToString();
        if (!string.IsNullOrWhiteSpace(workspace))
            return Path.GetFullPath(workspace.Trim());

        return AgentNotesRuntime.Settings.Status.PreviewWorkspace;
    }

    private AgentNotesStatusSnapshot BuildSnapshot(string? workspacePath, bool verbose) =>
        AgentNotesStatusSnapshot.Create(
            _storage,
            _startedAt,
            _statusUrl,
            _bindWarning,
            workspacePath,
            verbose);
}
