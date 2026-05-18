using AgentNotes.Core;
using System.Text;

namespace AgentNotesMcp.Tests;

public sealed class MultiRootKnowledgeTests
{
    [Fact]
    public void ReadKnowledgeFile_UsesReadOnlyRoot_ByKnowledgeRootId()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = CreateGroupKbRoot();
        using var runtime = InstallWithReadOnly(primary.Path, group.Path);
        var storage = new NotesStorage();

        var text = storage.ReadKnowledgeFile(null, "group/smoke-test-v1.md", knowledgeRootId: "group");
        Assert.Contains("group-kb smoke", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteKnowledgeFile_ToReadOnlyRoot_Throws()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = CreateGroupKbRoot();
        using var runtime = InstallWithReadOnly(primary.Path, group.Path);
        var storage = new NotesStorage();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            storage.WriteKnowledgeFile(null, "group/evil.md", "nope", knowledgeRootId: "group"));
        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteKnowledgeFile_ToReadOnlyPath_Throws()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = CreateGroupKbRoot();
        using var runtime = InstallWithReadOnly(primary.Path, group.Path);
        var storage = new NotesStorage();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            storage.WriteKnowledgeFile(group.Path, "group/evil.md", "nope"));
        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnowledgePath_And_KnowledgeRootId_AreMutuallyExclusive()
    {
        using var primary = LocalSettingsLoaderTests.TempKnowledgeRoot.Create();
        using var group = CreateGroupKbRoot();
        using var runtime = InstallWithReadOnly(primary.Path, group.Path);

        var ex = Assert.Throws<ArgumentException>(() =>
            NotesStorage.ResolveKnowledgeRoot(primary.Path, "group"));
        Assert.Contains("not both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    internal static AgentNotesTestToml.RuntimeScope InstallWithReadOnlyPublic(string primaryPath, string groupPath) =>
        InstallWithReadOnly(primaryPath, groupPath);

    internal static GroupKbRoot CreateGroupKbRootPublic() => CreateGroupKbRoot();

    private static AgentNotesTestToml.RuntimeScope InstallWithReadOnly(
        string primaryPath,
        string groupPath)
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
            port = 17341
            bind = "127.0.0.1"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml, Encoding.UTF8);
        return AgentNotesTestToml.Install(path);
    }

    private static GroupKbRoot CreateGroupKbRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", "group-kb", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "knowledge", "group"));
        File.WriteAllText(
            Path.Combine(path, "knowledge", "group", "smoke-test-v1.md"),
            "# Group KB smoke\n\ngroup-kb smoke test content.\n",
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(path, "agent-notes.md"), "# Group hot (stub)\n", Encoding.UTF8);
        return new GroupKbRoot(path);
    }

    internal sealed class GroupKbRoot(string path) : IDisposable
    {
        internal string Path { get; } = path;

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to cleanup group-kb: {Path}", ex);
            }
        }
    }
}
