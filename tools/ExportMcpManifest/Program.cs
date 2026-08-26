using McpToolManifest;
using AgentNotesMcp;

var tools = ToolCatalog.Build().Select(t => (t.Name!, (string?)t.Description)).ToList();
return McpToolManifestExporter.Run(
    args,
    tools,
    new McpToolManifestExportOptions
    {
        McpId = "agent-notes-mcp",
        Title = "Agent Notes MCP",
        RepoFolderName = "agent-notes-mcp",
    });
