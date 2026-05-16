using AgentNotes.Core;

namespace AgentNotesMcp.Tests;

public sealed class LocalSettingsLoaderTests
{
    [Fact]
    public void Bootstrap_MissingConfig_ReturnsExitCode2()
    {
        using var clear = TestEnvVarScope.Clear(AgentNotesBootstrap.ConfigEnvVar);
        var code = AgentNotesBootstrap.TryLoadSettings([], out var settings, out var error);
        Assert.Equal(AgentNotesBootstrap.ExitMissingConfig, code);
        Assert.Null(settings);
        Assert.Contains("--config", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_ResolvesPrimaryFromNamedRoot()
    {
        using var root = TempKnowledgeRoot.Create();
        var tomlPath = AgentNotesTestToml.WriteFromEmbeddedTemplate(root.Path);
        var settings = LocalSettingsLoader.Load(tomlPath);
        Assert.Equal(root.Path, settings.PrimaryKnowledgeRoot);
        Assert.Equal("door-to-singularity", settings.Workspace.DefaultScope);
        Assert.Equal("work/local/workspace-scope-map-v1.md", settings.Workspace.ScopeMapRelative);
    }

    [Fact]
    public void Loader_WithoutWorkspaceSection_UsesEmbeddedNeutralExample()
    {
        using var root = TempKnowledgeRoot.Create();
        var toml = $"""
            version = 1

            [knowledge]
            primary = "test"

            [knowledge.roots]
            test = "{root.Path.Replace('\\', '/')}"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml);
        var settings = LocalSettingsLoader.Load(path);
        Assert.Equal("example", settings.Workspace.DefaultScope);
        Assert.Equal("example/workspace-scope-map-v1.md", settings.Workspace.ScopeMapRelative);
    }

    [Fact]
    public void Loader_RejectsInvalidWorkspaceScopeMapPath()
    {
        using var root = TempKnowledgeRoot.Create();
        var toml = $"""
            version = 1

            [knowledge]
            primary = "test"

            [knowledge.roots]
            test = "{root.Path.Replace('\\', '/')}"

            [workspace]
            scope_map = "../evil-map.md"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml);
        var ex = Assert.Throws<InvalidOperationException>(() => LocalSettingsLoader.Load(path));
        Assert.Contains("relative under knowledge/", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetNotesPath_UsesPrimaryRootFromRuntime()
    {
        using var root = TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.Install(AgentNotesTestToml.WriteFromEmbeddedTemplate(root.Path));
        var storage = new NotesStorage();
        var notesPath = storage.GetNotesPath(Path.Combine(root.Path, "any-workspace"));
        Assert.Equal(Path.Combine(root.Path, "agent-notes.md"), notesPath);
    }

    [Fact]
    public void ResolveKnowledgeRoot_UsesRuntimeWithoutToolArgument()
    {
        using var root = TempKnowledgeRoot.Create();
        using var runtime = AgentNotesTestToml.Install(AgentNotesTestToml.WriteFromEmbeddedTemplate(root.Path));
        var resolved = NotesStorage.ResolveKnowledgeRoot(null);
        Assert.Equal(root.Path, resolved);
    }

    private sealed class TestEnvVarScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        private TestEnvVarScope(string name, string? previous)
        {
            _name = name;
            _previous = previous;
        }

        internal static TestEnvVarScope Clear(string name)
        {
            var previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
            return new TestEnvVarScope(name, previous);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }

    internal sealed class TempKnowledgeRoot : IDisposable
    {
        internal string Path { get; }

        private TempKnowledgeRoot(string path) => Path = path;

        internal static TempKnowledgeRoot Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AgentNotesMcpTests", "kb", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(path, "knowledge", "work", "local"));
            NotesStorageTests.SeedTestScopeAliasDefaultsForRoot(path);
            return new TempKnowledgeRoot(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to cleanup: {Path}", ex);
            }
        }
    }
}
