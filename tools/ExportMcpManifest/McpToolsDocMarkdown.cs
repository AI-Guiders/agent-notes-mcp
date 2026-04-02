using System.Text;

namespace ExportMcpManifest;

/// <summary>Markdown для агента/доков из того же источника, что и <c>mcp-tools.manifest.json</c>.</summary>
internal static class McpToolsDocMarkdown
{
    public static string Build(IEnumerable<(string Name, string Description)> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Agent Notes MCP — каталог тулов");
        sb.AppendLine();
        sb.AppendLine("<!-- GENERATED:ToolCatalog START -->");
        sb.AppendLine();
        sb.AppendLine("> Автогенерация из `ToolCatalog.Build()` в репозитории. Не править этот блок вручную.");
        sb.AppendLine(">");
        sb.AppendLine("> Обновление: из каталога `agent-notes-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.");
        sb.AppendLine(">");
        sb.AppendLine("> Тексты совпадают с полем `description` у инструментов MCP; полная схема аргументов — в `inputSchema` (например через `list_tools`).");
        sb.AppendLine();

        foreach (var (name, description) in tools)
        {
            sb.AppendLine($"### `{name}`");
            sb.AppendLine();
            sb.AppendLine(description.TrimEnd());
            sb.AppendLine();
        }

        sb.AppendLine("<!-- GENERATED:ToolCatalog END -->");
        sb.AppendLine();
        return sb.ToString();
    }
}
