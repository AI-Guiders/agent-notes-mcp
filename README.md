# Agent Notes MCP

MCP-сервер для долговременных заметок агента. Базовый файл: `workspace_path/.cascade-ide/agent-notes.md` (или путь из `AGENT_NOTES_FILE`). Для непрерывности между сессиями и до суммаризации — подключается в Cursor без Cascade IDE.

## Central Wiki

- Единая wiki команды: [Agent Notes Wiki](http://193.124.113.7/Krawler/agent-notes/-/wikis/home)
- В этом проекте wiki не дублируется; здесь только ссылка на центральный источник.

MLP-v1 в этом репозитории:

- атомарная запись;
- автоматические ревизии перед изменениями;
- безопасный append и точечный upsert по секциям;
- поиск по заметкам;
- rollback к ревизии.

Новые возможности v0.3.0:

- тонкая загрузка hot-context (`read_hot_context`);
- целевое извлечение из архивных ревизий (`extract_from_archive`);
- полуавтоматическое ужатие hot-context (`compact_hot_context`).

Новые возможности v0.4.0:

- health-check памяти (`memory_health`);
- router-first контекст-пакет по запросу (`route_context`);
- контракт `KB v2` для эксплуатационного режима (`KB-V2-CONTRACT.md`).

Новые возможности v0.5.0:

- запись и чтение файлов слоя **knowledge** канона: `write_knowledge_file` (полная замена), `append_knowledge_file` (добавить в конец без перезаписи), `upsert_knowledge_section` (точечное вставка/обновление секции по маркерам `<!-- section:ID -->`), `read_knowledge_file`. Путь к канону — `canon_path` или `AGENT_NOTES_CANON_PATH`. Меньше риска случайно перезаписать весь файл при точечных правках.

## Стек

- C#, .NET 10, win-x64, self-contained (как dotnet-debug-mcp и roslyn-mcp).

## Публикация

```bash
dotnet publish -c Release -o publish
```

Рекомендуется junction: например `D:\agent-notes-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\agent-notes-mcp\AgentNotesMcp.exe`, `args`: `[]`.

### Релизы (без Runner)

GitLab Runner не используется (нет Docker/Linux). Чтобы выложить win-x64 в релиз с твоего Windows:

1. Задай переменные: `GITLAB_URL` (например `http://193.124.113.7`), `GITLAB_TOKEN` (Personal Access Token с api).
2. Из корня репо:
   - только залить zip в Generic Package и добавить ссылку в **существующий** релиз:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08`
   - создать релиз от текущего коммита и привязать zip:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease`

Релиз должен уже существовать (создан вручную или через API), если не передаёшь `-CreateRelease`.

## Тулы

| Имя | Описание | Аргументы |
| ----- | ---------- | ---------- |
| `read_agent_notes` | Прочитать заметки. | `workspace_path` |
| `memory_health` | Быстрый health-check памяти: размер hot-context, обязательные секции, предупреждения по бюджету и рекомендации по compaction. | `workspace_path`, `active_scope?` |
| `route_context` | Подобрать релевантные секции под задачу и вернуть компактный assembled context. | `workspace_path`, `query`, `active_scope?`, `max_sections?`, `max_chars?` |
| `read_hot_context` | Прочитать только горячий контекст L0/L1 (без архивного хвоста). Список L0 (always load) **читается из секции `memory-architecture-v1`** (блок «### L0: Hot State» — буллеты `- section-id` до следующего `###`); при отсутствии или пустом разборе используется встроенный fallback. Затем добавляется секция scope из L1. Сначала берёт `active_scope`, иначе — из `workspace-scope-map-v1` или `active-scope.current`. | `workspace_path`, `active_scope?` |
| `write_agent_notes` | Записать заметки (**полная замена** файла). Перед заменой текущая версия сохраняется в ревизии. | `workspace_path`, `content` |
| `append_agent_notes` | Добавить блок в конец без полной перезаписи. Перед изменением создаётся ревизия. | `workspace_path`, `content` |
| `upsert_agent_notes_section` | Вставить/обновить секцию по `section_id` (через маркеры HTML-комментариев). | `workspace_path`, `section_id`, `content` |
| `search_agent_notes` | Поиск по заметкам (case-insensitive), возвращает строки и номера строк. | `workspace_path`, `query`, `head_limit?` |
| `extract_from_archive` | Поиск по конкретной/последней ревизии с контекстом строк. | `workspace_path`, `query`, `revision_file?`, `head_limit?`, `context_lines?` |
| `compact_hot_context` | Удалить дубли секций и нормализовать структуру hot-context (preview/apply). | `workspace_path`, `apply?` |
| `list_agent_notes_revisions` | Список доступных ревизий для отката. | `workspace_path`, `limit?` |
| `rollback_agent_notes` | Откатить заметки к выбранной (или последней) ревизии. | `workspace_path`, `revision_file?` |
| `write_knowledge_file` | Записать файл в **knowledge/** канона (полная замена). Для добавления блока — `append_knowledge_file`; для точечного обновления секции — `upsert_knowledge_section`. | `file_path`, `content`, `canon_path?` |
| `append_knowledge_file` | Добавить блок в конец файла в **knowledge/** без перезаписи. Безопасно: существующее содержимое сохраняется. | `file_path`, `content`, `canon_path?` |
| `upsert_knowledge_section` | Вставить/обновить секцию в файле **knowledge/** по `section_id` (маркеры `<!-- section:ID -->` … `<!-- /section:ID -->`). Точечное изменение. | `file_path`, `section_id`, `content`, `canon_path?` |
| `delete_knowledge_section` | Удалить секцию из файла **knowledge/** по `section_id`. Если секции нет — `NO_CHANGES`. | `file_path`, `section_id`, `canon_path?` |
| `delete_knowledge_file` | Удалить файл из **knowledge/** канона. Если файла нет — `NO_CHANGES`. | `file_path`, `canon_path?` |
| `read_knowledge_file` | Прочитать файл из **knowledge/** канона. | `file_path`, `canon_path?` |

`workspace_path` — каталог workspace (корень проекта в Cursor).  
Файл: `workspace_path/.cascade-ide/agent-notes.md`.  
Ревизии: `workspace_path/.cascade-ide/.revisions/*.md`.

### Слой knowledge (канон)

Инструменты `write_knowledge_file` и `read_knowledge_file` работают с каталогом **knowledge/** репозитория-канона (agent-notes), а не с текущим workspace. Это нужно, когда агент работает в другом workspace (например PersonalCursorFolder), а править надо файлы в каноне (index, kb-*, playbook-* и т.д.).

- **canon_path** — корень репо agent-notes (каталог, в котором лежит подкаталог `knowledge/`). Опционален, если задана переменная окружения **AGENT_NOTES_CANON_PATH**.
- **file_path** — относительный путь внутри `knowledge/`, например `kb-music-acoustics-v1.md`, `playbook-music-v1.md`. Не допускаются `..` и абсолютные пути.
- **Когда что использовать:** полная замена файла — `write_knowledge_file`; добавить блок в конец — `append_knowledge_file`; вставить/обновить секцию по ID — `upsert_knowledge_section`; удалить секцию по ID — `delete_knowledge_section`. Append, upsert и delete снижают риск затереть весь файл.
- Рекомендация: в окружении, где запускается MCP, задать `AGENT_NOTES_CANON_PATH=d:\Experiments\agent-notes` (или свой путь к канону), тогда при вызове можно не передавать `canon_path`.

## Формат секций для upsert

`upsert_agent_notes_section` использует маркеры:

```md
<!-- section:team-rules -->
... содержимое секции ...
<!-- /section:team-rules -->
```

Если секция уже есть — заменяется целиком; если нет — добавляется в конец файла.

### Workspace scope map (опционально)

Чтобы `read_hot_context` выбирал scope по workspace автоматически, можно добавить секцию:

```md
<!-- section:workspace-scope-map-v1 -->
- d:\Experiments\PersonalCursorFolder => current-projects
- c:\Projects\EDW.Portal.Repo => portal
<!-- /section:workspace-scope-map-v1 -->
```

Поддерживаются разделители `=>`, `:` и `=`.  
Матч по пути: сначала exact, иначе longest-prefix (с проверкой границы `\`), после нормализации `/` vs `\` и хвостового `\`.

## Репозиторий и субмодуль (legacy заметка)

Проект предназначен для отдельного репо на GitLab и подключения как субмодуль в репо **open** (financial-open), наряду с dotnet-debug-mcp, roslyn-mcp, cascade-ide.

Этот README и код в каталоге `open/agent-notes-mcp` отражают **legacy‑состояние** проекта (эксперименты до публикации Integrity POST и TPM‑архитектуры).  
Актуальный репозиторий и место публикации могут отличаться; конкретный URL и хост следует настраивать в своём окружении самостоятельно.

Пример добавления субмодуля в своём репозитории:

```bash
git submodule add <URL_ТВОЕГО_REPO_AGENT_NOTES_MCP> agent-notes-mcp
```

> Важно: любые ранние реализации integrity/POST из legacy‑репозиториев **не считаются** TPM‑корнями или авторитетными источниками для Integrity POST — валидными являются только версии `integrity-core` и спецификации, опубликованные через назначенные TPM‑узлы (см. `knowledge/META/integrity-post-spec-v1.md` и документы kb-public).
