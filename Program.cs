using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
const string RevisionsDirName = ".revisions";
var fileIoLock = new object();

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
    },
    new()
    {
        Name = "upsert_agent_notes_section",
        Description = "Точечно вставить/обновить секцию заметок по section_id без полной перезаписи файла. Секция оформляется маркерами <!-- section:ID --> ... <!-- /section:ID -->. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                section_id = new { type = "string", description = "Стабильный ID секции (латиница/цифры/._-)." },
                content = new { type = "string", description = "Новое содержимое секции." }
            },
            required = new[] { "workspace_path", "section_id", "content" }
        })
    },
    new()
    {
        Name = "list_agent_notes_revisions",
        Description = "Список ревизий заметок для rollback. Ревизии хранятся рядом с файлом заметок в подпапке .revisions.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                limit = new { type = "integer", description = "Максимум ревизий в ответе (по умолчанию 20)." }
            },
            required = new[] { "workspace_path" }
        })
    },
    new()
    {
        Name = "rollback_agent_notes",
        Description = "Откатить заметки к выбранной ревизии (или к последней, если revision_file не задан). Текущее содержимое перед откатом тоже сохраняется как ревизия.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                revision_file = new { type = "string", description = "Имя файла ревизии из list_agent_notes_revisions (опционально)." }
            },
            required = new[] { "workspace_path" }
        })
    },
    new()
    {
        Name = "search_agent_notes",
        Description = "Поиск по заметкам с возвратом совпавших строк и номеров строк.",
        InputSchema = Schema(new
        {
            type = "object",
            properties = new
            {
                workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                query = new { type = "string", description = "Подстрока для поиска (case-insensitive)." },
                head_limit = new { type = "integer", description = "Сколько совпадений вернуть (по умолчанию 20)." }
            },
            required = new[] { "workspace_path", "query" }
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

static string GetRevisionsDir(string notesPath)
{
    var dir = Path.GetDirectoryName(notesPath);
    if (string.IsNullOrWhiteSpace(dir))
        throw new ArgumentException("Invalid notes path.");
    return Path.Combine(dir, RevisionsDirName);
}

static string ComputeShortHash(string content)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(hash[..4]).ToLowerInvariant();
}

static string NormalizeReason(string reason)
{
    var normalized = Regex.Replace(reason.ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-');
    return string.IsNullOrWhiteSpace(normalized) ? "update" : normalized;
}

static void WriteRevisionSnapshot(string notesPath, string snapshotContent, string reason)
{
    var revisionsDir = GetRevisionsDir(notesPath);
    Directory.CreateDirectory(revisionsDir);
    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var revisionName = $"{timestamp}-{NormalizeReason(reason)}-{ComputeShortHash(snapshotContent)}.md";
    var revisionPath = Path.Combine(revisionsDir, revisionName);
    File.WriteAllText(revisionPath, snapshotContent, Encoding.UTF8);
}

static void AtomicWriteAllText(string path, string content)
{
    var dir = Path.GetDirectoryName(path);
    if (string.IsNullOrWhiteSpace(dir))
        throw new ArgumentException("Invalid target path.");
    Directory.CreateDirectory(dir);
    var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    File.WriteAllText(tempPath, content, Encoding.UTF8);
    File.Move(tempPath, path, true);
}

string SaveWithRevision(string notesPath, string newContent, string reason)
{
    lock (fileIoLock)
    {
        var hasCurrent = File.Exists(notesPath);
        var currentContent = hasCurrent ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
        if (currentContent == newContent)
            return "NO_CHANGES";
        if (hasCurrent)
            WriteRevisionSnapshot(notesPath, currentContent, reason);
        AtomicWriteAllText(notesPath, newContent);
        return "OK";
    }
}

static int GetIntArg(IReadOnlyDictionary<string, JsonElement> args, string key, int defaultValue, int min, int max)
{
    if (!args.TryGetValue(key, out var raw))
        return defaultValue;
    int value;
    if (raw.ValueKind == JsonValueKind.Number)
    {
        value = raw.GetInt32();
    }
    else if (raw.ValueKind == JsonValueKind.String && int.TryParse(raw.GetString(), out var parsed))
    {
        value = parsed;
    }
    else
    {
        return defaultValue;
    }
    return Math.Clamp(value, min, max);
}

static bool IsValidSectionId(string sectionId) => Regex.IsMatch(sectionId, "^[A-Za-z0-9._-]+$");

string HandleWrite(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("content", out var c))
        throw new ArgumentException("content is required.");
    var content = c.GetString() ?? "";
    var filePath = GetNotesPath(workspacePath);
    return SaveWithRevision(filePath, content, "write");
}

string HandleAppend(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("content", out var c))
        throw new ArgumentException("content is required.");
    var contentToAppend = c.GetString() ?? "";
    var filePath = GetNotesPath(workspacePath);
    var existing = File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : "";
    var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
    var newContent = existing + separator + contentToAppend;
    return SaveWithRevision(filePath, newContent, "append");
}

string HandleRead(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    var filePath = GetNotesPath(workspacePath);
    if (!File.Exists(filePath))
        return "";
    return File.ReadAllText(filePath, Encoding.UTF8);
}

string HandleUpsertSection(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("section_id", out var sid) || sid.GetString() is not { } sectionId || string.IsNullOrWhiteSpace(sectionId))
        throw new ArgumentException("section_id is required.");
    if (!IsValidSectionId(sectionId))
        throw new ArgumentException("section_id must match ^[A-Za-z0-9._-]+$.");
    if (!args.TryGetValue("content", out var c))
        throw new ArgumentException("content is required.");

    var content = c.GetString() ?? "";
    var notesPath = GetNotesPath(workspacePath);
    var existing = File.Exists(notesPath) ? File.ReadAllText(notesPath, Encoding.UTF8) : "";
    var startMarker = $"<!-- section:{sectionId} -->";
    var endMarker = $"<!-- /section:{sectionId} -->";
    var sectionBlock = $"{startMarker}\n{content}\n{endMarker}\n";
    var pattern = $"{Regex.Escape(startMarker)}\\R?[\\s\\S]*?\\R?{Regex.Escape(endMarker)}\\R?";

    string nextContent;
    if (Regex.IsMatch(existing, pattern))
    {
        var sectionRegex = new Regex(pattern);
        nextContent = sectionRegex.Replace(existing, sectionBlock, 1);
    }
    else
    {
        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "";
        nextContent = existing + separator + sectionBlock;
    }

    return SaveWithRevision(notesPath, nextContent, $"upsert-{sectionId}");
}

string HandleListRevisions(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");

    var limit = GetIntArg(args, "limit", 20, 1, 200);
    var notesPath = GetNotesPath(workspacePath);
    var revisionsDir = GetRevisionsDir(notesPath);
    if (!Directory.Exists(revisionsDir))
        return "[]";

    var revisions = Directory.GetFiles(revisionsDir, "*.md")
        .OrderByDescending(Path.GetFileName)
        .Take(limit)
        .Select(path =>
        {
            var info = new FileInfo(path);
            return new
            {
                file = Path.GetFileName(path),
                size_bytes = info.Length,
                modified_utc = info.LastWriteTimeUtc.ToString("O")
            };
        })
        .ToArray();

    return JsonSerializer.Serialize(revisions, new JsonSerializerOptions { WriteIndented = true });
}

string HandleRollback(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");

    var notesPath = GetNotesPath(workspacePath);
    var revisionsDir = GetRevisionsDir(notesPath);
    if (!Directory.Exists(revisionsDir))
        throw new ArgumentException("No revisions found.");

    string? requested = null;
    if (args.TryGetValue("revision_file", out var rev) && rev.GetString() is { } value && !string.IsNullOrWhiteSpace(value))
        requested = value.Trim();

    var revisionFile = requested
        ?? Directory.GetFiles(revisionsDir, "*.md")
            .Select(Path.GetFileName)
            .OrderByDescending(name => name)
            .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(revisionFile))
        throw new ArgumentException("No revisions found.");

    var revisionPath = Path.Combine(revisionsDir, revisionFile);
    if (!File.Exists(revisionPath))
        throw new ArgumentException("revision_file not found.");

    var target = File.ReadAllText(revisionPath, Encoding.UTF8);
    var result = SaveWithRevision(notesPath, target, $"rollback-{Path.GetFileNameWithoutExtension(revisionFile)}");
    return result == "NO_CHANGES" ? $"NO_CHANGES ({revisionFile})" : $"OK ({revisionFile})";
}

string HandleSearch(IReadOnlyDictionary<string, JsonElement> args)
{
    if (!args.TryGetValue("workspace_path", out var wp) || wp.GetString() is not { } workspacePath || string.IsNullOrWhiteSpace(workspacePath))
        throw new ArgumentException("workspace_path is required.");
    if (!args.TryGetValue("query", out var q) || q.GetString() is not { } query || string.IsNullOrWhiteSpace(query))
        throw new ArgumentException("query is required.");

    var limit = GetIntArg(args, "head_limit", 20, 1, 200);
    var notes = HandleRead(args);
    var lines = notes.Replace("\r\n", "\n").Split('\n');
    var totalMatches = 0;
    var returned = new List<object>();

    for (var i = 0; i < lines.Length; i++)
    {
        if (!lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
            continue;
        totalMatches++;
        if (returned.Count < limit)
        {
            returned.Add(new
            {
                line = i + 1,
                text = lines[i]
            });
        }
    }

    var payload = new
    {
        query,
        total_matches = totalMatches,
        returned_matches = returned.Count,
        matches = returned
    };
    return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
}

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "AgentNotesMcp", Version = "0.2.0" },
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
                    "upsert_agent_notes_section" => HandleUpsertSection(args),
                    "list_agent_notes_revisions" => HandleListRevisions(args),
                    "rollback_agent_notes" => HandleRollback(args),
                    "search_agent_notes" => HandleSearch(args),
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
