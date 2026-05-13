# Agent Notes MCP

[MCP](https://modelcontextprotocol.io)-сервер для **hot-заметок** (`agent-notes.md`) и **слоя `knowledge/`** канона (чтение/запись карточек и плейбуков). Удобно в **Cursor** и других хостах: переменные окружения указывают на канон или локальный файл заметок.

## Быстрый старт

```bash
git clone https://github.com/KarataevDmitry/agent-notes-mcp.git
cd agent-notes-mcp
dotnet build
dotnet publish AgentNotesMcp.csproj -c Release -o publish
```

В `mcp.json` укажи путь к **`AgentNotesMcp`** (или `.exe` под Windows) из каталога `publish`, плюс при необходимости **`AGENT_NOTES_CANON_PATH`** на корень клона **agent-notes** (полный канон с каталогом **`knowledge/`**). Только чтение публичного набора карточек — см. **[kb-public](https://github.com/KarataevDmitry/kb-public)** (запись в канон через него не подставляется). Подробнее про env и тулы — **[docs/MCP-TOOLS.md](docs/MCP-TOOLS.md)**.

## Лицензия

Код и документация **этого репозитория** — **MIT** ([`LICENSE`](LICENSE)). Тексты **KB** как контент — не MIT: публичный срез **[kb-public](https://github.com/KarataevDmitry/kb-public)** и [`knowledge/README.md` там](https://github.com/KarataevDmitry/kb-public/blob/main/knowledge/README.md). Сторонние пакеты — **[docs/THIRD-PARTY-NOTICES.md](docs/THIRD-PARTY-NOTICES.md)**.

Общая логика хранения — библиотека **[AIGuiders.AgentNotes.Core](https://www.nuget.org/packages/AIGuiders.AgentNotes.Core)** ([исходники](https://github.com/KarataevDmitry/AIGuiders.AgentNotes.Core)), MIT.

## Документация

| Что | Где |
|-----|-----|
| Имена тулов, аргументы, примеры | **[docs/MCP-TOOLS.md](docs/MCP-TOOLS.md)** и `mcp-tools.manifest.json` |
| Правила для `.cursor/rules` (Integrity POST, канон KB) | **[docs/cursor-rules-examples.md](docs/cursor-rules-examples.md)** |
| ADR по MCP и KB | **[docs/adr/](docs/adr/)** (канонические тексты также в репо **agent-notes**, `knowledge/adr/`) |
| Сборка и релизы (PowerShell), зеркала Git | **[docs/publishing-and-ci.md](docs/publishing-and-ci.md)** |

## Возможности (сжато)

- **Заметки:** атомарная запись, ревизии в `.revisions/`, поиск, rollback.
- **Hot-context:** `read_hot_context`, `extract_from_archive`, `compact_hot_context`, `memory_health`, `route_context`.
- **Knowledge:** `read_knowledge_file`, `write_knowledge_file`, `append_knowledge_file`, `upsert_knowledge_section`, `delete_knowledge_section` — пути относительно `knowledge/` в каноне; без `..` и абсолютных путей.
- **Контракты:** `KB-V2-CONTRACT.md`, `coexistence-framework-v1.md` — в репозитории.

Полнотекст по Markdown-дереву канона **не** в этом процессе: для поиска по ключевым словам — отдельный **[Hybrid Codebase Index](https://github.com/KarataevDmitry/hybrid-codebase-index)** и политика в каноне: `knowledge/adr/010-kb-markdown-fts-index-boundary.md`.

## Где лежит `agent-notes.md`

Приоритет: **`AGENT_NOTES_FILE`** → иначе **`{AGENT_NOTES_CANON_PATH}/agent-notes.md`** → иначе **`workspace_path/.cascade-ide/agent-notes.md`**. Ревизии — рядом с каталогом файла заметок: **`.revisions/*.md`**.

## Слой `knowledge/` (канон)

Тулы работают с **`knowledge/`** репозитория канона (не с текущим workspace), когда агент открыт в другом проекте, а править нужно индекс/kb/playbook в каноне.

- **`canon_path`** в вызове или **`AGENT_NOTES_CANON_PATH`**: корень репо с подкаталогом **`knowledge/`**. Если задан только **`AGENT_NOTES_FILE`**, корень канона может быть выведен вверх по дереву до предка с **`knowledge/`** (см. `NotesStorage.ResolveCanonPath` в коде).
- **`file_path`:** только внутри `knowledge/`, например `index-knowledge-router-v1.md`.

Правила публичной выгрузки KB — в каноне, **`knowledge/PUBLISHING.md`**; кратко про границу публикации — **`KB-V2-CONTRACT.md`**.

## Секции в `agent-notes.md` (upsert)

Инструмент **`upsert_agent_notes_section`** (и аналоги для hot-файла) ожидают маркеры:

```md
<!-- section:team-rules -->
... содержимое ...
<!-- /section:team-rules -->
```

## Workspace scope map (опционально)

Секция **`workspace-scope-map-v1`** в hot-файле сопоставляет путь workspace и scope, чтобы `read_hot_context` выбирал контекст:

```md
<!-- section:workspace-scope-map-v1 -->
- C:\src\my-app => door-to-singularity
- D:\work\portal => portal
<!-- /section:workspace-scope-map-v1 -->
```

Разделители строк: `=>`, `:` или `=`. Матч: exact, иначе longest-prefix по нормализованным путям.

Дефолтные пути к файлам карты workspace (если не переопределены в **`knowledge/META/mcp-resolve-paths-v1.json`**) — встроены в **AgentNotes.Core** (`mcp-resolve-paths-defaults.json`); см. **`docs/adr/008-workspace-scope-map-resolution.md`**.

## Два разных «корня»

| | Назначение | Откуда берётся путь |
|---|------------|---------------------|
| **Hot-файл** | секции, `read_hot_context`, `route_context` | цепочка **`AGENT_NOTES_FILE`** → **`{AGENT_NOTES_CANON_PATH}/agent-notes.md`** → **`workspace_path/.cascade-ide/...`** |
| **Канон `knowledge/`** | read/write knowledge | **`canon_path`** / **`AGENT_NOTES_CANON_PATH`** / вывод из пути к файлу заметок |

Частый случай: один раз **`AGENT_NOTES_CANON_PATH`** на корень клона **agent-notes** — и `agent-notes.md`, и **`knowledge/`** согласованы.

**`workspace_path`** в аргументах тула — текущий проект в Cursor; влияет на выбор scope по карте выше. На путь к `agent-notes.md` влияет только если оба env для заметок не заданы (локальный `.cascade-ide` под этим workspace).

## Участие

Issues и PR — на **GitHub**: [KarataevDmitry/agent-notes-mcp](https://github.com/KarataevDmitry/agent-notes-mcp).

Обновить описание тулов из кода:

```bash
dotnet run --project tools/ExportMcpManifest -- --write
```

(рабочий каталог — корень репозитория).
