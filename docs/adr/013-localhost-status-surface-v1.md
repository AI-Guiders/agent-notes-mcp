# ADR 013: Localhost status surface (AgentNotesStatus)

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Связано (только MCP/KB):** [008](008-workspace-scope-map-resolution.md); в каноне KB — `knowledge/adr/008-workspace-scope-map-hot-mcp-and-public-cut.md`, **`knowledge/adr/012-multi-canon-workspace-resolution-v1.md`** (единый workspace TOML).

**Вне scope этого ADR:** любые **remote operator / PWA / Operator Gateway** в Cascade IDE или других продуктах — отдельные решения, другой хост и другая аудитория. AgentNotesStatus **не** является пультом оператора и **не** дублирует веб-контуры IDE.

---

## 1. Контекст

**agent-notes-mcp** — процесс **stdio MCP**: Cursor (или другой хост) поднимает сервер, общение идёт по JSON-RPC в stdin/stdout. Для человека, настраивающего канон, неочевидно:

- какой **`AGENT_NOTES_FILE`** / **`AGENT_NOTES_CANON_PATH`** реально видит **этот** процесс;
- какой **`active_scope`** получится для данного `workspace_path`;
- есть ли карта workspace → scope, hot-секции, предупреждения `memory_health`;
- какая версия сборки и сколько экземпляров слушают (несколько workspace → несколько процессов MCP).

Сегодня ответы доступны только через вызов MCP-инструментов (`memory_health`, `read_hot_context`, …) из чата с агентом — неудобно при отладке конфигурации **до** или **вне** сессии агента.

**Проблема:** без локальной «панели правды» растёт число ложных багов («MCP не тот канон», «scope не тот»), а расследование требует лишних round-trip через LLM.

---

## 2. Решение (намерение)

### 2.1. AgentNotesStatus

Опциональный **HTTP-сервер только на loopback** (`127.0.0.1`), поднимаемый **тем же процессом** `agent-notes-mcp` (или явной подкомандой — см. §4), отдающий:

- **HTML** — краткая человекочитаемая сводка;
- **`/status.json`** (или аналог) — машиночитаемый снимок для скриптов.

Рабочее имя контура: **AgentNotesStatus** (не путать с продуктовыми status API других репозиториев).

### 2.2. Конфигурация: один TOML, не зоопарк env

По умолчанию **выключено** (нулевая поверхность атаки, нет лишних портов в CI).

**Формат: TOML**, не JSON — человекочитаемые комментарии, меньше кавычек, привычно рядом с `.cursor/mcp.json` и toolchain .NET.

**Один файл на workspace** (канон KB, ADR 012 — `knowledge/adr/012-multi-canon-workspace-resolution-v1.md`): **`.cursor/agent-notes.toml`** в корне кодового workspace (walk вверх, как `.git`). Секция **`[status]`** — часть того же файла, что **`[canon]`**, **`[scope]`**, **`[behavior]`**; отдельные `mcp-local-settings*.json` **не** вводим.

#### Цепочка резолва (целевая, снизу вверх)

| Приоритет | Источник | Содержание |
|-----------|----------|------------|
| 0 | embedded `agent-notes-mcp.defaults.toml` в Core | дефолты: `status.enabled = false`, `port = 17341`, `bind = "127.0.0.1"` |
| 1 | **`.cursor/agent-notes.toml`** (найденный walk-up от `workspace_path`) | canon paths, scope, **status**, secondary canon |
| 2 | **Legacy env** (временно) | `AGENT_NOTES_CANON_PATH`, `AGENT_NOTES_FILE` — см. §2.6 |
| 3 | Fallback hot | `workspace_path/.cascade-ide/agent-notes.md` |

Слияние: TOML workspace перекрывает embedded; при ошибке парсинга — stderr + откат на уровень ниже.

**Пример (фрагмент; полный шаблон — `knowledge/work/local/agent-notes.workspace.example.toml` в каноне):**

```toml
version = 1

[canon]
primary = "personal"

[canon.paths]
personal = "D:/Experiments/agent-notes"

[status]
enabled = true
port = 17341
bind = "127.0.0.1"   # v1: только loopback; иное — warning и принудительно 127.0.0.1

[status.preview]
# опционально: default workspace для scope/memory_health на странице (если хост не передал workspace_path)
# workspace_path = "D:/Experiments/PersonalCursorFolder"
```

Парсер: **Tomlyn** (или эквивалент) в `AgentNotes.Core`; одна модель `AgentNotesLocalSettings` для MCP и тестов.

#### Runtime-артефакт (не конфиг)

При старте listener — **`{workspace}/.cascade-ide/agent-notes-status.runtime.json`** (`pid`, `port`, `url`, `config_source`: путь к TOML). JSON здесь уместен: пишет только процесс, читают скрипты/браузер. В git не коммитить.

#### Env в v1 реализации status

На фазе **только AgentNotesStatus** новых env **не** добавляем. Существующие `AGENT_NOTES_*` не трогаем до фазы слияния в Core (ADR 012, фаза 2).

### 2.6. Вывод `AGENT_NOTES_CANON_PATH` / `AGENT_NOTES_FILE` (намерение)

**Цель:** настроить MCP **одним TOML** в workspace (и embedded defaults), без дублирования в `mcp.json` env и без «угадай, какой канон видит процесс».

| Сейчас | Целевое |
|--------|---------|
| `AGENT_NOTES_CANON_PATH`, `AGENT_NOTES_FILE` в env Cursor | **`[canon]` / `[canon.paths]`** в `.cursor/agent-notes.toml` |
| `knowledge/META/mcp-resolve-paths-v1.json` | фаза 3: секция **`[resolve.paths]`** в том же TOML (или оставить JSON до миграции — [008](008-workspace-scope-map-resolution.md)) |
| Разрозненные флаги | одна схема, версия `version = 1` |

**Legacy:** env остаётся **ниже по приоритету**, чем TOML, пока не объявим deprecation в README и runbook; затем major MCP — env только для CI/тестов (`AGENT_NOTES_FILE` в `EnvVarScope`).

**Статус-страница** показывает: `config_source` (путь TOML), effective `canon.primary`, legacy env «задано / не задано» (без секретов), чтобы видеть, **что победило** в резолве.

Документировать: README MCP, example toml в каноне, `runbook-kb-mcp-access-v1.md`, кросс-ссылка в ADR 012.

### 2.3. Содержимое v1 (read-only)

| Блок | Источник | Примечание |
|------|----------|------------|
| Версия / имя сервера | `McpServerOptions.ServerInfo` | как в ListTools |
| PID, uptime | процесс | |
| Effective paths | `NotesStorage` / резолв путей | `agent-notes.md`, canon root, существует ли файл |
| Config source | слитый TOML + legacy env | путь к `.cursor/agent-notes.toml`; env — только «present / overridden by toml» |
| Effective canon | `ResolveCanonPath` (целевой) | primary path, notes file; пути с `~` |
| Resolved scope | `ResolveScope(workspace_path)` | query `?workspace_path=` или `[status.preview].workspace_path` |
| `memory_health` | существующий метод | тот же JSON, что тула |
| Карта workspace | метаданные | «файл найден / N правил», без дампа полных путей в HTML по умолчанию |
| Список tools | `ToolCatalog` | имена + краткие описания |
| Secondary canon | ADR 012 | «только read через canon_path в тулах» — флаг «задан ли второй корень» без содержимого |

**Не входит в v1:**

- запись в канон, hot, knowledge через HTTP;
- прокси всех MCP tools по HTTP;
- доступ с LAN/WAN, TLS, аутентификация пользователей (loopback = доверие к локальной сессии ОС);
- стриминг событий вызовов tools (можно фаза 2).

### 2.4. Безопасность

- Bind **строго** `127.0.0.1`.
- Не отдавать содержимое `personal/`, полный hot ниже `public-cut`, токены, пароли, содержимое `.env`.
- Пути к KB на диске — опционально «redacted» в HTML; полные пути — только в JSON по явному `?verbose=1` для локального дебага.
- Если в будущем появится remote access — **новый ADR**, не расширение этого.

### 2.5. Несколько экземпляров MCP

Cursor может запустить **несколько** процессов (разные workspace). У каждого — свой walk-up **`.cursor/agent-notes.toml`** (свой `status.port`) и свой `agent-notes-status.runtime.json`. Страница **всегда** описывает **текущий** процесс, не «все MCP на машине».

---

## 3. Реализация (ориентир)

### Фаза 0 (документ)

- Этот ADR, README, упоминание в `ToolCatalog` / KB runbook.

### Фаза 1 (MVP)

- `Tomlyn` + модель настроек; чтение `[status]` из `.cursor/agent-notes.toml` (walk-up); embedded defaults TOML.
- `Microsoft.AspNetCore.App` minimal hosting — Kestrel на loopback, не блокируя stdio.
- Endpoints: `GET /`, `GET /status.json`, `GET /health` → `200 OK`.

### Фаза 2 (опционально)

- Последние N tool calls (имя, длительность, error flag) — ring buffer в памяти;
- `GET /hot-preview` — только размеры секций L0/L1, без текста;
- CLI: `agent-notes-mcp --status-only` для диагностики без stdio-хоста.

---

## 4. Альтернативы

| Вариант | Почему не основной |
|---------|-------------------|
| Только MCP tool `debug_status` | не открывается в браузере одним кликом; нужен хост с агентом |
| Отдельный exe | дублирование резолва; рассинхрон с живым MCP |
| Расширить `memory_health` | уже есть, но не заменяет path/pid/версию в одном UI |
| Только env-флаги | плохо масштабируется; дублирует TOML |
| JSON для локальных настроек | без комментариев; дублирует ADR 012 TOML; `mcp-resolve-paths-v1.json` — legacy до `[resolve.paths]` |
| IDE / PWA status | другой продукт; не зависит от agent-notes-mcp |

---

## 5. Критерии принятия (для Accepted)

- При `[status].enabled = true` в слитом TOML после старта MCP в браузере по URL из `agent-notes-status.runtime.json` видны версия, effective canon, scope, фрагмент `memory_health`, `config_source`.
- При `enabled = false` (дефолт) — порт не слушается.
- Bind не `0.0.0.0`.
- Тесты: parse TOML, merge с defaults, smoke HTTP на loopback.

---

## 6. Открытые вопросы

1. Имя файла: только `.cursor/agent-notes.toml` или alias `.cascade-ide/agent-notes.toml` (как в [012]).
2. User-level `~/.config/agent-notes/settings.toml` для машины без привязки к одному workspace — нужен ли, или достаточно embedded + per-workspace TOML.
3. Публиковать ли HTML как embedded resource или генерировать минимальный шаблон в коде.
4. Порядок внедрения: сначала `[status]` + status HTTP, или сразу фаза 2 [012] (`[canon]` из TOML) в одном PR Core.
