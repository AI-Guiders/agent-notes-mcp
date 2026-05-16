# ADR 014 (MCP): Локальные настройки — TOML по `--config` (релиз **2.0**)

**Статус:** Proposed  
**Дата:** 2026-05-16  
**Обновлено:** 2026-05-16 — breaking MCP **2.0**: обязательный `--config`, Tomlyn, без legacy env/META JSON.

**Канонический текст (KB):** `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md` (репо **agent-notes**), в т.ч. **R7 — major 2.0**.

## Связанные ADR

| ADR | Роль |
|-----|------|
| [008](008-workspace-scope-map-resolution.md) | резолв scope workspace |
| [013](013-localhost-status-surface-v1.md) | секция `[status]` в том же TOML |

## Резюме

- **MCP 2.0:** один локальный TOML по **`--config`** в `mcp.json` (как DBHub), без walk-up и без `AGENT_NOTES_CANON_PATH`.
- Секции: `[knowledge]`, `[workspace]`, `[status]`; `version = 1` в файле — схема TOML, не semver продукта.
- Breaking: `canon_path` → `knowledge_path`; embedded defaults + merge Tomlyn.
- До релиза **1.x** — env + META JSON; после — только TOML.

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
| META на диске | `knowledge/META/mcp-resolve-paths-v1.json` | **не читать** (реализовано в 2.0); тесты — fixture TOML |
| Параметры API | `canonPath` во всех `*KnowledgeFile*` | `knowledgePath` (или optional `knowledgeRoot`) |
| Внутренние имена | `canonRoot`, `ResolveCanonRootFromNotesPath` | `knowledgeRoot` |

Дубликаты `.cs` в корне **agent-notes-mcp** — **удалены** при выносе Core (зеркало больше не в репо).

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

### `[[knowledge.read_only]]` — схема в 2.0, маршрутизация позже

Смысл и роли корней — **KB** [012-multi-canon-workspace-resolution-v1.md](https://github.com/AIGuiders/agent-notes/blob/main/knowledge/adr/012-multi-canon-workspace-resolution-v1.md) (исторически «secondary canon»). В TOML это **не** то же самое, что `[knowledge.roots]`:

| Секция | Роль |
|--------|------|
| **`[knowledge].primary`** + **`[knowledge.roots]`** | Один **primary** knowledge root: hot-файл, **запись** в `knowledge/`, `[workspace]` (карта scope только из primary). |
| **`[[knowledge.read_only]]`** | Дополнительные корни **только чтение** (org-kb, kb-public-клон): агент может **читать** карточки, **не** писать туда через MCP. Поле **`id`** — стабильная метка (`org`, `public`) для будущего выбора корня в тулах и на странице [013](013-localhost-status-surface-v1.md). |

Пример (опционально в `--config`; в шаблоне `config/agent-notes-mcp.toml` — закомментирован):

```toml
[[knowledge.read_only]]
id = "org"
path = "D:/clones/AI-Guiders/kb"
```

**Релиз `2.0.0` (фактически в коде):**

| Что | Статус |
|-----|--------|
| Парсинг Tomlyn → `LocalSettings.ReadOnlyKnowledgeRoots` | **да** |
| `read_knowledge_file` / `write_*` / `list_knowledge_files` по `id` read-only | **нет** |
| Запрет записи в read-only при явном `knowledge_path` | **нет** (пока один корень: primary или аргумент тула) |
| `[routing]` / overlay org в `route_context` | **фаза 2** (KB ADR 013) |

До multi-KB секцию **можно не заполнять** — поведение как с одним primary. Обязательный минимум конфига: `[knowledge]` + `[workspace]` (или embedded defaults для `[workspace]`).

**Следующий шаг (не 2.0):** резолв `knowledge_path` / параметр `knowledge_root_id` → primary или read-only; `write_*` только в primary; status показывает список read-only roots.

---

## Критерии принятия

- `mcp.json` с `--config` → тот же primary KB root, что раньше через `AGENT_NOTES_CANON_PATH`.
- Информационная версия сборки начинается с **2.0**.
- `env: {}` достаточно при полном TOML.
- Unit-тесты: valid fixture; missing file + `--config` → fail fast.

---

## Открытые вопросы (MCP)

1. Относительный `--config` от cwd — разрешить с warning или только absolute (рекомендация KB: absolute).
