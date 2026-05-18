# Agent Notes MCP

[MCP](https://modelcontextprotocol.io)-сервер для **hot-заметок** (`agent-notes.md`) и **слоя `knowledge/`** (чтение/запись карточек и плейбуков). Версия **2.0** настраивается через **локальный TOML** (`--config`), как DBHub.

## Быстрый старт

```bash
git clone https://github.com/AI-Guiders/agent-notes-mcp.git
cd agent-notes-mcp
dotnet build
dotnet publish AgentNotesMcp.csproj -c Release -o publish
```

Скопируй и отредактируй пример конфига: **`config/agent-notes-mcp.toml`** (пути `[knowledge.roots]`, `[workspace]`). После `publish-and-deploy.ps1` тот же файл попадает рядом с exe.

В **`mcp.json`**:

```json
{
  "mcpServers": {
    "agent-notes": {
      "command": "D:\\agent-notes-mcp\\AgentNotesMcp.exe",
      "args": ["--config", "D:/agent-notes-mcp/agent-notes-mcp.toml"],
      "env": {}
    }
  }
}
```

Без **`--config`** процесс завершится с ошибкой (fail fast). Переменная **`AGENT_NOTES_CONFIG`** — альтернатива пути к TOML.

Публичный срез KB (только чтение) — **[kb-public](https://github.com/KarataevDmitry/kb-public)**. Подробнее по тулам — **[docs/MCP-TOOLS.md](docs/MCP-TOOLS.md)**.

## Лицензия

Код и документация **этого репозитория** — **MIT** ([`LICENSE`](LICENSE)). Тексты **KB** как контент — не MIT: публичный срез **[kb-public](https://github.com/KarataevDmitry/kb-public)** и [`knowledge/README.md` там](https://github.com/KarataevDmitry/kb-public/blob/main/knowledge/README.md). Сторонние пакеты — **[docs/THIRD-PARTY-NOTICES.md](docs/THIRD-PARTY-NOTICES.md)**.

Общая логика хранения — библиотека **[AIGuiders.AgentNotes.Core](https://www.nuget.org/packages/AIGuiders.AgentNotes.Core)** 2.x ([исходники](https://github.com/AI-Guiders/AIGuiders.AgentNotes.Core)), MIT.

## Документация

| Что | Где |
|-----|-----|
| Имена тулов, аргументы, примеры | **[docs/MCP-TOOLS.md](docs/MCP-TOOLS.md)** и `mcp-tools.manifest.json` |
| Локальный TOML (`--config`) | **[docs/adr/014-agent-notes-local-settings-toml-v1.md](docs/adr/014-agent-notes-local-settings-toml-v1.md)** |
| Правила для `.cursor/rules` (Integrity POST, канон KB) | **[docs/cursor-rules-examples.md](docs/cursor-rules-examples.md)** |
| ADR по MCP и KB | **[docs/adr/](docs/adr/)** (канон также в репо **agent-notes**, `knowledge/adr/`) |
| Чистая установка (новый пользователь) | Playbook: `knowledge/domains/agent-operations/playbook-knowledge-stack-clean-setup-v1.md`; шаблоны: `knowledge/templates/newcomer/` (kb-public) |
| Сборка и релизы (PowerShell), зеркала Git | **[docs/publishing-and-ci.md](docs/publishing-and-ci.md)** |

## Возможности (сжато)

- **Заметки:** атомарная запись, ревизии в `.revisions/`, поиск, rollback.
- **Hot-context:** `read_hot_context`, `extract_from_archive`, `compact_hot_context`, `memory_health`, `route_context`.
- **Knowledge:** `read_knowledge_file`, `write_knowledge_file`, … — пути относительно `knowledge/`; корень — **`knowledge_path`** в туле или **primary root** из TOML.
- **Контракты:** `KB-V2-CONTRACT.md`, `coexistence-framework-v1.md` — в репозитории канона.

Полнотекст по Markdown-дереву канона **не** в этом процессе: для поиска — **[Hybrid Codebase Index](https://github.com/KarataevDmitry/hybrid-codebase-index)**.

## Где лежит `agent-notes.md`

При запущенном MCP с **`--config`**: **`{primary knowledge root}/agent-notes.md`** (см. `[knowledge]` в TOML).

Иначе (in-proc / тесты без runtime): **`AGENT_NOTES_FILE`** → иначе **`workspace_path/.cascade-ide/agent-notes.md`**. Ревизии — рядом с каталогом файла: **`.revisions/*.md`**.

## Слой `knowledge/`

- **`knowledge_path`** в вызове тула — явный корень репозитория с каталогом **`knowledge/`**.
- Без аргумента — **primary root** из **`--config`** (`[knowledge].primary` → `[knowledge.roots]`).
- **`file_path`:** только внутри `knowledge/`, без `..` и абсолютных путей.

Пример TOML и схема: `knowledge/work/local/agent-notes.workspace.example.toml` в репозитории **agent-notes** (канон).

## Workspace scope map

Секция **`workspace-scope-map-v1`** в hot-файле и файлы из **`[workspace]`** в TOML (`scope_map`, `scope_aliases`). Дефолты для нейтрального example — embedded в **AgentNotes.Core** (`agent-notes-mcp.defaults.toml`); см. **`docs/adr/008-workspace-scope-map-resolution.md`**.

**`workspace_path`** в аргументах тула — текущий проект в Cursor (longest-prefix match по карте).

## Два разных «корня»

| | Назначение | Откуда путь |
|---|------------|-------------|
| **Hot-файл** | секции, `read_hot_context`, `route_context` | primary root из **`--config`** (или `AGENT_NOTES_FILE` / `.cascade-ide` без runtime) |
| **`knowledge/`** | read/write knowledge | **`knowledge_path`** или primary из TOML |

Один TOML с primary на клон **agent-notes** согласует hot-файл и **`knowledge/`**.

## Участие

Issues и PR — на **GitHub**: [AI-Guiders/agent-notes-mcp](https://github.com/AI-Guiders/agent-notes-mcp).

Обновить описание тулов из кода:

```bash
dotnet run --project tools/ExportMcpManifest -- --write
```

(рабочий каталог — корень репозитория).
