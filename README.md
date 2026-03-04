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

## Стек

- C#, .NET 10, win-x64, self-contained (как dotnet-debug-mcp и roslyn-mcp).

## Публикация

```bash
dotnet publish -c Release -o publish
```

Рекомендуется junction: например `D:\agent-notes-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\agent-notes-mcp\AgentNotesMcp.exe`, `args`: `[]`.

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

`workspace_path` — каталог workspace (корень проекта в Cursor).  
Файл: `workspace_path/.cascade-ide/agent-notes.md`.  
Ревизии: `workspace_path/.cascade-ide/.revisions/*.md`.

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

## Репозиторий и субмодуль

Проект предназначен для отдельного репо на GitLab и подключения как субмодуль в репо **open** (financial-open), наряду с dotnet-debug-mcp, roslyn-mcp, cascade-ide.

После создания репо на GitLab (например `Krawler/agent-notes-mcp`), пуша туда этого кода и перехода в каталог `open` выполнить:

```bash
git submodule add http://193.124.113.7/Krawler/agent-notes-mcp.git agent-notes-mcp
```
