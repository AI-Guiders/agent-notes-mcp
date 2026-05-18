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

    private static void SeedKnowledgeRootsRouting(string primaryPath)
    {
        Directory.CreateDirectory(Path.Combine(primaryPath, "knowledge", "work", "local"));
        File.WriteAllText(
            Path.Combine(primaryPath, "knowledge", "work", "local", "knowledge-roots-index-v1.md"),
            "group/smoke-test-v1.md => group\n",
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
