using System.Text;
using System.Text.Json;
using AgentNotes.Core;

namespace AgentNotesMcp.Tests;

public sealed class LlmNativePackTests
{
    [Fact]
    public void GetDefinition_Reads_DebugRadius_From_TempPack()
    {
        using var root = TempKnowledgeRoot.Create();
        SeedAgentOpsPack(root.Path);
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();

        var json = storage.GetDefinition(null, "debug-radius", packId: "epistemic-scene");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("debug-radius", doc.RootElement.GetProperty("definition_id").GetString());
        Assert.Contains("shrink", doc.RootElement.GetProperty("llm_cue").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProcess_Returns_BugRadiusShrink_Gates()
    {
        using var root = TempKnowledgeRoot.Create();
        SeedAgentOpsPack(root.Path);
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();

        var json = storage.GetProcess(null, processId: "bug-radius-shrink", packId: "epistemic-scene");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("bug-radius-shrink", doc.RootElement.GetProperty("process").GetProperty("id").GetString());
        Assert.Equal("ask", doc.RootElement.GetProperty("suggested_next").GetProperty("policy").GetString());
    }

    [Fact]
    public void GetProcedure_Returns_KolbJournalPark_Steps()
    {
        using var root = TempKnowledgeRoot.Create();
        SeedAgentOpsPack(root.Path);
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();

        var json = storage.GetProcedure(null, procedureId: "kolb-journal-park", packId: "epistemic-scene");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("kolb-journal-park", doc.RootElement.GetProperty("procedure").GetProperty("id").GetString());
        Assert.Equal("curiosity-kolb-loop", doc.RootElement.GetProperty("procedure").GetProperty("related_process").GetString());
        Assert.Contains(
            doc.RootElement.GetProperty("procedure").GetProperty("steps").EnumerateArray().Select(e => e.GetString()),
            s => s is not null && s.Contains("JOURNAL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListPack_Includes_ProcedureIds()
    {
        using var root = TempKnowledgeRoot.Create();
        SeedAgentOpsPack(root.Path);
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();

        var json = storage.ListPack(null, packId: "epistemic-scene");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var ids = doc.RootElement.GetProperty("procedure_ids").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("kolb-journal-park", ids);
    }

    [Fact]
    public void RadiusGateCheck_Rejects_NonNegative_Delta()
    {
        var storage = new NotesStorage();
        var bad = JsonDocument.Parse(storage.RadiusGateCheck(0));
        Assert.False(bad.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("ask", bad.RootElement.GetProperty("policy").GetString());

        var good = JsonDocument.Parse(storage.RadiusGateCheck(-1, openHypothesisCount: 2));
        Assert.True(good.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("continue", good.RootElement.GetProperty("policy").GetString());
    }

    [Fact]
    public void AllowedRoots_Hides_OutOfScope_Pack()
    {
        using var root = TempKnowledgeRoot.Create();
        SeedAgentOpsPack(root.Path);
        using var runtime = AgentNotesTestToml.InstallForRoot(root.Path);
        var storage = new NotesStorage();

        var json = storage.GetDefinition(
            null,
            "debug-radius",
            packId: "epistemic-scene",
            allowedRoots: ["domains"]);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    private static void SeedAgentOpsPack(string repoRoot)
    {
        var pack = Path.Combine(repoRoot, "knowledge", "worlds", "epistemic-scene", "pack");
        Directory.CreateDirectory(Path.Combine(pack, "definitions"));
        File.WriteAllText(Path.Combine(pack, "pack.toml"),
            """
            id = "epistemic-scene"
            version = "0.1.0"
            layer = "scene"
            title = "test scene"
            onboarding = "load debug-radius"
            """);
        File.WriteAllText(Path.Combine(pack, "processes.toml"),
            """
            [[process]]
            id = "bug-radius-shrink"
            name = "Bug mode"
            apply_when = "defect"
            signals = ["exception_seen"]
            steps = ["fix", "list H"]
            gate = ["delta_radius < 0"]
            definition_anchors = ["debug-radius"]
            """);
        File.WriteAllText(Path.Combine(pack, "procedures.toml"),
            """
            [[procedure]]
            id = "kolb-journal-park"
            name = "Park journal"
            apply_when = "curiosity gap"
            signals = ["curiosity_gap"]
            steps = ["append JOURNAL.jsonl", "do not chat-only"]
            gate = ["line has settle"]
            definition_anchors = ["kolb-journal"]
            related_process = "curiosity-kolb-loop"
            llm_cue = "JOURNAL alone?"
            """);
        File.WriteAllText(Path.Combine(pack, "definitions", "debug-radius.md"),
            """
            # Debug Radius
            - id: debug-radius
            - kind: definition
            - llm_cue: Does this conclusion shrink debug-radius?
            - informal: remaining H size
            """, Encoding.UTF8);
        NotesStorageTests.SeedTestScopeAliasDefaultsForRoot(repoRoot);
    }

    private sealed class TempKnowledgeRoot : IDisposable
    {
        public string Path { get; }

        private TempKnowledgeRoot(string path) => Path = path;

        public static TempKnowledgeRoot Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "AgentNotesPackTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(path, "knowledge"));
            return new TempKnowledgeRoot(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
