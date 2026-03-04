using System.Text.Json;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AgentNotesMcp.Tests;

public sealed class NotesStorageTests
{
    [Fact]
    public void UpsertSection_CreatesAndUpdatesWithoutDuplicates()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "first state"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-current-projects", "scope body"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "second state"));

        var notes = storage.Read(temp.WorkspacePath);
        Assert.Contains("second state", notes);
        Assert.DoesNotContain("first state", notes);
        Assert.Equal(1, Count(notes, "<!-- section:current-task -->"));
        Assert.Equal(1, Count(notes, "<!-- /section:current-task -->"));
        Assert.Contains("scope body", notes);
    }

    [Fact]
    public void Search_IsCaseInsensitive_AndRespectsHeadLimit()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var content = string.Join('\n', new[]
        {
            "alpha one",
            "ALPHA two",
            "beta",
            "Alpha three"
        });
        Assert.Equal("OK", storage.Write(temp.WorkspacePath, content));

        var json = storage.Search(temp.WorkspacePath, "alpha", 2);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(3, root.GetProperty("total_matches").GetInt32());
        Assert.Equal(2, root.GetProperty("returned_matches").GetInt32());
        Assert.Equal(2, root.GetProperty("matches").GetArrayLength());
    }

    [Fact]
    public void Rollback_RestoresPreviousContent_AndCreatesRollbackRevision()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.Write(temp.WorkspacePath, "v1"));
        Assert.Equal("OK", storage.Write(temp.WorkspacePath, "v2"));

        var beforeRollback = ParseRevisionList(storage.ListRevisions(temp.WorkspacePath, 20));
        Assert.NotEmpty(beforeRollback);

        var rollbackResult = storage.Rollback(temp.WorkspacePath, null);
        Assert.StartsWith("OK (", rollbackResult, StringComparison.Ordinal);
        Assert.Equal("v1", storage.Read(temp.WorkspacePath));

        var afterRollback = ParseRevisionList(storage.ListRevisions(temp.WorkspacePath, 20));
        Assert.Contains(
            afterRollback,
            static file => file.Contains("rollback-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EndToEnd_WriteUpsertSearchRollback_WorksAndCleansState()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.Write(temp.WorkspacePath, "seed"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "Build integration checks"));

        var searchJson = storage.Search(temp.WorkspacePath, "integration", 10);
        using var searchDoc = JsonDocument.Parse(searchJson);
        Assert.Equal(1, searchDoc.RootElement.GetProperty("total_matches").GetInt32());

        Assert.Equal("OK", storage.Write(temp.WorkspacePath, "mutated"));
        var revisions = ParseRevisionList(storage.ListRevisions(temp.WorkspacePath, 20));
        var seedRevision = revisions
            .FirstOrDefault(static file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrEmpty(seedRevision));

        var rollbackResult = storage.Rollback(temp.WorkspacePath, seedRevision);
        Assert.StartsWith("OK (", rollbackResult, StringComparison.Ordinal);
        Assert.Contains("current-task", storage.Read(temp.WorkspacePath));
    }

    [Fact]
    public void MemoryHealth_ReportsWarning_WhenHotContextIsTooLarge()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: current-projects"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", new string('x', 7000)));

        var json = storage.MemoryHealth(temp.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("warning", root.GetProperty("health_level").GetString());
        Assert.True(root.GetProperty("recommend_compaction").GetBoolean());
        Assert.True(root.GetProperty("hot_context").GetProperty("chars").GetInt32() > 6000);
    }

    [Fact]
    public void ReadHotContext_LoadsL0FromMemoryArchitectureSection_WhenPresent()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var memoryArch = """
            ## Memory Architecture v1
            ### L0: Hot State (always load)
            - custom-baseline-a
            - custom-baseline-b
            - active-scope
            - current-task
            ### L1: Operational
            - scope-current-projects
            """;
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", memoryArch));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-a", "baseline A"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-b", "baseline B"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: current-projects"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "task"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-current-projects", "scope body"));

        var json = storage.ReadHotContext(temp.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        var loaded = doc.RootElement.GetProperty("loaded_sections");
        var ids = new List<string>();
        foreach (var e in loaded.EnumerateArray())
            ids.Add(e.GetString() ?? "");

        Assert.Contains("custom-baseline-a", ids);
        Assert.Contains("custom-baseline-b", ids);
        Assert.True(ids.IndexOf("custom-baseline-a") < ids.IndexOf("active-scope"), "L0 from file should appear before scope");
    }

    [Fact]
    public void RouteContext_ReturnsRelevantSectionsAndAssembledContext()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: current-projects"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "Ship release flow for MCP stack"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-current-projects", "Engineering release queue"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "random-notes", "Unrelated text"));

        var json = storage.RouteContext(temp.WorkspacePath, "release flow", null, maxSections: 3, maxChars: 5000);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("selected_count").GetInt32() >= 1);
        var assembled = root.GetProperty("assembled_context").GetString() ?? "";
        Assert.Contains("current-task", assembled);
        Assert.Contains("release", assembled, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string token)
    {
        var matches = 0;
        var index = 0;
        while (index >= 0)
        {
            index = value.IndexOf(token, index, StringComparison.Ordinal);
            if (index < 0)
                break;
            matches++;
            index += token.Length;
        }

        return matches;
    }

    private static string[] ParseRevisionList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .EnumerateArray()
            .Select(static item => item.GetProperty("file").GetString() ?? "")
            .Where(static value => value.Length > 0)
            .ToArray();
    }

    private sealed class TempWorkspace : IDisposable
    {
        private TempWorkspace(string rootPath, string workspacePath, string notesFilePath)
        {
            RootPath = rootPath;
            WorkspacePath = workspacePath;
            NotesFilePath = notesFilePath;
        }

        internal string RootPath { get; }
        internal string WorkspacePath { get; }
        internal string NotesFilePath { get; }

        internal static TempWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", Guid.NewGuid().ToString("N"));
            var workspace = Path.Combine(root, "workspace");
            Directory.CreateDirectory(workspace);
            var notesDir = Path.Combine(root, "notes");
            Directory.CreateDirectory(notesDir);
            var notesFile = Path.Combine(notesDir, "agent-notes.md");
            return new TempWorkspace(root, workspace, notesFile);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                    Directory.Delete(RootPath, recursive: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to cleanup test temp directory: {RootPath}", ex);
            }
        }
    }

    private sealed class EnvVarScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        private EnvVarScope(string name, string? previous)
        {
            _name = name;
            _previous = previous;
        }

        internal static EnvVarScope Set(string name, string value)
        {
            var previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
            return new EnvVarScope(name, previous);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
