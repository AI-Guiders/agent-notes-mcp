# ADR 013: Localhost status surface (AgentNotesStatus)

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Связано (только MCP/KB):** [008](008-workspace-scope-map-resolution.md); в каноне KB — `knowledge/adr/008-workspace-scope-map-hot-mcp-and-public-cut.md`, `knowledge/adr/012-multi-canon-workspace-resolution-v1.md`.

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

### 2.2. Включение (opt-in)

По умолчанию **выключено** (нулевая поверхность атаки, нет лишних портов в CI).

| Переменная | Смысл |
|------------|--------|
| `AGENT_NOTES_STATUS_HTTP` | `1` / `true` — включить listener |
| `AGENT_NOTES_STATUS_PORT` | порт (дефолт, напр. `17341`; при занятости — fail fast с понятным логом в stderr) |
| `AGENT_NOTES_STATUS_BIND` | только `127.0.0.1` в v1 (другие bind — **не** в v1) |

Документировать в README MCP и в runbook канона (`knowledge/worlds/knowledge-engineering/runbook-kb-mcp-access-v1.md` при обновлении).

### 2.3. Содержимое v1 (read-only)

| Блок | Источник | Примечание |
|------|----------|------------|
| Версия / имя сервера | `McpServerOptions.ServerInfo` | как в ListTools |
| PID, uptime | процесс | |
| Effective paths | `NotesStorage` / резолв путей | `agent-notes.md`, canon root, существует ли файл |
| Env **presence** | `AGENT_NOTES_FILE`, `AGENT_NOTES_CANON_PATH`, status flags | **не** выводить значения секретов; пути — с сокращением home (`~`) |
| Resolved scope | `ResolveScope(workspace_path)` | нужен query `?workspace_path=` или последний известный из env `AGENT_NOTES_STATUS_WORKSPACE` |
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

Cursor может запустить **несколько** процессов (разные workspace). Каждый процесс — свой порт (env в `mcp.json` на workspace) или запись порта в `{workspace}/.cascade-ide/agent-notes-status.json` при старте. Страница **всегда** описывает **текущий** процесс, не «все MCP на машине».

---

## 3. Реализация (ориентир)

### Фаза 0 (документ)

- Этот ADR, README, упоминание в `ToolCatalog` / KB runbook.

### Фаза 1 (MVP)

- `Microsoft.AspNetCore.App` minimal hosting **или** `HttpListener` — предпочтение: minimal API в отдельном partial, не блокирующий stdio loop.
- Фоновый `Task` после `McpServer` start: Kestrel на loopback.
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
| Расширить `memory_health` | уже есть, но не заменяет env/path/pid/версию в одном UI |
| IDE / PWA status | другой продукт; не зависит от agent-notes-mcp |

---

## 5. Критерии принятия (для Accepted)

- При `AGENT_NOTES_STATUS_HTTP=1` после старта MCP в браузере `http://127.0.0.1:<port>/` видны версия, effective canon path, scope для тестового workspace, фрагмент `memory_health`.
- Без переменной — порт не слушается.
- Bind не `0.0.0.0`.
- Тесты: smoke на loopback (можно `WebApplicationFactory` или интеграционный с `HttpClient` к `127.0.0.1`).

---

## 6. Открытые вопросы

1. Дефолтный порт: фиксированный vs динамический с записью в `.cascade-ide/`.
2. Нужен ли `AGENT_NOTES_STATUS_WORKSPACE` в env хоста Cursor по умолчанию.
3. Публиковать ли HTML как embedded resource или генерировать минимальный шаблон в коде.
