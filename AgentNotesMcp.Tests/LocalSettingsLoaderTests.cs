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
        using var canon = TempKnowledgeRoot.Create();
        var tomlPath = WriteFixtureToml(canon.KnowledgeRootPath);
        var settings = LocalSettingsLoader.Load(tomlPath);
        Assert.Equal(canon.KnowledgeRootPath, settings.PrimaryKnowledgeRoot);
        Assert.Equal("door-to-singularity", settings.Workspace.DefaultScope);
        Assert.Equal("work/local/workspace-scope-map-v1.md", settings.Workspace.ScopeMapRelative);
    }

    [Fact]
    public void Loader_WithoutWorkspaceSection_UsesEmbeddedNeutralExample()
    {
        using var canon = TempKnowledgeRoot.Create();
        var toml = $"""
            version = 1

            [knowledge]
            primary = "test"

            [knowledge.roots]
            test = "{canon.KnowledgeRootPath.Replace('\\', '/')}"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml);
        var settings = LocalSettingsLoader.Load(path);
        Assert.Equal("example", settings.Workspace.DefaultScope);
        Assert.Equal("example/workspace-scope-map-v1.md", settings.Workspace.ScopeMapRelative);
    }

    [Fact]
    public void GetNotesPath_UsesPrimaryRootFromRuntime()
    {
        using var canon = TempKnowledgeRoot.Create();
        var tomlPath = WriteFixtureToml(canon.KnowledgeRootPath);
        using var scope = LocalSettingsScope.Install(LocalSettingsLoader.Load(tomlPath));
        var storage = new NotesStorage();
        var notesPath = storage.GetNotesPath(Path.Combine(canon.KnowledgeRootPath, "any-workspace"));
        Assert.Equal(Path.Combine(canon.KnowledgeRootPath, "agent-notes.md"), notesPath);
    }

    [Fact]
    public void ResolveKnowledgeRoot_UsesRuntimeWithoutToolArgument()
    {
        using var canon = TempKnowledgeRoot.Create();
        var tomlPath = WriteFixtureToml(canon.KnowledgeRootPath);
        using var scope = LocalSettingsScope.Install(LocalSettingsLoader.Load(tomlPath));
        var root = NotesStorage.ResolveKnowledgeRoot(null);
        Assert.Equal(canon.KnowledgeRootPath, root);
    }

    private static string WriteFixtureToml(string knowledgeRoot)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.local.toml");
        var text = File.ReadAllText(templatePath).Replace("PLACEHOLDER_ROOT", knowledgeRoot.Replace('\\', '/'));
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private sealed class LocalSettingsScope : IDisposable
    {
        private LocalSettingsScope() { }

        internal static LocalSettingsScope Install(LocalSettings settings)
        {
            AgentNotesRuntime.Initialize(settings);
            return new LocalSettingsScope();
        }

        public void Dispose() => AgentNotesRuntime.ResetForTests();
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

    private sealed class TempKnowledgeRoot : IDisposable
    {
        internal string KnowledgeRootPath { get; }

        private TempKnowledgeRoot(string path) => KnowledgeRootPath = path;

        internal static TempKnowledgeRoot Create()
        {
            var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", "kb", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(path, "knowledge", "work", "local"));
            NotesStorageTests.SeedTestScopeAliasDefaultsForRoot(path);
            return new TempKnowledgeRoot(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(KnowledgeRootPath))
                    Directory.Delete(KnowledgeRootPath, recursive: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to cleanup: {KnowledgeRootPath}", ex);
            }
        }
    }
}
