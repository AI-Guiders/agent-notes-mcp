# Agent Notes MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()` в репозитории. Не править этот блок вручную.
>
> Обновление: из каталога `agent-notes-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема аргументов — в `inputSchema` (например через `list_tools`).

### `memory_health`

Быстрый health-check памяти: размер hot-context, обязательные секции, предупреждения по бюджету и рекомендации по compaction. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).

### `route_context`

Подобрать релевантные секции из agent-notes.md по запросу и собрать компактный context-пакет (router-first). Не индексирует файлы knowledge/ — длинные playbook/kb подгружать отдельно через read_knowledge_file (напр. playbook-multi-project-context-v1.md, index-knowledge-router-v1.md). Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).

### `write_agent_notes`

Записать заметки агента (полная замена файла). Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md. ВНИМАНИЕ: перезаписывает файл целиком; для добавления блока без риска стереть остальное используйте append_agent_notes.

### `append_agent_notes`

Добавить блок в конец заметок агента без перезаписи файла. Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md.

### `read_agent_notes`

Прочитать заметки агента. Путь hot-файла: primary knowledge root из --config → {корень}/agent-notes.md; иначе workspace_path/.cascade-ide/agent-notes.md. Возвращает содержимое или пустую строку.

### `read_hot_context`

Прочитать только горячий контекст (L0/L1) без загрузки архивного хвоста. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → опционально current: в секции active-scope (легаси) → иначе встроенный fallback (door-to-singularity).

### `upsert_agent_notes_section`

Точечно вставить/обновить секцию заметок по section_id без полной перезаписи файла. Секция оформляется маркерами <!-- section:ID --> ... <!-- /section:ID -->. При дублях/unclosed/orphan close — REJECTED (без silent append). Путь hot-файла — как у read_agent_notes.

### `delete_agent_notes_section`

Удалить секцию заметок по section_id (блок между <!-- section:ID --> и <!-- /section:ID -->). Если секции нет — NO_CHANGES. Путь hot-файла — как у read_agent_notes; перед удалением сохраняется ревизия.

### `list_agent_notes_revisions`

Список ревизий заметок для rollback. Ревизии хранятся рядом с файлом заметок в подпапке .revisions.

### `rollback_agent_notes`

Откатить заметки к выбранной ревизии (или к последней, если revision_file не задан). Текущее содержимое перед откатом тоже сохраняется как ревизия.

### `search_agent_notes`

Поиск по заметкам с возвратом совпавших строк и номеров строк.

### `extract_from_archive`

Точечное извлечение фактов из архивной ревизии без чтения всего файла.

### `compact_hot_context`

Ужать hot-context: удалить дубли секций, нормализовать формат. По умолчанию preview, apply=true для записи.

### `validate_sections`

Проверить <!-- section:id --> разметку: ids, дубли, unclosed/orphan. Hot: workspace_path. Knowledge: file_path (+ knowledge_path|knowledge_root_id).

### `normalize_sections`

Починить разметку секций: дубли → keep last, убрать orphan/unclosed маркеры, канон блоков. По умолчанию preview; apply=true пишет. Hot: workspace_path. Knowledge: file_path.

### `write_knowledge_file`

Записать файл в каталог knowledge/ (полная замена). Перед записью текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true). Запись только в primary; read-only roots (knowledge_root_id=group) отклоняются.

### `append_knowledge_file`

Добавить блок в конец файла в knowledge/ без перезаписи. Перед добавлением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).

### `upsert_knowledge_section`

Вставить или обновить секцию в файле knowledge/ по section_id (маркеры <!-- section:ID --> ... <!-- /section:ID -->). Дубли/битая разметка → REJECTED. Перед изменением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).

### `delete_knowledge_file`

Удалить файл из каталога knowledge/. file_path — относительный путь (без '..'). Если файла нет — NO_CHANGES.

### `delete_knowledge_section`

Удалить секцию из файла knowledge/ по section_id (блок между <!-- section:ID --> и <!-- /section:ID -->). Если секции нет — NO_CHANGES.

### `read_knowledge_file`

Прочитать файл из каталога knowledge/. Корень: knowledge_path, knowledge_root_id (group, …) или primary из --config. Возвращает содержимое или пустую строку. Опционально offset (1-based) и limit. Для протоколов: playbook-multi-project-context-v1.md, index-knowledge-router-v1.md (route_context их не подставляет автоматически).

### `list_knowledge_files`

Список файлов в каталоге knowledge/ (без .revisions). Опционально subdir — подкаталог (например work). Возвращает path, size_bytes, modified_utc.

<!-- GENERATED:ToolCatalog END -->

