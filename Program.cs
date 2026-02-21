using System.Collections.Frozen;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tool = ModelContextProtocol.Protocol.Tool;

// MCP-сервер «Заметки агента»: запись и чтение в workspace/.cascade-ide/agent-notes.md.
// Подключается в Cursor без Cascade IDE — агент сохраняет и восстанавливает контекст между сессиями и до суммаризации.

static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

const string NotesDirName = ".cascade-ide";
const string NotesFileName = "agent-notes.md";

var toolsList = new List<Tool>
{
    new()
    {
        Name = "write_agent_notes",
        Description = "Записать заметки агента. Агент сам решает, когда, что и в каком формате сохранять (markdown, json, текст). Хранятся в workspace_path/.cascade-ide/agent-notes.md. Для непрерывности между сессиями и до суммаризации.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (например корень проекта в Cursor). Здесь создаётся .cascade-ide/agent-notes.md." },
                content = new { type = "string", description = "Полное содержимое заметок (перезаписывает файл)." }
            },
            required = new[] { "workspace_path", "content" }
        })
    },
    new()
    {
        Name = "read_agent_notes",
        Description = "Прочитать заметки агента из workspace_path/.cascade-ide/agent-notes.md. Возвращает содержимое или пустую строку. Агент восстанавливает контекст в новом чате.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при записи)." }
            },
            required = new[] { "workspace_path" }
        })
    }
};

static string GetNotesPath(string workspacePath)
{
    var root = Path.GetFullPath(workspacePath.Trim());
    if (File.Exists(root))
        root = Path.GetDirectoryName(root) ?? root;
    return Path.Combine(root, NotesDirName, NotesFileName);
}

static string HandleWrite(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("content", out var c))
        throw new ArgumentException("content is required.");
    var content = c.GetString() ?? "";
    var filePath = GetNotesPath(workspacePath);
    var dir = Path.GetDirectoryName(filePath);
    if (string.IsNullOrEmpty(dir))
        throw new ArgumentException("Invalid workspace_path.");
    Directory.CreateDirectory(dir);
    File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
    return "OK";
}

static string HandleRead(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    var filePath = GetNotesPath(workspacePath);
    if (!File.Exists(filePath))
        return "";
    return File.ReadAllText(filePath, System.Text.Encoding.UTF8);
}

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "AgentNotesMcp", Version = "0.1.0" },
    ProtocolVersion = "2024-11-05",
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = toolsList }),

        CallToolHandler = (request, cancellationToken) =>
        {
            var name = request.Params?.Name ?? "";
            var args = request.Params?.Arguments is IReadOnlyDictionary<string, JsonElement> a
                ? a
                : FrozenDictionary<string, JsonElement>.Empty;
            try
            {
                var text = name switch
                {
                    "write_agent_notes" => HandleWrite(args),
                    "read_agent_notes" => HandleRead(args),
                    _ => throw new ArgumentException($"Unknown tool: {name}.")
                };
                var isError = name == "write_agent_notes" && text != "OK";
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = text }],
                    IsError = isError
                });
            }
            catch (ArgumentException ex)
            {
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Error: {ex.Message}" }],
                    IsError = true
                });
            }
            catch (Exception ex)
            {
                return ValueTask.FromResult(new CallToolResult
                {
                    Content = [new TextContentBlock { Text = "Error: " + ex.Message }],
                    IsError = true
                });
            }
        }
    }
};

var transport = new StdioServerTransport("AgentNotesMcp");
await using var server = McpServer.Create(transport, options);
await server.RunAsync();
return 0;
