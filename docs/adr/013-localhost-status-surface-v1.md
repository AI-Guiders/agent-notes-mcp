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

### 2.2. Конфигурация (файл, не зоопарк env)

По умолчанию **выключено** (нулевая поверхность атаки, нет лишних портов в CI).

**Принцип:** как у [008](008-workspace-scope-map-resolution.md) для путей карты — **embedded defaults** в `AgentNotes.Core` + **слияние** дисковых JSON; **не** отдельная переменная на каждый флаг.

#### Файлы (приоритет снизу вверх)

| Уровень | Путь | Назначение |
|---------|------|------------|
| 0 | embedded `mcp-local-settings-defaults.json` | `status.enabled: false`, `port: 17341`, `bind: "127.0.0.1"`, `default_workspace_path: null` |
| 1 | `{canon}/knowledge/META/mcp-local-settings-v1.json` | настройки **установки канона** на машине (включить status, порт по умолчанию) |
| 2 | `{workspace}/.cascade-ide/mcp-local-settings.json` | **пер-workspace** (свой порт при нескольких MCP; опционально `workspace_path` для превью scope) |

Слияние: поверхностные ключи перекрывают глубокие; невалидный JSON → лог в stderr + откат к предыдущему уровню (как `mcp-resolve-paths-v1.json`).

**Схема v1 (черновик):**

```json
{
  "version": 1,
  "status": {
    "enabled": true,
    "port": 17341,
    "bind": "127.0.0.1"
  }
}
```

`bind` в v1 допускает только `127.0.0.1`; иное значение — игнор + warning (не слушать на `0.0.0.0`).

#### Env — только escape hatch

| Переменная | Когда |
|------------|--------|
| `AGENT_NOTES_LOCAL_SETTINGS_FILE` | **опционально:** абсолютный путь к одному JSON вместо цепочки 1–2 (тесты, нестандартная раскладка) |

`AGENT_NOTES_FILE` / `AGENT_NOTES_CANON_PATH` остаются **корнем канона**, не дублируем их для status.

#### Runtime-артефакт (не конфиг)

При старте listener процесс может записать **`{workspace}/.cascade-ide/agent-notes-status.runtime.json`** (`pid`, `port`, `url`) — чтобы человек и скрипты нашли **этот** экземпляр без угадывания порта. Файл перезаписывается при рестарте MCP; в git не коммитить.

Документировать: README MCP, пример в `knowledge/META/` (шаблон в каноне), runbook `knowledge/worlds/knowledge-engineering/runbook-kb-mcp-access-v1.md`.

### 2.3. Содержимое v1 (read-only)

| Блок | Источник | Примечание |
|------|----------|------------|
| Версия / имя сервера | `McpServerOptions.ServerInfo` | как в ListTools |
| PID, uptime | процесс | |
| Effective paths | `NotesStorage` / резолв путей | `agent-notes.md`, canon root, существует ли файл |
| Env **presence** | `AGENT_NOTES_FILE`, `AGENT_NOTES_CANON_PATH`, status flags | **не** выводить значения секретов; пути — с сокращением home (`~`) |
| Resolved scope | `ResolveScope(workspace_path)` | query `?workspace_path=` или `default_workspace_path` из слитого `mcp-local-settings` |
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

Cursor может запустить **несколько** процессов (разные workspace). У каждого — свой `.cascade-ide/mcp-local-settings.json` (порт) и свой `agent-notes-status.runtime.json`. Страница **всегда** описывает **текущий** процесс, не «все MCP на машине».

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
| Расширить `memory_health` | уже есть, но не заменяет path/pid/версию в одном UI |
| Только env-флаги (`AGENT_NOTES_STATUS_*`) | плохо масштабируется; дублирует то, что уже решает JSON в META / `.cascade-ide/` |
| IDE / PWA status | другой продукт; не зависит от agent-notes-mcp |

---

## 5. Критерии принятия (для Accepted)

- При `status.enabled: true` в слитом local-settings после старта MCP в браузере по URL из `agent-notes-status.runtime.json` видны версия, effective canon path, scope для `default_workspace_path`, фрагмент `memory_health`.
- При `enabled: false` (дефолт) — порт не слушается.
- Bind не `0.0.0.0`.
- Тесты: merge settings (3 уровня), smoke HTTP на loopback.

---

## 6. Открытые вопросы

1. Один файл `mcp-local-settings-v1.json` vs расширить существующий `mcp-resolve-paths-v1.json` секцией `"status"` (меньше файлов в META, но смешение «пути KB» и «локальный HTTP»).
2. Коммитить ли пример `mcp-local-settings-v1.json` в kb-public (только схема с `enabled: false`) или держать шаблон только в полном каноне / README.
3. Публиковать ли HTML как embedded resource или генерировать минимальный шаблон в коде.
