# ADR 014 (MCP): Локальные настройки — TOML по `--config` (релиз **2.0**)

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Канонический текст (KB):** `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md` (репо **agent-notes**), в т.ч. **R7 — major 2.0**.

**Связано:** [008](008-workspace-scope-map-resolution.md), [013](013-localhost-status-surface-v1.md) (`[status]` в том же TOML).

**Релиз:** **semver major `2.0.0`** (единственный «2» у продукта). Breaking: обязательный **`--config`**, **новый** TOML (в 1.x не было), `canon_path` → `knowledge_path`, без META JSON / `AGENT_NOTES_CANON_PATH` в supported install. Поле `version = 1` в TOML — **первая** схема файла, не «версия 2».

**Принцип:** явное лучше неявного — один путь в `mcp.json`, без walk-up.

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

Локальный TOML (`version = 1` в файле): `[knowledge]`, `[workspace]`, `[status]`. Появляется только в **MCP 2.0**. До релиза **1.x** — env + META JSON, без TOML.

Полная схема и список breaking — **KB ADR 013** (R3, R7).

---

## Реализация (целевая)

| Компонент | Ответственность |
|-----------|-----------------|
| **Program.cs** | parse `--config` (alias `--config-file`) **до** `McpServer.RunAsync`; fail fast если путь задан и файл битый |
| **AgentNotes.Core** | `LocalSettingsLoader.Load(configPath)`; Tomlyn; merge поверх embedded defaults |
| **NotesStorage** | settings from loaded file |
| **Embedded** | `Resources/agent-notes-mcp.defaults.toml` |

### Приоритет пути к конфигу (**2.0**)

1. CLI **`--config`** (абсолютный путь рекомендуется)
2. Env **`AGENT_NOTES_CONFIG`** — тесты / хосты без `args`
3. Иначе — **ошибка запуска** с текстом «нужен --config» (legacy `AGENT_NOTES_*` **не** в supported path 2.0)

**Walk-up:** не реализуем (KB ADR 013).

### Приоритет (**1.x**, до 2.0)

Как сейчас: env `AGENT_NOTES_*`, META JSON — без изменений до тега `2.0.0`.

### Поведение при ошибке config

| Ситуация | Поведение |
|----------|-----------|
| `--config` / `AGENT_NOTES_CONFIG` задан, файл отсутствует или TOML невалиден | **exit ≠ 0**, понятное сообщение в stderr |
| Явный путь не задан (**2.0**) | exit ≠ 0, инструкция по example toml |

### Tomlyn

В **AgentNotes.Core**; тесты с `--config` на fixture в `AgentNotesMcp.Tests`.

**2.0:** `[workspace]` вместо META JSON; `[knowledge]` для корней; ToolCatalog — **`knowledge_path`**; `LocalSettings` / резолвер без имён `Canon*` в новом API (допустимы private alias на переходе внутри PR).

### Миграция 1.x → 2.0 (чеклист)

1. Собрать/скачать **agent-notes-mcp 2.0.x**.
2. Создать TOML из `knowledge/work/local/agent-notes.workspace.example.toml` (схема файла `version = 1`).
3. В `mcp.json`: `"args": ["--config", "<abs path>"], "env": {}`.
4. В правилах и playbook: **`canon_path` → `knowledge_path`**.
5. Удалить `knowledge/META/mcp-resolve-paths-v1.json` из primary KB (после проверки путей в `[workspace]`).

---

## Техдолг и приборка в коде (релиз 2.0)

Сейчас логика в **agent-notes-core** + тонкий **AgentNotesMcp**; имена застряли на эпохе env + META JSON. **2.0** — единственный разумный момент для переименований и выкидывания legacy (не тащить «canon» в новый TOML-loader).

### Публичный контракт MCP (breaking)

| Было | Стало |
|------|--------|
| аргумент тула `canon_path` | **`knowledge_path`** (`ToolCatalog`, `ToolHandlers`, manifest export) |
| описания «канон» / `AGENT_NOTES_CANON_PATH` | primary **knowledge root**, путь из **`--config`** |
| `Program.cs` `Version = "0.5.1"` | **`2.0.0`** (+ informational / assembly version) |

### AgentNotes.Core

| Область | Сейчас | 2.0 |
|---------|--------|-----|
| Резолв корня | `ResolveCanonPath`, `TryInferCanonRootFromAgentNotesFilePath`, `EnvCanonPath` | `ResolveKnowledgeRoot` (или `ResolvePrimaryKnowledgeRoot`), настройки из **`LocalSettings`** после Tomlyn |
| Пути scope | `ReadMcpResolvePathsOrDefaults`, `McpResolvePathsDefaults`, `McpResolvePathsConfigModel`, `mcp-resolve-paths-defaults.json` | `WorkspacePaths` / `[workspace]` из TOML + embedded **`agent-notes-mcp.defaults.toml`** |
| META на диске | `knowledge/META/mcp-resolve-paths-v1.json` | **не читать**; тесты на JSON → fixture TOML |
| Параметры API | `canonPath` во всех `*KnowledgeFile*` | `knowledgePath` (или optional `knowledgeRoot`) |
| Внутренние имена | `canonRoot`, `ResolveCanonRootFromNotesPath` | `knowledgeRoot` |

Дубликаты `.cs` в корне **agent-notes-mcp** (исключены из compile, зеркало core) — **удалить**, оставить один источник в **agent-notes-core**.

### AgentNotesMcp

| Область | Действие |
|---------|----------|
| `Program.cs` | parse **`--config`** до `McpServer.RunAsync`; fail fast |
| `ToolHandlers` | `canon_path` → `knowledge_path` |
| Тесты | `TempCanon` → `TempKnowledgeRoot`; фикстуры TOML вместо META JSON |

### Связанные репозитории (не блокер MCP, но тот же major по смыслу)

- **Cascade IDE** (`McpAgentNotesService`, `IdeMcpCommandExecutor`): параметр `canon_path` в JSON команд — обновить при поднятии ссылки на Core 2.0.
- **Cursor rules / KB playbook**: `canon_path` в текстах.
- **agent-notes** KB: удалить `knowledge/META/mcp-resolve-paths-v1.json` после кода.

### Что не переименовывать в 2.0 (осознанно)

- Имена **файлов** в репо (`agent-notes.md`, каталог `knowledge/`) — без изменений.
- ADR **012** title «multi-canon» — исторический документ; в новом коде не продлевать термин.

---

## Критерии принятия

- `mcp.json` с `--config` → тот же primary KB root, что раньше через `AGENT_NOTES_CANON_PATH`.
- Информационная версия сборки начинается с **2.0**.
- `env: {}` достаточно при полном TOML.
- Unit-тесты: valid fixture; missing file + `--config` → fail fast.

---

## Открытые вопросы (MCP)

1. Относительный `--config` от cwd — разрешить с warning или только absolute (рекомендация KB: absolute).
