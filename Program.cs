using System.Collections.Frozen;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tool = ModelContextProtocol.Protocol.Tool;

// MCP-сервер «Заметки агента»: запись и чтение в workspace/.cascade-ide/agent-notes.md.
// Подключается в Cursor без Cascade IDE — агент сохраняет и восстанавливает контекст между сессиями и до суммаризации.
// Если задана переменная окружения AGENT_NOTES_FILE (полный путь к файлу) — используется она; один файл заметок во всех окнах/репо.

static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

const string NotesDirName = ".cascade-ide";
const string NotesFileName = "agent-notes.md";
const string EnvNotesFile = "AGENT_NOTES_FILE";

var toolsList = new List<Tool>
{
    new()
    {
        Name = "write_agent_notes",
        Description = "Записать заметки агента (полная замена файла). Агент сам решает, когда, что и в каком формате сохранять. Путь: если задана переменная окружения AGENT_NOTES_FILE — используется она (один файл во всех workspace); иначе workspace_path/.cascade-ide/agent-notes.md. ВНИМАНИЕ: перезаписывает файл целиком; для добавления блока без риска стереть остальное используйте append_agent_notes.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (например корень проекта в Cursor). Здесь создаётся .cascade-ide/agent-notes.md." },
                content = new { type = "string", description = "Полное содержимое заметок (перезаписывает файл целиком)." }
            },
            required = new[] { "workspace_path", "content" }
        })
    },
    new()
    {
        Name = "append_agent_notes",
        Description = "Добавить блок в конец заметок агента без перезаписи файла. Безопасно: не трогает существующее содержимое. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md. Рекомендуется для добавления своего блока (Claude, Composer, другой агент), чтобы не стереть заметки других.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                content = new { type = "string", description = "Текст блока для добавления в конец файла (перед ним добавляется перевод строки, если нужно)." }
            },
            required = new[] { "workspace_path", "content" }
        })
    },
    new()
    {
        Name = "read_agent_notes",
        Description = "Прочитать заметки агента. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md. Возвращает содержимое или пустую строку. Агент восстанавливает контекст в новом чате.",
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
    var globalPath = Environment.GetEnvironmentVariable(EnvNotesFile);
    if (!string.IsNullOrWhiteSpace(globalPath))
        return Path.GetFullPath(globalPath.Trim());
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

static string HandleAppend(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("content", out var c))
        throw new ArgumentException("content is required.");
    var contentToAppend = c.GetString() ?? "";
    var filePath = GetNotesPath(workspacePath);
    var dir = Path.GetDirectoryName(filePath);
    if (string.IsNullOrEmpty(dir))
        throw new ArgumentException("Invalid workspace_path.");
    Directory.CreateDirectory(dir);
    var existing = File.Exists(filePath) ? File.ReadAllText(filePath, System.Text.Encoding.UTF8) : "";
    var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
    File.WriteAllText(filePath, existing + separator + contentToAppend, System.Text.Encoding.UTF8);
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
                    "append_agent_notes" => HandleAppend(args),
                    "read_agent_notes" => HandleRead(args),
                    _ => throw new ArgumentException($"Unknown tool: {name}.")
                };
                var isError = (name == "write_agent_notes" || name == "append_agent_notes") && text != "OK";
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
