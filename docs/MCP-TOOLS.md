# Agent Notes MCP — каталог тулов

<!-- GENERATED:ToolCatalog START -->

> Автогенерация из `ToolCatalog.Build()` в репозитории. Не править этот блок вручную.
>
> Обновление: из каталога `agent-notes-mcp` выполнить `dotnet run --project tools/ExportMcpManifest -- --write`.
>
> Тексты совпадают с полем `description` у инструментов MCP; полная схема аргументов — в `inputSchema` (например через `list_tools`).

### `memory_health`

Быстрый health-check памяти: размер hot-context, обязательные секции, предупреждения по бюджету и рекомендации по compaction. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback door-to-singularity.

### `route_context`

Подобрать релевантные секции из agent-notes.md по запросу и собрать компактный context-пакет (router-first). Не индексирует файлы knowledge/ — длинные playbook/kb подгружать отдельно через read_knowledge_file (напр. playbook-multi-project-context-v1.md, index-knowledge-router-v1.md). Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback door-to-singularity.

### `write_agent_notes`

Записать заметки агента (полная замена файла). Агент сам решает, когда, что и в каком формате сохранять. Путь: если задана переменная окружения AGENT_NOTES_FILE — используется она (один файл во всех workspace); иначе workspace_path/.cascade-ide/agent-notes.md. ВНИМАНИЕ: перезаписывает файл целиком; для добавления блока без риска стереть остальное используйте append_agent_notes.

### `append_agent_notes`

Добавить блок в конец заметок агента без перезаписи файла. Безопасно: не трогает существующее содержимое. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md. Рекомендуется для добавления своего блока (Claude, Composer, другой агент), чтобы не стереть заметки других.

### `read_agent_notes`

Прочитать заметки агента. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md. Возвращает содержимое или пустую строку. Агент восстанавливает контекст в новом чате.

### `read_hot_context`

Прочитать только горячий контекст (L0/L1) без загрузки архивного хвоста. Резолв scope: active_scope (если передан) → workspace-scope-map-v1 (по workspace_path) → active-scope.current → fallback door-to-singularity.

### `upsert_agent_notes_section`

Точечно вставить/обновить секцию заметок по section_id без полной перезаписи файла. Секция оформляется маркерами <!-- section:ID --> ... <!-- /section:ID -->. Путь: AGENT_NOTES_FILE (если задана) иначе workspace_path/.cascade-ide/agent-notes.md.

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

### `write_knowledge_file`

Записать файл в каталог knowledge/ канона (полная замена). Перед записью текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true). Путь к канону: canon_path или AGENT_NOTES_CANON_PATH.

### `append_knowledge_file`

Добавить блок в конец файла в knowledge/ канона без перезаписи. Перед добавлением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).

### `upsert_knowledge_section`

Вставить или обновить секцию в файле knowledge/ по section_id (маркеры <!-- section:ID --> ... <!-- /section:ID -->). Перед изменением текущая версия сохраняется в knowledge/.revisions/ (если save_revision=true).

### `delete_knowledge_file`

Удалить файл из каталога knowledge/ канона. file_path — относительный путь (без '..'). Если файла нет — NO_CHANGES.

### `delete_knowledge_section`

Удалить секцию из файла knowledge/ по section_id (блок между <!-- section:ID --> и <!-- /section:ID -->). Если секции нет — NO_CHANGES.

### `read_knowledge_file`

Прочитать файл из каталога knowledge/ канона. Путь к канону: canon_path или AGENT_NOTES_CANON_PATH. Возвращает содержимое или пустую строку, если файла нет. Для протоколов и индекса роутера KB: playbook-multi-project-context-v1.md, index-knowledge-router-v1.md, agent-memory-and-operating-principles-v1.md (route_context их не подставляет автоматически).

### `list_knowledge_files`

Список файлов в каталоге knowledge/ канона (без .revisions). Опционально subdir — подкаталог (например work для knowledge/work/). Возвращает path, size_bytes, modified_utc для каждого файла.

<!-- GENERATED:ToolCatalog END -->

