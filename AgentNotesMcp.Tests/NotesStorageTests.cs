using System.Text;
using System.Text.Json;
using AgentNotes.Core;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AgentNotesMcp.Tests;

public sealed class NotesStorageTests
{
    /// <summary>Must stay in sync with agent-notes <c>knowledge/work/local/scope-alias-map-v1.md</c> (tests seed this file whenever a temp canon root is created).</summary>
    private const string TestScopeAliasesMd =
        """
        current-projects => door-to-singularity
        dts => door-to-singularity
        cp => door-to-singularity
        ptl => portal
        hrv => harvester
        """;

    internal static void SeedTestScopeAliasDefaultsForRoot(string canonTreeRoot) =>
        SeedTestScopeAliasDefaults(canonTreeRoot);

    private static void SeedTestScopeAliasDefaults(string canonTreeRoot)
    {
        var local = Path.Combine(canonTreeRoot, "knowledge", "work", "local");
        Directory.CreateDirectory(local);
        var path = Path.Combine(local, "scope-alias-map-v1.md");
        if (!File.Exists(path))
            File.WriteAllText(path, TestScopeAliasesMd.Trim() + Environment.NewLine, Encoding.UTF8);
    }
    [Fact]
    public void UpsertSection_CreatesAndUpdatesWithoutDuplicates()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "first state"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-door-to-singularity", "scope body"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "second state"));

        var notes = storage.Read(temp.WorkspacePath);
        Assert.Contains("second state", notes);
        Assert.DoesNotContain("first state", notes);
        Assert.Equal(1, Count(notes, "<!-- section:current-task -->"));
        Assert.Equal(1, Count(notes, "<!-- /section:current-task -->"));
        Assert.Contains("scope body", notes);
    }

    [Fact]
    public void UpsertSection_RejectsDuplicateBlocks_DoesNotBloat()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var broken =
            """
            <!-- section:current-task -->
            first
            <!-- /section:current-task -->

            <!-- section:current-task -->
            second
            <!-- /section:current-task -->
            """;
        Assert.Equal("OK", storage.Write(temp.WorkspacePath, broken));
        var before = storage.Read(temp.WorkspacePath);

        var ex = Assert.Throws<InvalidOperationException>(
            () => storage.UpsertSection(temp.WorkspacePath, "current-task", "third"));
        Assert.Contains("REJECTED", ex.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, storage.Read(temp.WorkspacePath));
    }

    [Fact]
    public void UpsertSection_RejectsUnclosedSection_DoesNotAppend()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.Write(temp.WorkspacePath,
            "<!-- section:current-task -->\norphan open\n"));
        var before = storage.Read(temp.WorkspacePath);

        var ex = Assert.Throws<InvalidOperationException>(
            () => storage.UpsertSection(temp.WorkspacePath, "current-task", "fixed"));
        Assert.Contains("REJECTED", ex.Message, StringComparison.Ordinal);
        Assert.Contains("unclosed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, storage.Read(temp.WorkspacePath));
        Assert.Equal(1, Count(before, "<!-- section:current-task -->"));
    }

    [Fact]
    public void ValidateSections_ReportsDuplicates()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();
        Assert.Equal("OK", storage.Write(temp.WorkspacePath,
            """
            <!-- section:a -->
            one
            <!-- /section:a -->

            <!-- section:a -->
            two
            <!-- /section:a -->
            """));

        using var doc = JsonDocument.Parse(storage.ValidateSections(temp.WorkspacePath));
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("a", doc.RootElement.GetProperty("section_ids").EnumerateArray().Select(x => x.GetString()));
        Assert.True(doc.RootElement.GetProperty("duplicates").GetArrayLength() >= 1);
    }

    [Fact]
    public void NormalizeSections_CollapsesDuplicates_ApplyWrites()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();
        Assert.Equal("OK", storage.Write(temp.WorkspacePath,
            """
            preamble

            <!-- section:a -->
            one
            <!-- /section:a -->

            <!-- section:a -->
            two
            <!-- /section:a -->

            <!-- section:current-task -->
            open only
            """));

        var preview = storage.NormalizeSections(temp.WorkspacePath, apply: false);
        using (var doc = JsonDocument.Parse(preview))
        {
            Assert.True(doc.RootElement.GetProperty("changed").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("after").GetProperty("ok").GetBoolean());
        }

        Assert.Equal("OK", storage.NormalizeSections(temp.WorkspacePath, apply: true));
        var after = storage.Read(temp.WorkspacePath);
        Assert.Equal(1, Count(after, "<!-- section:a -->"));
        Assert.Contains("two", after);
        Assert.DoesNotContain("one", after);
        Assert.Contains("preamble", after);
        // Unclosed body may remain as plain text (do not delete agent content).
        Assert.DoesNotContain("<!-- section:current-task -->", after);
        using var afterDoc = JsonDocument.Parse(storage.ValidateSections(temp.WorkspacePath));
        Assert.True(afterDoc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "a", "three"));
    }

    [Fact]
    public void DeleteSection_RemovesBlock_ReturnsNoChangesWhenMissing()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.Write(temp.WorkspacePath,
            "head\n\n<!-- section:to-remove -->\nbody\n<!-- /section:to-remove -->\n\ntail"));
        Assert.Equal("OK", storage.DeleteSection(temp.WorkspacePath, "to-remove"));

        var after = storage.Read(temp.WorkspacePath);
        Assert.DoesNotContain("<!-- section:to-remove -->", after);
        Assert.DoesNotContain("body", after);
        Assert.Contains("head", after);
        Assert.Contains("tail", after);
        Assert.Equal("NO_CHANGES", storage.DeleteSection(temp.WorkspacePath, "to-remove"));
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
    public void Knowledge_WriteRead_WithExplicitCanonPath()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        const string content = "# Test KB\n\nBody here.";

        Assert.Equal("OK", storage.WriteKnowledgeFile(root.Path, "kb-test-v1.md", content));
        Assert.Equal(content, storage.ReadKnowledgeFile(root.Path, "kb-test-v1.md"));
        Assert.True(File.Exists(Path.Combine(root.Path, "knowledge", "kb-test-v1.md")));
    }

    [Fact]
    public void Knowledge_Read_WhenFileMissing_ReturnsEmpty()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        Assert.Equal("", storage.ReadKnowledgeFile(root.Path, "missing.md"));
    }

    [Fact]
    public void Knowledge_Read_OffsetAndLimit_SlicesByOneBasedLines()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "lines.md", "a\r\nb\nc\nd", saveRevision: false);
        var path = root.Path;
        Assert.Equal("b\nc", storage.ReadKnowledgeFile(path, "lines.md", 2, 2));
        Assert.Equal("c\nd", storage.ReadKnowledgeFile(path, "lines.md", 3, null));
        Assert.Equal("a\nb", storage.ReadKnowledgeFile(path, "lines.md", 1, 2));
        Assert.Equal("a\nb\nc\nd", storage.ReadKnowledgeFile(path, "lines.md", 1, null));
        Assert.Equal("d", storage.ReadKnowledgeFile(path, "lines.md", 4, 10));
        Assert.Equal("", storage.ReadKnowledgeFile(path, "lines.md", 5, 1));
        Assert.Equal("", storage.ReadKnowledgeFile(path, "lines.md", 1, 0));
    }

    [Fact]
    public void Knowledge_Append_DoesNotOverwrite()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "append-test.md", "first");
        Assert.Equal("OK", storage.AppendKnowledgeFile(root.Path, "append-test.md", "second"));
        Assert.Equal("first\nsecond", storage.ReadKnowledgeFile(root.Path, "append-test.md"));
    }

    [Fact]
    public void Knowledge_UpsertSection_InsertsThenUpdates()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "sections.md", "preamble\n");

        Assert.Equal("OK", storage.UpsertKnowledgeSection(root.Path, "sections.md", "music-router", "load playbook-music-v1"));
        var afterInsert = storage.ReadKnowledgeFile(root.Path, "sections.md");
        Assert.Contains("<!-- section:music-router -->", afterInsert);
        Assert.Contains("load playbook-music-v1", afterInsert);

        Assert.Equal("OK", storage.UpsertKnowledgeSection(root.Path, "sections.md", "music-router", "load playbook-music-v1 and kb-*"));
        var afterUpdate = storage.ReadKnowledgeFile(root.Path, "sections.md");
        Assert.DoesNotContain("load playbook-music-v1\n", afterUpdate);
        Assert.Contains("load playbook-music-v1 and kb-*", afterUpdate);
        Assert.Equal(1, Count(afterUpdate, "<!-- section:music-router -->"));
    }

    [Fact]
    public void Knowledge_UpsertSection_RejectsDuplicate_DoesNotBloat()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "dup.md",
            """
            <!-- section:a -->
            one
            <!-- /section:a -->

            <!-- section:a -->
            two
            <!-- /section:a -->
            """);
        var before = storage.ReadKnowledgeFile(root.Path, "dup.md");
        var ex = Assert.Throws<InvalidOperationException>(
            () => storage.UpsertKnowledgeSection(root.Path, "dup.md", "a", "three"));
        Assert.Contains("REJECTED", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, storage.ReadKnowledgeFile(root.Path, "dup.md"));
    }

    [Fact]
    public void Knowledge_DeleteSection_RemovesBlock_ReturnsNoChangesWhenMissing()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "del-section.md", "head\n\n<!-- section:to-remove -->\nbody\n<!-- /section:to-remove -->\n\ntail");
        Assert.Equal("OK", storage.DeleteKnowledgeSection(root.Path, "del-section.md", "to-remove"));
        var after = storage.ReadKnowledgeFile(root.Path, "del-section.md");
        Assert.DoesNotContain("<!-- section:to-remove -->", after);
        Assert.DoesNotContain("body", after);
        Assert.Contains("head", after);
        Assert.Contains("tail", after);
        Assert.Equal("NO_CHANGES", storage.DeleteKnowledgeSection(root.Path, "del-section.md", "to-remove"));
    }

    [Fact]
    public void Knowledge_DeleteFile_RemovesFile_ReturnsNoChangesWhenMissing()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(root.Path, "to-delete.md", "content");
        Assert.True(File.Exists(Path.Combine(root.Path, "knowledge", "to-delete.md")));
        Assert.Equal("OK", storage.DeleteKnowledgeFile(root.Path, "to-delete.md"));
        Assert.False(File.Exists(Path.Combine(root.Path, "knowledge", "to-delete.md")));
        Assert.Equal("NO_CHANGES", storage.DeleteKnowledgeFile(root.Path, "to-delete.md"));
    }

    [Fact]
    public void Knowledge_Write_RejectsPathTraversal()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        Assert.Throws<ArgumentException>(() =>
            storage.WriteKnowledgeFile(root.Path, "../evil.md", "x"));
    }

    [Fact]
    public void Knowledge_ResolveKnowledgeRoot_FromRuntime_WhenToolArgumentNull()
    {
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(null, "runtime-root-test.md", "from runtime");
        Assert.Equal("from runtime", storage.ReadKnowledgeFile(null, "runtime-root-test.md"));
    }

    [Fact]
    public void Knowledge_ResolveKnowledgeRoot_ThrowsWhenNeitherArgumentNorRuntimeNorInferableNotesFile()
    {
        AgentNotesRuntime.ClearConfiguration();
        using var clearFile = EnvVarScope.Clear("AGENT_NOTES_FILE");
        var storage = new NotesStorage();
        Assert.Throws<ArgumentException>(() =>
            storage.WriteKnowledgeFile(null, "any.md", "x"));
    }

    [Fact]
    public void Knowledge_ResolveKnowledgeRoot_InferredFromAgentNotesFile_WhenRuntimeNotLoaded()
    {
        AgentNotesRuntime.ClearConfiguration();
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        Directory.CreateDirectory(Path.Combine(root.Path, "knowledge"));
        var notesPath = Path.Combine(root.Path, "agent-notes.md");
        File.WriteAllText(notesPath, "# notes\n", Encoding.UTF8);

        using var setFile = EnvVarScope.Set("AGENT_NOTES_FILE", notesPath);
        var storage = new NotesStorage();
        storage.WriteKnowledgeFile(null, "from-inferred-root.md", "body");
        Assert.Equal("body", storage.ReadKnowledgeFile(null, "from-inferred-root.md"));
    }

    [Fact]
    public void ReadWrite_UsesAgentNotesUnderPrimaryRoot_WhenTomlConfiguredAndNotesFileUnset()
    {
        using var ws = TempWorkspace.Create();
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        Directory.CreateDirectory(Path.Combine(root.Path, "knowledge"));
        using var clearFile = EnvVarScope.Clear("AGENT_NOTES_FILE");
        var storage = new NotesStorage();
        var expectedNotes = Path.Combine(root.Path, "agent-notes.md");

        Assert.Equal("OK", storage.Write(ws.WorkspacePath, "from-toml-root"));
        Assert.Equal("from-toml-root", storage.Read(ws.WorkspacePath));
        Assert.True(File.Exists(expectedNotes));
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
    public void MemoryHealth_UsesWorkspaceScopeMapFromTomlWorkLocal()
    {
        using var ws = TempWorkspace.Create();
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        Directory.CreateDirectory(Path.Combine(root.Path, "knowledge", "work", "local"));
        var mapPath = Path.Combine(root.Path, "knowledge", "work", "local", "workspace-scope-map-v1.md");
        File.WriteAllText(mapPath, $"{Path.GetFullPath(ws.WorkspacePath)} => portal\n", Encoding.UTF8);

        using var clearFile = EnvVarScope.Clear("AGENT_NOTES_FILE");
        var storage = new NotesStorage();

        const string notesContent = """
<!-- section:current-task -->
ok
<!-- /section:current-task -->
""";
        Assert.Equal("OK", storage.Write(ws.WorkspacePath, notesContent));

        var json = storage.MemoryHealth(ws.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("portal", doc.RootElement.GetProperty("resolved_scope").GetString());
    }

    [Fact]
    public void MemoryHealth_UsesWorkspaceScopeMapFromToml_WhenCustomRelativePath()
    {
        using var ws = TempWorkspace.Create();
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        Directory.CreateDirectory(Path.Combine(root.Path, "knowledge", "work", "custom-maps"));
        var mapPath = Path.Combine(root.Path, "knowledge", "work", "custom-maps", "my-ws-map.md");
        File.WriteAllText(mapPath, $"{Path.GetFullPath(ws.WorkspacePath)} => portal\n", Encoding.UTF8);
        SeedTestScopeAliasDefaults(root.Path);
        using var runtime = AgentNotesTestToml.Install(
            AgentNotesTestToml.Write(
                root.Path,
                scopeMap: "work/custom-maps/my-ws-map.md",
                scopeAliases: "work/local/scope-alias-map-v1.md"));

        using var clearFile = EnvVarScope.Clear("AGENT_NOTES_FILE");
        var storage = new NotesStorage();

        const string notesContent = """
<!-- section:current-task -->
ok
<!-- /section:current-task -->
""";
        Assert.Equal("OK", storage.Write(ws.WorkspacePath, notesContent));

        var json = storage.MemoryHealth(ws.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("portal", doc.RootElement.GetProperty("resolved_scope").GetString());
    }

    [Fact]
    public void MemoryHealth_UsesEmbeddedWorkspaceMap_WhenRuntimeNotConfigured()
    {
        AgentNotesRuntime.ClearConfiguration();
        using var ws = TempWorkspace.Create();
        using var root = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        Directory.CreateDirectory(Path.Combine(root.Path, "knowledge", "work", "local"));
        var mapPath = Path.Combine(root.Path, "knowledge", "work", "local", "workspace-scope-map-v1.md");
        File.WriteAllText(mapPath, $"{Path.GetFullPath(ws.WorkspacePath)} => portal\n", Encoding.UTF8);
        SeedTestScopeAliasDefaults(root.Path);
        var notesPath = Path.Combine(root.Path, "agent-notes.md");
        File.WriteAllText(notesPath, "# notes\n", Encoding.UTF8);

        using var setFile = EnvVarScope.Set("AGENT_NOTES_FILE", notesPath);
        var storage = new NotesStorage();

        const string notesContent = """
<!-- section:current-task -->
ok
<!-- /section:current-task -->
""";
        Assert.Equal("OK", storage.Write(ws.WorkspacePath, notesContent));

        var json = storage.MemoryHealth(ws.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("portal", doc.RootElement.GetProperty("resolved_scope").GetString());
    }

    [Fact]
    public void MemoryHealth_ReportsWarning_WhenHotContextIsTooLarge()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var notesRoot = Path.GetDirectoryName(temp.NotesFilePath)!;
        var metaDir = Path.Combine(notesRoot, "knowledge", "META");
        Directory.CreateDirectory(metaDir);
        File.WriteAllText(Path.Combine(metaDir, "memory-architecture-v1.json"), """
            { "l0": ["active-scope"], "l0_owner": ["current-task"] }
            """);
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", """
            l0_manifest: knowledge/META/memory-architecture-v1.json
            """));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: door-to-singularity"));
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
            - scope-door-to-singularity
            """;
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", memoryArch));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-a", "baseline A"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-b", "baseline B"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: door-to-singularity"));
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
    public void ReadHotContext_LoadsL0FromManifest_WhenSpecified()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var notesRoot = Path.GetDirectoryName(temp.NotesFilePath)!;
        var metaDir = Path.Combine(notesRoot, "knowledge", "META");
        Directory.CreateDirectory(metaDir);

        var manifestPath = Path.Combine(metaDir, "memory-architecture-v1.json");
        File.WriteAllText(manifestPath, """
            {
              "l0": [
                "custom-baseline-a",
                "custom-baseline-b",
                "active-scope",
                "current-task"
              ]
            }
            """);

        var memoryArch = """
            ## Memory Architecture v1
            l0_manifest: knowledge/META/memory-architecture-v1.json
            ### L0: Hot State (always load)
            - THIS_SHOULD_BE_IGNORED
            - active-scope
            - current-task
            """;
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", memoryArch));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-a", "baseline A"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "custom-baseline-b", "baseline B"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: door-to-singularity"));
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
        Assert.DoesNotContain("THIS_SHOULD_BE_IGNORED", ids);
    }

    [Fact]
    public void ReadHotContext_LoadsDefaultManifest_WhenOnlyPublicStubPresent()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var notesRoot = Path.GetDirectoryName(temp.NotesFilePath)!;
        var metaDir = Path.Combine(notesRoot, "knowledge", "META");
        Directory.CreateDirectory(metaDir);
        File.WriteAllText(Path.Combine(metaDir, "memory-architecture-v1.json"), """
            {
              "l0": ["baseline-integrity-epistemic-v1", "active-scope"],
              "l0_owner": ["current-task"]
            }
            """);

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", """
            l0_manifest: knowledge/META/memory-architecture-v1.json
            """));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "baseline-integrity-epistemic-v1", "baseline"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "scope pointer"));

        var json = storage.ReadHotContext(temp.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("loaded_sections").EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .ToList();

        Assert.Contains("baseline-integrity-epistemic-v1", ids);
        Assert.DoesNotContain("current-task", ids);
    }

    [Fact]
    public void MemoryHealth_DoesNotRequireCurrentTask_WhenNotInHotSectionIds()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        var notesRoot = Path.GetDirectoryName(temp.NotesFilePath)!;
        var metaDir = Path.Combine(notesRoot, "knowledge", "META");
        Directory.CreateDirectory(metaDir);
        File.WriteAllText(Path.Combine(metaDir, "memory-architecture-v1.json"), """
            { "l0": ["baseline-integrity-epistemic-v1", "active-scope"] }
            """);

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "memory-architecture-v1", """
            l0_manifest: knowledge/META/memory-architecture-v1.json
            """));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "baseline-integrity-epistemic-v1", "baseline"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "scope"));

        var json = storage.MemoryHealth(temp.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        var missing = doc.RootElement.GetProperty("missing_core_sections");
        Assert.Equal(0, missing.GetArrayLength());
    }

    [Fact]
    public void RouteContext_ReturnsRelevantSectionsAndAssembledContext()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: door-to-singularity"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "current-task", "Ship release flow for MCP stack"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-door-to-singularity", "Engineering release queue"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "random-notes", "Unrelated text"));

        var json = storage.RouteContext(temp.WorkspacePath, "release flow", null, maxSections: 3, maxChars: 5000);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("selected_count").GetInt32() >= 1);
        var assembled = root.GetProperty("assembled_context").GetString() ?? "";
        Assert.Contains("current-task", assembled);
        Assert.Contains("release", assembled, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadHotContext_LegacyActiveScopeValue_ResolvesToCanonical_LoadsLegacySectionWhenNewIdMissing()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "active-scope", "current: current-projects"));
        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-current-projects", "legacy hub body"));

        var json = storage.ReadHotContext(temp.WorkspacePath, null);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("door-to-singularity", doc.RootElement.GetProperty("active_scope").GetString());
        var content = doc.RootElement.GetProperty("content").GetString() ?? "";
        Assert.Contains("legacy hub body", content);
    }

    [Fact]
    public void ReadHotContext_ExplicitDtsAlias_ResolvesToDoorToSingularity()
    {
        using var temp = TempWorkspace.Create();
        using var env = EnvVarScope.Set("AGENT_NOTES_FILE", temp.NotesFilePath);
        var storage = new NotesStorage();

        Assert.Equal("OK", storage.UpsertSection(temp.WorkspacePath, "scope-door-to-singularity", "dts scope"));

        var json = storage.ReadHotContext(temp.WorkspacePath, "dts");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("door-to-singularity", doc.RootElement.GetProperty("active_scope").GetString());
        Assert.Contains("dts scope", doc.RootElement.GetProperty("content").GetString() ?? "");
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
            SeedTestScopeAliasDefaults(root);
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

        internal static EnvVarScope Clear(string name)
        {
            var previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
            return new EnvVarScope(name, previous);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
