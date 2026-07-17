using System.Text.Json;
using AgentNotesMcp.Status;
using ModelContextProtocol.Protocol;
using Tool = ModelContextProtocol.Protocol.Tool;

/// <summary>Каталог MCP-тулов. Согласован с <c>mcp-tools.manifest.json</c> и <c>docs/MCP-TOOLS.md</c> (генерация: <c>tools/ExportMcpManifest</c>, тесты <c>McpToolManifestTests</c>, <c>McpToolsDocTests</c>).</summary>
internal static class ToolCatalog
{
    private static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);

    internal static List<Tool> Build() =>
    [
        new()
        {
            Name = "memory_health",
            Description = "Быстрый health-check памяти: размер hot-context, обязательные секции, предупреждения по бюджету и рекомендации по compaction. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    active_scope = new { type = "string", description = "Опционально: door-to-singularity | portal | harvester | imc | mixed (алиасы: dts, cp; ptl→portal; hrv→harvester; legacy: current-projects)." }
                },
                required = new[] { "workspace_path" }
            })
        },
        new()
        {
            Name = "route_context",
            Description = "Подобрать релевантные секции из agent-notes.md по запросу и собрать компактный context-пакет (router-first). Не индексирует файлы knowledge/ — длинные playbook/kb подгружать отдельно через read_knowledge_file (напр. playbook-multi-project-context-v1.md, index-knowledge-router-v1.md). Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    query = new { type = "string", description = "Поисковый запрос или задача для маршрутизации контекста." },
                    active_scope = new { type = "string", description = "Опционально: door-to-singularity | portal | harvester | imc | mixed (алиасы: dts, cp; ptl→portal; hrv→harvester; legacy: current-projects)." },
                    max_sections = new { type = "integer", description = "Максимум секций в ответе (по умолчанию 5)." },
                    max_chars = new { type = "integer", description = "Бюджет символов для assembled_context (по умолчанию 12000)." }
                },
                required = new[] { "workspace_path", "query" }
            })
        },
        new()
        {
            Name = "write_agent_notes",
            Description = "Записать заметки агента (полная замена файла). Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md. ВНИМАНИЕ: перезаписывает файл целиком; для добавления блока без риска стереть остальное используйте append_agent_notes.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace (например корень проекта в Cursor). Нужен для резолва scope; hot-файл — из --config (primary root) или workspace_path/.cascade-ide/agent-notes.md." },
                    content = new { type = "string", description = "Полное содержимое заметок (перезаписывает файл целиком)." }
                },
                required = new[] { "workspace_path", "content" }
            })
        },
        new()
        {
            Name = "append_agent_notes",
            Description = "Добавить блок в конец заметок агента без перезаписи файла. Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md.",
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
            Description = "Прочитать заметки агента. Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md. Возвращает содержимое или пустую строку.",
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
            Description = "Прочитать только горячий контекст (L0/L1) без загрузки архивного хвоста. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace." },
                    active_scope = new { type = "string", description = "Опционально: door-to-singularity | portal | harvester | imc | mixed (алиасы: dts, cp; ptl→portal; hrv→harvester; legacy: current-projects)." }
                },
                required = new[] { "workspace_path" }
            })
        },
        new()
        {
            Name = "upsert_agent_notes_section",
            Description = "Точечно вставить/обновить секцию заметок по section_id без полной перезаписи файла. Секция оформляется маркерами <!-- section:ID --> ... <!-- /section:ID -->. При дублях/unclosed/orphan close — REJECTED (без silent append). Путь hot-файла — как у read_agent_notes.",
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
            Name = "delete_agent_notes_section",
            Description = "Удалить секцию заметок по section_id (блок между <!-- section:ID --> и <!-- /section:ID -->). Если секции нет — NO_CHANGES. Путь hot-файла — как у read_agent_notes; перед удалением сохраняется ревизия.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Каталог workspace (тот же, что при read/write)." },
                    section_id = new { type = "string", description = "ID секции для удаления (A-Za-z0-9._-)." }
                },
                required = new[] { "workspace_path", "section_id" }
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
            Name = "validate_sections",
            Description = "Проверить <!-- section:id --> разметку: ids, дубли, unclosed/orphan. Hot: workspace_path. Knowledge: file_path (+ knowledge_path|knowledge_root_id).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Hot agent-notes (если нет file_path)." },
                    knowledge_path = new { type = "string", description = "Корень knowledge (опционально)." },
                    knowledge_root_id = new { type = "string", description = "id корня из --config (опционально)." },
                    file_path = new { type = "string", description = "Относительный путь в knowledge/." }
                }
            })
        },
        new()
        {
            Name = "normalize_sections",
            Description = "Починить разметку секций: дубли → keep last, убрать orphan/unclosed маркеры, канон блоков. По умолчанию preview; apply=true пишет. Hot: workspace_path. Knowledge: file_path.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    workspace_path = new { type = "string", description = "Hot agent-notes (если нет file_path)." },
                    knowledge_path = new { type = "string", description = "Корень knowledge (опционально)." },
                    knowledge_root_id = new { type = "string", description = "id корня из --config (опционально)." },
                    file_path = new { type = "string", description = "Относительный путь в knowledge/." },
                    apply = new { type = "boolean", description = "true — записать, false — превью JSON." },
                    save_revision = new { type = "boolean", description = "Для knowledge: revision перед записью (по умолчанию true)." }
                }
            })
        },
        new()
        {
            Name = "write_knowledge_file",
            Description = "Записать файл в каталог knowledge/ (полная замена). Перед записью текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true). Запись только в primary; read-only roots (knowledge_root_id=group) отклоняются.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
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
            Description = "Добавить блок в конец файла в knowledge/ без перезаписи. Перед добавлением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
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
            Description = "Вставить или обновить секцию в файле knowledge/ по section_id (маркеры <!-- section:ID --> ... <!-- /section:ID -->). Дубли/битая разметка → REJECTED. Перед изменением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
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
            Description = "Удалить файл из каталога knowledge/. file_path — относительный путь (без '..'). Если файла нет — NO_CHANGES.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
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
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/." },
                    section_id = new { type = "string", description = "ID секции для удаления (A-Za-z0-9._-)." }
                },
                required = new[] { "file_path", "section_id" }
            })
        },
        new()
        {
            Name = "read_knowledge_file",
            Description = "Прочитать файл из каталога knowledge/. Корень: knowledge_path, knowledge_root_id (group, …) или primary из --config. Возвращает содержимое или пустую строку. Опционально offset (1-based) и limit. Для протоколов: playbook-multi-project-context-v1.md, index-knowledge-router-v1.md (route_context их не подставляет автоматически).",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
                    file_path = new { type = "string", description = "Относительный путь внутри knowledge/, например kb-music-theory-fundamentals-v1.md." },
                    offset = new { type = "integer", description = "Опционально. Номер первой возвращаемой строки, нумерация с 1. Без offset и limit — весь файл." },
                    limit = new { type = "integer", description = "Опционально. Максимум строк в ответе (после offset). 0 = пусто. Без limit — до конца файла." }
                },
                required = new[] { "file_path" }
            })
        },
        new()
        {
            Name = "list_knowledge_files",
            Description = "Список файлов в каталоге knowledge/ (без .revisions). Опционально subdir — подкаталог (например work). Возвращает path, size_bytes, modified_utc.",
            InputSchema = Schema(new
            {
                type = "object",
                properties = new
                {
                    knowledge_path = new { type = "string", description = "Корень репозитория knowledge (каталог с подпапкой knowledge/). Опционально: primary из --config. Не задавать вместе с knowledge_root_id." },
                    knowledge_root_id = new { type = "string", description = "Опционально. id из [knowledge.roots] или [[knowledge.read_only]] (напр. group). Чтение — любой корень; запись — только primary (user)." },
                    subdir = new { type = "string", description = "Подкаталог внутри knowledge/ (пусто = весь knowledge/). Например work." }
                },
                required = Array.Empty<string>()
            })
        }
    ];

    internal static IReadOnlyList<AgentNotesStatusSnapshot.ToolSummary> ListSummaries() =>
        Build()
            .Select(t => new AgentNotesStatusSnapshot.ToolSummary(t.Name, t.Description ?? ""))
            .ToArray();
}
