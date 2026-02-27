using System.Text.Json;
using ModelContextProtocol.Protocol;
using Tool = ModelContextProtocol.Protocol.Tool;

internal static class ToolCatalog
{
    private static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

    internal static List<Tool> Build() =>
    [
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
    ];
}
