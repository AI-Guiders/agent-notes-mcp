# ADR 014 (MCP): Локальные настройки — TOML по `--config`

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Канонический текст (KB):** `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md` (репо **agent-notes**).

**Связано:** [008](008-workspace-scope-map-resolution.md), [013](013-localhost-status-surface-v1.md) (`[status]` в том же TOML).

---

## Контекст для разработчиков MCP

Как **DBHub** (`--config path/to/config.toml` в `mcp.json`), без автопоиска по workspace:

```json
"agent-notes": {
  "command": "D:\\agent-notes-mcp\\AgentNotesMcp.exe",
  "args": ["--config", "D:/path/agent-notes-mcp.local.toml"],
  "env": {}
}
```

Один TOML: canon, scope, resolve paths, status. Заменяет `AGENT_NOTES_CANON_PATH` / `AGENT_NOTES_FILE` в `env` хоста.

Полная схема секций и вывод legacy — **KB ADR 013**.

---

## Реализация (целевая)

| Компонент | Ответственность |
|-----------|-----------------|
| **Program.cs** | parse `--config` (и опционально `--config-file` alias) **до** `McpServer.RunAsync` |
| **AgentNotes.Core** | `LocalSettingsLoader.Load(configPath)`; Tomlyn; merge поверх embedded defaults |
| **NotesStorage** | settings singleton / scoped from loaded file |
| **Embedded** | `Resources/agent-notes-mcp.defaults.toml` |

### Приоритет пути к файлу

1. CLI **`--config`**
2. Env **`AGENT_NOTES_CONFIG`** (если нет argv — для тестов и хостов без args)
3. *(опционально, фаза 1b)* walk-up `.cursor/agent-notes.toml`
4. Нет файла → legacy env + текущее поведение

### Поведение при ошибке config

Предпочтение: **fail fast** (stderr + ненулевой exit), если `--config` задан и файл не читается — паритет с ожиданием от DBHub. Без `--config` — legacy env без падения.

### Tomlyn

В **AgentNotes.Core**; тесты с `--config` на fixture в `AgentNotesMcp.Tests`.

`mcp-resolve-paths-v1.json` — fallback, если в TOML нет `[resolve.paths]` (KB 013, фаза 2).

---

## Критерии принятия

- `mcp.json` с `--config` на example toml → тот же canon, что раньше через env.
- `env: {}` достаточно при полном TOML.
- Unit-тесты: `--config` fixture; отсутствующий файл + fail fast.

---

## Открытые вопросы (MCP)

1. Поддержка относительного пути в `--config` (от cwd процесса) или только absolute.
2. Нужен ли walk-up вообще.
