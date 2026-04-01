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
            Name = "memory_health",
            Description = "Быстрый health-check памяти: размер hot-context, обязательные секции, предупреждения по бюджету и рекомендации по compaction. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback current-projects.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    active_scope = new { type = "string", description = "Опционально: current-projects | portal | mixed." }
                },
                required = new[] { "workspace_path" }
            })
        },
        new()
        {
            Name = "route_context",
            Description = "Подобрать релевантные секции из agent-notes по запросу и собрать компактный context-пакет (router-first). Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback current-projects.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    query = new { type = "string", description = "Поисковый запрос или задача для маршрутизации контекста." },
                    active_scope = new { type = "string", description = "Опционально: current-projects | portal | mixed." },
                    max_sections = new { type = "integer", description = "Максимум секций в ответе (по умолчанию 5)." },
                    max_chars = new { type = "integer", description = "Бюджет символов для assembled_context (по умолчанию 12000)." }
                },
                required = new[] { "workspace_path", "query" }
            })
        },
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
            Name = "read_hot_context",
            Description = "Прочитать только горячий контекст (L0/L1) без загрузки архивного хвоста. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback current-projects.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    active_scope = new { type = "string", description = "Опционально: current-projects | portal | mixed." }
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
        },
        new()
        {
            Name = "extract_from_archive",
            Description = "Точечное извлечение фактов из архивной ревизии без чтения всего файла.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    query = new { type = "string", description = "Подстрока для поиска в архивной ревизии." },
                    revision_file = new { type = "string", description = "Имя ревизии. Если не задано — берется последняя." },
                    head_limit = new { type = "integer", description = "Сколько совпадений вернуть (по умолчанию 10)." },
                    context_lines = new { type = "integer", description = "Контекст строк вокруг совпадения (по умолчанию 2)." }
                },
                required = new[] { "workspace_path", "query" }
            })
        },
        new()
        {
            Name = "compact_hot_context",
            Description = "Ужать hot-context: удалить дубли секций, нормализовать формат. По умолчанию preview, apply=true для записи.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    apply = new { type = "boolean", description = "true — применить изменения, false — только превью." }
                },
                required = new[] { "workspace_path" }
            })
        },
        new()
        {
            Name = "write_knowledge_file",
            Description = "Записать файл в каталог knowledge/ канона (полная замена). Перед записью текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true). Путь к канону: canon_path или AGENT_NOTES_CANON_PATH.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/, например kb-music-acoustics-v1.md (без '..' и без абсолютного пути)." },
                    content = new { type = "string", description = "Полное содержимое файла." },
                    save_revision = new { type = "boolean", description = "Сохранить текущую версию в knowledge/.revisions/ перед записью (по умолчанию true)." }
                },
                required = new[] { "file_path", "content" }
            })
        },
        new()
        {
            Name = "append_knowledge_file",
            Description = "Добавить блок в конец файла в knowledge/ канона без перезаписи. Перед добавлением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/." },
                    content = new { type = "string", description = "Текст для добавления в конец файла (перед ним при необходимости добавляется перевод строки)." },
                    save_revision = new { type = "boolean", description = "Сохранить текущую версию в knowledge/.revisions/ перед добавлением (по умолчанию true)." }
                },
                required = new[] { "file_path", "content" }
            })
        },
        new()
        {
            Name = "upsert_knowledge_section",
            Description = "Вставить или обновить секцию в файле knowledge/ по section_id (маркеры <!-- section:ID --> ... <!-- /section:ID -->). Перед изменением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/, например index-knowledge-router-v1.md." },
                    section_id = new { type = "string", description = "Стабильный ID секции (A-Za-z0-9._-)." },
                    content = new { type = "string", description = "Новое содержимое секции." },
                    save_revision = new { type = "boolean", description = "Сохранить текущую версию в knowledge/.revisions/ перед изменением (по умолчанию true)." }
                },
                required = new[] { "file_path", "section_id", "content" }
            })
        },
        new()
        {
            Name = "delete_knowledge_file",
            Description = "Удалить файл из каталога knowledge/ канона. file_path — относительный путь (без '..'). Если файла нет — NO_CHANGES.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/, например mcp-test-irl.md." }
                },
                required = new[] { "file_path" }
            })
        },
        new()
        {
            Name = "delete_knowledge_section",
            Description = "Удалить секцию из файла knowledge/ по section_id (блок между <!-- section:ID --> и <!-- /section:ID -->). Если секции нет — NO_CHANGES.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/." },
                    section_id = new { type = "string", description = "ID секции для удаления (A-Za-z0-9._-)." }
                },
                required = new[] { "file_path", "section_id" }
            })
        },
        new()
        {
            Name = "read_knowledge_file",
            Description = "Прочитать файл из каталога knowledge/ канона. Путь к канону: canon_path или AGENT_NOTES_CANON_PATH. Возвращает содержимое или пустую строку, если файла нет.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/, например kb-music-theory-fundamentals-v1.md." }
                },
                required = new[] { "file_path" }
            })
        },
        new()
        {
            Name = "list_knowledge_files",
            Description = "Список файлов в каталоге knowledge/ канона (без .revisions). Опционально subdir — подкаталог (например work для knowledge/work/). Возвращает path, size_bytes, modified_utc для каждого файла.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    canon_path = new { type = "string", description = "Корень репо agent-notes. Опционально, если задана AGENT_NOTES_CANON_PATH." },
                    subdir = new { type = "string", description = "Подкаталог внутри knowledge/ (пусто = весь knowledge/). Например work." }
                },
                required = Array.Empty<string>()
            })
        }
    ];
}
