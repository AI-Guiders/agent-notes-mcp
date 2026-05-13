using AgentNotes.Core;

namespace AgentNotesMcp.Tests;

public sealed class EmbeddedDefaultsResourceTests
{
    [Fact]
    public void BundledAgentNotesContent_reads_hot_context_and_mcp_resolve_defaults_json()
    {
        Assert.True(BundledAgentNotesContent.TryReadEmbeddedText("hot-context-defaults.json", out var hot));
        Assert.False(string.IsNullOrWhiteSpace(hot));
        Assert.True(BundledAgentNotesContent.TryReadEmbeddedText("mcp-resolve-paths-defaults.json", out var mcp));
        Assert.False(string.IsNullOrWhiteSpace(mcp));
    }
}
