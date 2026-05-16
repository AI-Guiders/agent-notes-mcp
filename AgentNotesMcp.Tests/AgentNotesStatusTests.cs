using System.Net;
using System.Text.Json;
using AgentNotes.Core;
using AgentNotesMcp.Status;

namespace AgentNotesMcp.Tests;

public sealed class AgentNotesStatusTests
{
    [Fact]
    public void Bind_resolve_forces_loopback_for_non_loopback_config()
    {
        var (host, warning) = AgentNotesStatusBind.Resolve("0.0.0.0");
        Assert.Equal(AgentNotesStatusBind.Loopback, host);
        Assert.NotNull(warning);
    }

    [Fact]
    public void Html_renderer_includes_version_and_config()
    {
        using var scope = InstallStatusFixture(enabled: false, port: 17341, out _);
        var snapshot = AgentNotesStatusSnapshot.Create(
            new NotesStorage(),
            DateTimeOffset.UtcNow,
            "http://127.0.0.1:17341",
            bindWarning: null,
            workspacePath: null,
            verbose: false);

        var html = AgentNotesStatusHtmlRenderer.Render(snapshot, workspaceQuery: null);

        Assert.Contains("agent-notes-mcp", html, StringComparison.Ordinal);
        Assert.Contains("2.0.0", html, StringComparison.Ordinal);
        Assert.Contains("memory_health", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tools-strip", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/tools\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<ul class=\"tools\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_page_renders_catalog_table()
    {
        var tools = ToolCatalog.ListSummaries();
        var html = AgentNotesStatusHtmlRenderer.RenderToolsPage(tools);

        Assert.Contains("tools-catalog", html, StringComparison.Ordinal);
        Assert.Contains("<code>memory_health</code>", html, StringComparison.Ordinal);
        var memoryHealth = tools.First(t => t.Name == "memory_health");
        Assert.Contains(memoryHealth.Description, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Ring_buffer_keeps_at_most_default_capacity()
    {
        AgentNotesToolCallRingBuffer.ClearForTests();
        var args = new Dictionary<string, JsonElement>
        {
            ["workspace_path"] = JsonSerializer.SerializeToElement("D:/ws")
        };

        for (var i = 0; i < AgentNotesToolCallRingBuffer.DefaultCapacity + 10; i++)
            AgentNotesToolCallRingBuffer.Record("read_agent_notes", args, $"ok-{i}", false, i);

        var snap = AgentNotesToolCallRingBuffer.Snapshot();
        Assert.Equal(AgentNotesToolCallRingBuffer.DefaultCapacity, snap.Count);
        Assert.Equal("read_agent_notes", snap[0].ToolName);
        Assert.Contains("ok-73", snap[0].ResultPreview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hot_preview_returns_section_sizes()
    {
        var port = GetFreeTcpPort();
        using var scope = InstallStatusFixture(enabled: true, port, out var knowledgeRoot);
        var workspace = Path.Combine(Path.GetTempPath(), "AgentNotesStatusTests", "ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        File.WriteAllText(
            Path.Combine(knowledgeRoot, "agent-notes.md"),
            """
            <!-- section:active-scope -->
            mixed
            <!-- /section:active-scope -->
            <!-- section:door-to-singularity -->
            x
            <!-- /section:door-to-singularity -->
            """);

        var storage = new NotesStorage();
        Assert.True(AgentNotesStatusHost.TryStartBackground(storage, DateTimeOffset.UtcNow, out var host, out var url, out _));
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await client.GetStringAsync($"{url}/hot-preview?workspace_path={Uri.EscapeDataString(workspace)}");
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("hot_context").GetProperty("chars").GetInt32() > 0);
            Assert.True(doc.RootElement.GetProperty("sections").GetArrayLength() > 0);
        }
        finally
        {
            await host!.StopAsync();
        }
    }

    [Fact]
    public async Task Status_host_health_and_json_when_enabled()
    {
        var port = GetFreeTcpPort();
        using var scope = InstallStatusFixture(enabled: true, port, out var knowledgeRoot);
        var storage = new NotesStorage();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.True(AgentNotesStatusHost.TryStartBackground(storage, startedAt, out var host, out var url, out var error));
        Assert.Null(error);
        Assert.NotNull(host);
        Assert.NotNull(url);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var health = await client.GetStringAsync($"{url}/health");
            Assert.Equal("OK", health);

            var toolsHtml = await client.GetStringAsync($"{url}/tools");
            Assert.Contains("tools-catalog", toolsHtml, StringComparison.Ordinal);

            var json = await client.GetStringAsync($"{url}/status.json?verbose=1");
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("2.0.0", doc.RootElement.GetProperty("mcp_version").GetString());
            Assert.True(doc.RootElement.GetProperty("knowledge").GetProperty("primary_root").GetString()?.Contains("AgentNotesStatusTests", StringComparison.Ordinal) == true
                || doc.RootElement.GetProperty("knowledge").GetProperty("primary_root").GetString()?.Length > 0);
        }
        finally
        {
            await host!.StopAsync();
        }
    }

    private static AgentNotesTestToml.RuntimeScope InstallStatusFixture(bool enabled, int port, out string knowledgeRoot)
    {
        knowledgeRoot = Path.Combine(Path.GetTempPath(), "AgentNotesStatusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(knowledgeRoot, "knowledge", "work", "local"));
        File.WriteAllText(Path.Combine(knowledgeRoot, "agent-notes.md"), "<!-- section:active-scope -->\n<!-- /section:active-scope -->\n");
        File.WriteAllText(Path.Combine(knowledgeRoot, "knowledge", "work", "local", "workspace-scope-map-v1.md"), "# map\n");

        var toml = $"""
            version = 1
            [knowledge]
            primary = "test"
            [knowledge.roots]
            test = "{knowledgeRoot.Replace('\\', '/')}"
            [workspace]
            default_scope = "door-to-singularity"
            scope_map = "work/local/workspace-scope-map-v1.md"
            scope_aliases = "work/local/scope-alias-map-v1.md"
            [status]
            enabled = {(enabled ? "true" : "false")}
            port = {port}
            bind = "127.0.0.1"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"status-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml);
        var settings = LocalSettingsLoader.Load(path);
        AgentNotesRuntime.Initialize(settings, path);
        return new AgentNotesTestToml.RuntimeScope();
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
