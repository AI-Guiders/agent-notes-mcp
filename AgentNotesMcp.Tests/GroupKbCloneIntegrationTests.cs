using AgentNotes.Core;
using System.Text;

namespace AgentNotesMcp.Tests;

/// <summary>Smoke against repo <c>group-kb</c> and a local <c>git clone</c> (see ADR 015).</summary>
public sealed class GroupKbCloneIntegrationTests
{
    [Fact]
    public void ReadSmoke_FromRepoGroupKb_WhenPresent()
    {
        var groupKb = FindSiblingDirectory("group-kb");
        if (groupKb is null)
            return;

        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = InstallGroupReadOnly(primary.Path, groupKb);
        var storage = new NotesStorage();
        var text = storage.ReadKnowledgeFile(null, "group/smoke-test-v1.md", knowledgeRootId: "group");
        Assert.Contains("group-kb smoke", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSmoke_FromClonedGroupKb_WhenPresent()
    {
        var clone = FindSiblingDirectory("group-kb-clone");
        if (clone is null)
            return;

        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var runtime = InstallGroupReadOnly(primary.Path, clone);
        var storage = new NotesStorage();
        var text = storage.ReadKnowledgeFile(null, "group/smoke-test-v1.md", knowledgeRootId: "group");
        Assert.Contains("group-kb smoke", text, StringComparison.Ordinal);
    }

    private static AgentNotesTestToml.RuntimeScope InstallGroupReadOnly(string primaryPath, string groupPath)
    {
        var toml = $"""
            version = 1

            [knowledge]
            primary = "test"

            [knowledge.roots]
            test = "{primaryPath.Replace('\\', '/')}"

            [[knowledge.read_only]]
            id = "group"
            path = "{groupPath.Replace('\\', '/')}"

            [workspace]
            default_scope = "door-to-singularity"
            scope_map = "work/local/workspace-scope-map-v1.md"
            scope_aliases = "work/local/scope-alias-map-v1.md"

            [status]
            enabled = false
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml, Encoding.UTF8);
        return AgentNotesTestToml.Install(path);
    }

    private static string? FindSiblingDirectory(string name)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(Path.Combine(candidate, "knowledge", "group", "smoke-test-v1.md")))
                return Path.GetFullPath(candidate);
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
