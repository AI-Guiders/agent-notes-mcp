using System.Reflection;
using System.Text.Json;
using AgentNotes.Core;

namespace AgentNotesMcp.Status;

internal sealed class AgentNotesStatusSnapshot
{
    internal required string McpVersion { get; init; }

    internal required int ProcessId { get; init; }

    internal required long UptimeSeconds { get; init; }

    internal required DateTimeOffset StartedAtUtc { get; init; }

    internal required string ConfigPath { get; init; }

    internal required string StatusUrl { get; init; }

    internal required string? BindWarning { get; init; }

    internal required KnowledgeBlock Knowledge { get; init; }

    internal required WorkspaceBlock Workspace { get; init; }

    internal required IReadOnlyList<ToolSummary> Tools { get; init; }

    internal required IReadOnlyList<RecentToolCall> RecentToolCalls { get; init; }

    internal static AgentNotesStatusSnapshot Create(
        NotesStorage storage,
        DateTimeOffset startedAt,
        string statusUrl,
        string? bindWarning,
        string? workspacePath,
        bool verbose)
    {
        var settings = AgentNotesRuntime.Settings;
        var configPath = AgentNotesRuntime.ConfigFilePath ?? "(unknown)";
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        var uptime = (long)Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);

        var previewWorkspace = settings.Status.PreviewWorkspace;
        var effectiveWorkspace = !string.IsNullOrWhiteSpace(workspacePath)
            ? Path.GetFullPath(workspacePath.Trim())
            : previewWorkspace;

        MemoryHealthBlock? memory = null;
        if (!string.IsNullOrWhiteSpace(effectiveWorkspace))
            memory = MemoryHealthBlock.TryParse(storage.MemoryHealth(effectiveWorkspace, activeScope: null));

        return new AgentNotesStatusSnapshot
        {
            McpVersion = version,
            ProcessId = Environment.ProcessId,
            UptimeSeconds = uptime,
            StartedAtUtc = startedAt,
            ConfigPath = configPath,
            StatusUrl = statusUrl,
            BindWarning = bindWarning,
            Knowledge = KnowledgeBlock.FromSettings(settings, verbose),
            Workspace = WorkspaceBlock.FromSettings(settings, effectiveWorkspace, previewWorkspace, memory),
            Tools = ToolCatalog.ListSummaries(),
            MemoryHealth = memory,
            RecentToolCalls = AgentNotesToolCallRingBuffer.Snapshot()
                .Select(e => RecentToolCall.From(e))
                .ToArray()
        };
    }

    internal MemoryHealthBlock? MemoryHealth { get; init; }

    internal void WriteJson(Utf8JsonWriter writer, bool verbose)
    {
        writer.WriteStartObject();
        writer.WriteString("mcp_version", McpVersion);
        writer.WriteNumber("pid", ProcessId);
        writer.WriteNumber("uptime_seconds", UptimeSeconds);
        writer.WriteString("started_at_utc", StartedAtUtc.ToString("O"));
        writer.WriteString("config_path", ConfigPath);
        writer.WriteString("status_url", StatusUrl);
        if (BindWarning is not null)
            writer.WriteString("bind_warning", BindWarning);

        Knowledge.WriteTo(writer, verbose);
        Workspace.WriteTo(writer, verbose);
        MemoryHealth?.WriteTo(writer, verbose);

        writer.WritePropertyName("tools");
        writer.WriteStartArray();
        foreach (var tool in Tools)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("recent_tool_calls");
        writer.WriteStartArray();
        foreach (var call in RecentToolCalls)
            call.WriteTo(writer, verbose);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    internal sealed record RecentToolCall(
        DateTimeOffset AtUtc,
        string ToolName,
        string? WorkspacePath,
        bool IsError,
        long DurationMs,
        string ResultPreview)
    {
        internal static RecentToolCall From(AgentNotesToolCallRingBuffer.ToolCallEntry e) =>
            new(e.AtUtc, e.ToolName, e.WorkspacePath, e.IsError, e.DurationMs, e.ResultPreview);

        internal void WriteTo(Utf8JsonWriter writer, bool verbose)
        {
            writer.WriteStartObject();
            writer.WriteString("at_utc", AtUtc.ToString("O"));
            writer.WriteString("tool", ToolName);
            if (WorkspacePath is not null)
                writer.WriteString("workspace_path", verbose ? WorkspacePath : SummarizePath(WorkspacePath));
            writer.WriteBoolean("is_error", IsError);
            writer.WriteNumber("duration_ms", DurationMs);
            writer.WriteString("result_preview", ResultPreview);
            writer.WriteEndObject();
        }
    }

    internal sealed class KnowledgeBlock
    {
        internal required string PrimaryRoot { get; init; }

        internal required string NotesPath { get; init; }

        internal required bool NotesExists { get; init; }

        internal required IReadOnlyList<NamedRoot> NamedRoots { get; init; }

        internal required IReadOnlyList<ReadOnlyRoot> ReadOnlyRoots { get; init; }

        internal required bool ReadOnlyRoutingEnabled { get; init; }

        internal static KnowledgeBlock FromSettings(LocalSettings settings, bool verbose)
        {
            var primary = settings.PrimaryKnowledgeRoot;
            var notesPath = Path.Combine(primary, "agent-notes.md");
            var named = settings.KnowledgeRoots
                .Select(kv => new NamedRoot(kv.Key, kv.Value, Directory.Exists(kv.Value)))
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .ToArray();

            var readOnly = settings.ReadOnlyKnowledgeRoots
                .Select(r => new ReadOnlyRoot(r.Id, r.Path, Directory.Exists(r.Path)))
                .ToArray();

            return new KnowledgeBlock
            {
                PrimaryRoot = verbose ? primary : SummarizePath(primary),
                NotesPath = verbose ? notesPath : SummarizePath(notesPath),
                NotesExists = File.Exists(notesPath),
                NamedRoots = named,
                ReadOnlyRoots = readOnly,
                ReadOnlyRoutingEnabled = readOnly.Length > 0
            };
        }

        internal void WriteTo(Utf8JsonWriter writer, bool verbose)
        {
            writer.WritePropertyName("knowledge");
            writer.WriteStartObject();
            writer.WriteString("primary_root", PrimaryRoot);
            writer.WriteString("notes_path", NotesPath);
            writer.WriteBoolean("notes_exists", NotesExists);
            writer.WriteBoolean("read_only_routing_enabled", ReadOnlyRoutingEnabled);

            writer.WritePropertyName("named_roots");
            writer.WriteStartArray();
            foreach (var root in NamedRoots)
            {
                writer.WriteStartObject();
                writer.WriteString("id", root.Id);
                writer.WriteString("path", verbose ? root.Path : SummarizePath(root.Path));
                writer.WriteBoolean("exists", root.Exists);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WritePropertyName("read_only_roots");
            writer.WriteStartArray();
            foreach (var root in ReadOnlyRoots)
            {
                writer.WriteStartObject();
                writer.WriteString("id", root.Id);
                writer.WriteString("path", verbose ? root.Path : SummarizePath(root.Path));
                writer.WriteBoolean("exists", root.Exists);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    internal sealed record NamedRoot(string Id, string Path, bool Exists);

    internal sealed record ReadOnlyRoot(string Id, string Path, bool Exists);

    internal sealed class WorkspaceBlock
    {
        internal required string? EffectiveWorkspace { get; init; }

        internal required string? PreviewWorkspace { get; init; }

        internal required string DefaultScope { get; init; }

        internal required string ScopeMapRelative { get; init; }

        internal required string ScopeAliasMapRelative { get; init; }

        internal required bool ScopeMapExists { get; init; }

        internal required bool ScopeAliasMapExists { get; init; }

        internal required string? ResolvedScope { get; init; }

        internal static WorkspaceBlock FromSettings(
            LocalSettings settings,
            string? effectiveWorkspace,
            string? previewWorkspace,
            MemoryHealthBlock? memory)
        {
            var primary = settings.PrimaryKnowledgeRoot;
            var scopeMapPath = Path.Combine(primary, "knowledge", settings.Workspace.ScopeMapRelative);
            var aliasPath = Path.Combine(primary, "knowledge", settings.Workspace.ScopeAliasMapRelative);

            return new WorkspaceBlock
            {
                EffectiveWorkspace = effectiveWorkspace,
                PreviewWorkspace = previewWorkspace,
                DefaultScope = settings.Workspace.DefaultScope,
                ScopeMapRelative = settings.Workspace.ScopeMapRelative,
                ScopeAliasMapRelative = settings.Workspace.ScopeAliasMapRelative,
                ScopeMapExists = File.Exists(scopeMapPath),
                ScopeAliasMapExists = File.Exists(aliasPath),
                ResolvedScope = memory?.ResolvedScope
            };
        }

        internal void WriteTo(Utf8JsonWriter writer, bool verbose)
        {
            writer.WritePropertyName("workspace");
            writer.WriteStartObject();
            if (EffectiveWorkspace is not null)
                writer.WriteString("effective_path", verbose ? EffectiveWorkspace : SummarizePath(EffectiveWorkspace));
            if (PreviewWorkspace is not null)
                writer.WriteString("preview_path", verbose ? PreviewWorkspace : SummarizePath(PreviewWorkspace));
            writer.WriteString("default_scope", DefaultScope);
            writer.WriteString("scope_map", ScopeMapRelative);
            writer.WriteBoolean("scope_map_exists", ScopeMapExists);
            writer.WriteString("scope_aliases", ScopeAliasMapRelative);
            writer.WriteBoolean("scope_aliases_exists", ScopeAliasMapExists);
            if (ResolvedScope is not null)
                writer.WriteString("resolved_scope", ResolvedScope);
            writer.WriteEndObject();
        }
    }

    internal sealed class MemoryHealthBlock
    {
        internal required string HealthLevel { get; init; }

        internal required string ResolvedScope { get; init; }

        internal required string NotesPath { get; init; }

        internal required bool NotesExists { get; init; }

        internal required int HotChars { get; init; }

        internal required int HotLines { get; init; }

        internal required int SectionCount { get; init; }

        internal required IReadOnlyList<string> Warnings { get; init; }

        internal required IReadOnlyList<string> Recommendations { get; init; }

        internal required IReadOnlyList<string> HotSectionIds { get; init; }

        internal static MemoryHealthBlock? TryParse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var hot = root.GetProperty("hot_context");
                var sectionIds = hot.TryGetProperty("section_ids", out var ids)
                    ? ids.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : Array.Empty<string>();

                return new MemoryHealthBlock
                {
                    HealthLevel = root.GetProperty("health_level").GetString() ?? "unknown",
                    ResolvedScope = root.GetProperty("resolved_scope").GetString() ?? "",
                    NotesPath = root.GetProperty("notes_path").GetString() ?? "",
                    NotesExists = root.TryGetProperty("notes_exists", out var ne) && ne.GetBoolean(),
                    HotChars = hot.GetProperty("chars").GetInt32(),
                    HotLines = hot.GetProperty("lines").GetInt32(),
                    SectionCount = root.GetProperty("section_count").GetInt32(),
                    Warnings = ReadStringArray(root, "warnings"),
                    Recommendations = ReadStringArray(root, "recommendations"),
                    HotSectionIds = sectionIds
                };
            }
            catch
            {
                return null;
            }
        }

        internal void WriteTo(Utf8JsonWriter writer, bool verbose)
        {
            writer.WritePropertyName("memory_health");
            writer.WriteStartObject();
            writer.WriteString("health_level", HealthLevel);
            writer.WriteString("resolved_scope", ResolvedScope);
            writer.WriteString("notes_path", verbose ? NotesPath : SummarizePath(NotesPath));
            writer.WriteBoolean("notes_exists", NotesExists);
            writer.WriteNumber("hot_chars", HotChars);
            writer.WriteNumber("hot_lines", HotLines);
            writer.WriteNumber("section_count", SectionCount);
            WriteStringArray(writer, "warnings", Warnings);
            WriteStringArray(writer, "recommendations", Recommendations);
            WriteStringArray(writer, "hot_section_ids", HotSectionIds);
            writer.WriteEndObject();
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return arr.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToArray();
        }
    }

    internal sealed record ToolSummary(string Name, string Description);

    private static string SummarizePath(string path)
    {
        if (path.Length <= 64)
            return path;

        var file = Path.GetFileName(path);
        var root = Path.GetPathRoot(path) ?? "";
        var tail = path.Length > root.Length + 20
            ? "…" + path[^36..]
            : path;
        return string.IsNullOrEmpty(file) ? tail : $"{root}…{file}";
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
