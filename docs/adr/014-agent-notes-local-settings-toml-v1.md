# ADR 014 (MCP): Локальные настройки — TOML

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Канонический текст (KB):** в репозитории **agent-notes** — `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md`.

**Связано:** [008](008-workspace-scope-map-resolution.md), [013](013-localhost-status-surface-v1.md) (секция `[status]` в том же TOML).

---

## Контекст для разработчиков MCP

Реализация **единого файла настроек** для процесса `agent-notes-mcp`: резолв канона, scope, путей к картам, опционально localhost status. Заменяет разрозненные **env** и постепенно — `knowledge/META/mcp-resolve-paths-v1.json`.

Полная схема, приоритеты слияния и план вывода `AGENT_NOTES_*` — только в **KB ADR 013** (не дублировать здесь).

---

## Реализация (целевая)

| Компонент | Ответственность |
|-----------|-----------------|
| **AgentNotes.Core** | `AgentNotesLocalSettings` model; `LocalSettingsLoader` (Tomlyn); merge embedded + workspace TOML + env fallback |
| **NotesStorage** | `ResolveCanonPath`, `GetNotesPath`, `ResolveScope` читают слитые settings |
| **Program.cs** | при старте: load settings для `workspace_path` из env хоста (если хост передаёт) или lazy на первый tool call |
| **Embedded** | `Resources/agent-notes-mcp.defaults.toml` |

### Walk-up

От абсолютного `workspace_path` вверх по каталогам: первый `.cursor/agent-notes.toml` (опционально также `.cascade-ide/agent-notes.toml` — порядок зафиксировать в коде и тестах).

### Tomlyn

Пакет **Tomlyn** в **AgentNotes.Core** (не только в exe MCP), чтобы тесты `AgentNotesMcp.Tests` гоняли merge без subprocess.

### Обратная совместимость

Пока TOML не найден или секция `[canon]` пуста — поведение **как сейчас** (env → fallback `.cascade-ide`).

`mcp-resolve-paths-v1.json` — читать, если в TOML нет `[resolve.paths]` (см. KB ADR 013, фаза 2).

---

## Критерии принятия

- С example toml из канона процесс резолвит тот же canon path, что раньше через `AGENT_NOTES_CANON_PATH`.
- При наличии TOML env **не** перебивает явные ключи в файле.
- Unit-тесты: merge layers, invalid toml fallback, walk-up на вложенном каталоге.

---

## Открытые вопросы (MCP)

1. Передаёт ли Cursor `workspace_path` в env при старте MCP — если нет, lazy load на первом `workspace_path` в tool args.
2. Версионирование `version = 1` — отказ при неизвестной major или warning.
