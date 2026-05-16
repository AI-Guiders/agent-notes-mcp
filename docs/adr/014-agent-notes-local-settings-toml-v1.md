# ADR 014 (MCP): Локальные настройки — TOML по `--config`

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Канонический текст (KB):** `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md` (репо **agent-notes**).

**Связано:** [008](008-workspace-scope-map-resolution.md), [013](013-localhost-status-surface-v1.md) (`[status]` в том же TOML).

**Принцип:** явное лучше неявного — один путь в `mcp.json`, без walk-up в v1.

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
| **Program.cs** | parse `--config` (alias `--config-file`) **до** `McpServer.RunAsync`; fail fast если путь задан и файл битый |
| **AgentNotes.Core** | `LocalSettingsLoader.Load(configPath)`; Tomlyn; merge поверх embedded defaults |
| **NotesStorage** | settings from loaded file |
| **Embedded** | `Resources/agent-notes-mcp.defaults.toml` |

### Приоритет пути к файлу (v1)

1. CLI **`--config`** (абсолютный путь рекомендуется)
2. Env **`AGENT_NOTES_CONFIG`** — только для тестов / хостов без `args`
3. Нет (1–2) → **legacy** `AGENT_NOTES_*` + `.cascade-ide/agent-notes.md` + stderr warning

**Walk-up:** не реализуем в v1 (KB ADR 013).

### Поведение при ошибке config

| Ситуация | Поведение |
|----------|-----------|
| `--config` / `AGENT_NOTES_CONFIG` задан, файл отсутствует или TOML невалиден | **exit ≠ 0**, понятное сообщение в stderr |
| Явный путь не задан | legacy env, как сегодня; warning «migrate to --config» |

### Tomlyn

В **AgentNotes.Core**; тесты с `--config` на fixture в `AgentNotesMcp.Tests`.

`mcp-resolve-paths-v1.json` — fallback, если в TOML нет `[resolve.paths]` (KB 013, фаза 2).

---

## Критерии принятия

- `mcp.json` с `--config` на example toml → тот же canon, что раньше через env.
- `env: {}` достаточно при полном TOML.
- Unit-тесты: valid fixture; missing file + `--config` → fail fast.

---

## Открытые вопросы (MCP)

1. Относительный `--config` от cwd — разрешить с warning или только absolute (рекомендация KB: absolute).
