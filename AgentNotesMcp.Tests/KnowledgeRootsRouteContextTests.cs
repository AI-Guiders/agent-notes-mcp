using AgentNotes.Core;
using System.Text;
using System.Text.Json;

namespace AgentNotesMcp.Tests;

public sealed class KnowledgeRootsRouteContextTests
{
    [Fact]
    public void RouteContext_IncludesKnowledgeRootsOverlay_WhenRegistryMatchesGroupQuery()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = MultiRootKnowledgeTests.CreateGroupKbRootPublic();
        using var runtime = MultiRootKnowledgeTests.InstallWithReadOnlyPublic(primary.Path, group.Path);
        SeedKnowledgeRootsRouting(primary.Path);

        var storage = new NotesStorage();
        var workspace = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        var json = storage.RouteContext(workspace, "group smoke test", null, maxSections: 6, maxChars: 12000);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("knowledge_roots_overlay_applied").GetBoolean());
        Assert.True(root.GetProperty("knowledge_roots_registry_hits").GetInt32() >= 1);

        var assembled = root.GetProperty("assembled_context").GetString() ?? "";
        Assert.Contains("knowledge-roots-routing-v1", assembled);
        Assert.Contains("knowledge_root_id=group", assembled, StringComparison.Ordinal);
        Assert.Contains("group-kb smoke", assembled, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RouteContext_IncludesPrefixRegistry_WhenQueryMatchesCatalogSegment()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = CreateGroupKbWithOpenStackPrefix();
        using var runtime = MultiRootKnowledgeTests.InstallWithReadOnlyPublic(primary.Path, group.Path);
        SeedKnowledgeRootsRoutingWithPrefix(primary.Path);

        var storage = new NotesStorage();
        var workspace = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        var json = storage.RouteContext(workspace, "aiguiders-open mission cards", null, maxSections: 8, maxChars: 12000);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("knowledge_roots_overlay_applied").GetBoolean());
        Assert.True(root.GetProperty("knowledge_roots_registry_hits").GetInt32() >= 1);

        var assembled = root.GetProperty("assembled_context").GetString() ?? "";
        Assert.Contains("Prefix:", assembled, StringComparison.Ordinal);
        Assert.Contains("aiguiders-open", assembled, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("open-stack hub", assembled, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RouteContext_NoOverlay_WhenUnrelatedQueryAndNoRegistryMatch()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = MultiRootKnowledgeTests.CreateGroupKbRootPublic();
        using var runtime = MultiRootKnowledgeTests.InstallWithReadOnlyPublic(primary.Path, group.Path);
        SeedKnowledgeRootsRouting(primary.Path);

        var storage = new NotesStorage();
        var workspace = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        Assert.Equal("OK", storage.UpsertSection(workspace, "current-task", "Ship release flow for unrelated feature"));

        var json = storage.RouteContext(workspace, "release flow", null, maxSections: 3, maxChars: 5000);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("knowledge_roots_overlay_applied").GetBoolean());
        var assembled = root.GetProperty("assembled_context").GetString() ?? "";
        Assert.DoesNotContain("knowledge_root_id=group", assembled);
    }

    private static MultiRootKnowledgeTests.GroupKbRoot CreateGroupKbWithOpenStackPrefix()
    {
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", "group-kb", Guid.NewGuid().ToString("N"));
        var openDir = Path.Combine(path, "knowledge", "work", "projects", "aiguiders-open");
        Directory.CreateDirectory(Path.Combine(path, "knowledge", "group"));
        Directory.CreateDirectory(openDir);
        File.WriteAllText(
            Path.Combine(path, "knowledge", "group", "smoke-test-v1.md"),
            "# Group KB smoke\n\ngroup-kb smoke test content.\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(openDir, "README.md"),
            "# Open stack hub\n\nopen-stack hub for tests.\n",
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(path, "agent-notes.md"), "# Group hot (stub)\n", Encoding.UTF8);
        return new MultiRootKnowledgeTests.GroupKbRoot(path);
    }

    private static void SeedKnowledgeRootsRouting(string primaryPath) =>
        WriteKnowledgeRootsIndex(primaryPath, "group/smoke-test-v1.md => group\n");

    private static void SeedKnowledgeRootsRoutingWithPrefix(string primaryPath) =>
        WriteKnowledgeRootsIndex(
            primaryPath,
            """
            group/smoke-test-v1.md => group
            work/projects/aiguiders-open/ => group
            """);

    private static void WriteKnowledgeRootsIndex(string primaryPath, string body)
    {
        Directory.CreateDirectory(Path.Combine(primaryPath, "knowledge", "work", "local"));
        File.WriteAllText(
            Path.Combine(primaryPath, "knowledge", "work", "local", "knowledge-roots-index-v1.md"),
            body,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(primaryPath, "agent-notes.md"),
            """
            <!-- section:knowledge-roots-routing-v1 -->
            chmod ugo: group=read-only via knowledge_root_id=group; user=primary writes.
            Registry: work/local/knowledge-roots-index-v1.md
            <!-- /section:knowledge-roots-routing-v1 -->
            <!-- section:current-task -->
            unrelated default task
            <!-- /section:current-task -->
            """,
            Encoding.UTF8);
    }
}
