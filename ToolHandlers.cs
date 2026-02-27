using System.Text.Json;

internal sealed class ToolHandlers
{
    private readonly NotesStorage _storage;

    internal ToolHandlers(NotesStorage storage)
    {
        _storage = storage;
    }

    internal bool IsWriteLikeTool(string name) =>
        name is
            "write_agent_notes" or
            "append_agent_notes" or
            "upsert_agent_notes_section" or
            "rollback_agent_notes" or
            "compact_hot_context";

    internal string Handle(string toolName, IReadOnlyDictionary<string, JsonElement> args) =>
        toolName switch
        {
            "write_agent_notes" => Write(args),
            "append_agent_notes" => Append(args),
            "read_agent_notes" => Read(args),
            "read_hot_context" => ReadHotContext(args),
            "upsert_agent_notes_section" => UpsertSection(args),
            "list_agent_notes_revisions" => ListRevisions(args),
            "rollback_agent_notes" => Rollback(args),
            "search_agent_notes" => Search(args),
            "extract_from_archive" => ExtractFromArchive(args),
            "compact_hot_context" => CompactHotContext(args),
            _ => throw new ArgumentException($"Unknown tool: {toolName}.")
        };

    private string Write(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var content = ToolArgs.RequiredString(args, "content");
        return _storage.Write(workspacePath, content);
    }

    private string Append(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var content = ToolArgs.RequiredString(args, "content");
        return _storage.Append(workspacePath, content);
    }

    private string Read(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        return _storage.Read(workspacePath);
    }

    private string UpsertSection(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var sectionId = ToolArgs.RequiredString(args, "section_id");
        var content = ToolArgs.RequiredString(args, "content");

        if (!ToolArgs.IsValidSectionId(sectionId))
            throw new ArgumentException("section_id must match ^[A-Za-z0-9._-]+$.");

        return _storage.UpsertSection(workspacePath, sectionId, content);
    }

    private string ListRevisions(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var limit = ToolArgs.GetIntOrDefault(args, "limit", 20, 1, 200);
        return _storage.ListRevisions(workspacePath, limit);
    }

    private string Rollback(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var revisionFile = ToolArgs.OptionalString(args, "revision_file");
        return _storage.Rollback(workspacePath, revisionFile);
    }

    private string Search(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var query = ToolArgs.RequiredString(args, "query");
        var limit = ToolArgs.GetIntOrDefault(args, "head_limit", 20, 1, 200);
        return _storage.Search(workspacePath, query, limit);
    }

    private string ReadHotContext(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var activeScope = ToolArgs.OptionalString(args, "active_scope");
        return _storage.ReadHotContext(workspacePath, activeScope);
    }

    private string ExtractFromArchive(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var query = ToolArgs.RequiredString(args, "query");
        var revisionFile = ToolArgs.OptionalString(args, "revision_file");
        var limit = ToolArgs.GetIntOrDefault(args, "head_limit", 10, 1, 100);
        var contextLines = ToolArgs.GetIntOrDefault(args, "context_lines", 2, 0, 20);
        return _storage.ExtractFromArchive(workspacePath, query, revisionFile, limit, contextLines);
    }

    private string CompactHotContext(IReadOnlyDictionary<string, JsonElement> args)
    {
        var workspacePath = ToolArgs.RequiredString(args, "workspace_path");
        var apply = ToolArgs.GetBoolOrDefault(args, "apply", false);
        return _storage.CompactHotContext(workspacePath, apply);
    }
}
