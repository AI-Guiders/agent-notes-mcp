using AgentNotes.Core;

namespace AgentNotesMcp.Tests;

/// <summary>Fixture TOML и <see cref="AgentNotesRuntime"/> для интеграционных тестов (MCP 2.0).</summary>
internal static class AgentNotesTestToml
{
    internal static string Write(
        string knowledgeRoot,
        string defaultScope = "door-to-singularity",
        string scopeMap = "work/local/workspace-scope-map-v1.md",
        string scopeAliases = "work/local/scope-alias-map-v1.md")
    {
        var root = knowledgeRoot.Replace('\\', '/');
        var toml = $"""
            version = 1

            [knowledge]
            primary = "test"

            [knowledge.roots]
            test = "{root}"

            [workspace]
            default_scope = "{defaultScope}"
            scope_map = "{scopeMap}"
            scope_aliases = "{scopeAliases}"

            [status]
            enabled = false
            port = 17341
            bind = "127.0.0.1"
            """;
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, toml);
        return path;
    }

    internal static string WriteFromEmbeddedTemplate(string knowledgeRoot)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.local.toml");
        var text = File.ReadAllText(templatePath).Replace("PLACEHOLDER_ROOT", knowledgeRoot.Replace('\\', '/'));
        var path = Path.Combine(Path.GetTempPath(), "AgentNotesMcpTests", $"cfg-{Guid.NewGuid():N}.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    internal static RuntimeScope Install(string tomlPath)
    {
        var settings = LocalSettingsLoader.Load(tomlPath);
        AgentNotesRuntime.Initialize(settings, tomlPath);
        return new RuntimeScope();
    }

    internal static RuntimeScope InstallForRoot(string knowledgeRoot, string? scopeMap = null, string? scopeAliases = null) =>
        Install(Write(
            knowledgeRoot,
            scopeMap: scopeMap ?? "work/local/workspace-scope-map-v1.md",
            scopeAliases: scopeAliases ?? "work/local/scope-alias-map-v1.md"));

    internal sealed class RuntimeScope : IDisposable
    {
        public void Dispose() => AgentNotesRuntime.ClearConfiguration();
    }
}
