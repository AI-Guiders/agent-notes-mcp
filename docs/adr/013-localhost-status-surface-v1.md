# ADR 013: Localhost status surface (AgentNotesStatus)

**Статус:** Accepted · Implemented  
**Дата:** 2026-05-16  
**Обновлено:** 2026-05-16 — фазы 1–2: HTTP loopback, HTML/JSON, ring buffer, `/hot-preview`, `--status-only`.

**Канонический текст (KB):** `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md` (секция status).

## Связанные ADR

| ADR | Роль |
|-----|------|
| [008](008-workspace-scope-map-resolution.md) | резолв `active_scope` для `/hot-preview` |
| [014](014-agent-notes-local-settings-toml-v1.md) | секция **`[status]`** в TOML (`--config`, MCP 2.0) |

### Вне scope

Remote operator / PWA / Operator Gateway (Cascade IDE) — другие ADR и продукты.

## Резюме

- Опциональный **HTTP на loopback** в процессе `agent-notes-mcp` для отладки без лишних MCP round-trip.
- Endpoints: `/`, `/status.json`, `/health`, `/hot-preview` (см. §2.1).
- По умолчанию **выключено** — `[status].enabled` в TOML из **`--config`** ([014](014-agent-notes-local-settings-toml-v1.md)).
- `bind` в v1 — только `127.0.0.1`.

---

## 1. Контекст

**agent-notes-mcp** — stdio MCP. При настройке канона неочевидно, что реально резолвит **этот** процесс (canon, scope, `memory_health`), особенно при нескольких workspace и legacy env.

Ответы только через MCP-tools из чата — лишние round-trip при отладке.

---

## 2. Решение

### 2.1. AgentNotesStatus

Опциональный **HTTP на loopback** (`127.0.0.1`) в том же процессе `agent-notes-mcp`:

| Endpoint | Назначение |
|----------|------------|
| `GET /` | HTML-сводка |
| `GET /status.json` | JSON для скриптов |
| `GET /health` | `200 OK` |
| `GET /hot-preview` | JSON: размеры hot-секций (`?workspace_path=` или `[status.preview].workspace`) |

По умолчанию **выключено** — см. **`[status].enabled`** в TOML из **`--config`** ([014](014-agent-notes-local-settings-toml-v1.md)).

### 2.2. Конфигурация status

**Не** отдельный файл и **не** env. Секция в том же TOML, путь к которому задан в `mcp.json` как у DBHub:

```toml
[status]
enabled = true
port = 17341
bind = "127.0.0.1"

[status.preview]
# workspace = "..."   # для превью scope на странице (KB ADR 013)
```

`bind` в v1: только `127.0.0.1`; иное — warning и принудительно loopback.

**Runtime** (не конфиг): `{workspace}/.cascade-ide/agent-notes-status.runtime.json` — `pid`, `port`, `url`, `config_source`.

### 2.3. Содержимое страницы (read-only)

| Блок | Источник |
|------|----------|
| Версия MCP, PID, uptime | процесс |
| `config_path` | абсолютный путь из **`--config`** ([014](014-agent-notes-local-settings-toml-v1.md)) |
| Primary knowledge root / notes path | TOML `[knowledge]` + legacy `AGENT_NOTES_*` («present / overridden») |
| Scope, `memory_health` | существующий код; `?workspace_path=` или `[status.preview].workspace` |
| Карта workspace | метаданные (N правил), без полного дампа путей в HTML |
| Tools | `ToolCatalog` |
| Read-only knowledge roots | `[[knowledge.read_only]]` настроен / нет ([012] в KB) |

**Не входит:** запись в KB/hot через HTTP; прокси всех tools; LAN/WAN.

### 2.4. Безопасность

Loopback only; без `personal/`, hot ниже `public-cut`, секретов; `?verbose=1` для полных путей в JSON.

### 2.5. Несколько процессов MCP

У каждого workspace — свой TOML (`status.port`) и свой `agent-notes-status.runtime.json`. Страница описывает **текущий** процесс.

---

## 3. Реализация

| Фаза | Содержание |
|------|------------|
| 0 | ADR + README |
| 1 | Зависит от [014](014-agent-notes-local-settings-toml-v1.md) фаза 1 (Tomlyn); Kestrel minimal API; `[status]` |
| 2 | Ring buffer последних tool calls (в `status.json` + HTML); `GET /hot-preview`; CLI `--status-only` (только HTTP, без stdio MCP) |

---

## 4. Альтернативы

| Вариант | Почему нет |
|---------|------------|
| Только tool `debug_status` | нет браузера в один клик |
| Env `AGENT_NOTES_STATUS_*` | дублирует TOML [014] |
| IDE / PWA status | другой продукт |

---

## 5. Критерии принятия

- `[status].enabled = true` + `--config` → браузер: версия MCP, primary KB root, scope, `memory_health`, `config_path`.
- `enabled = false` → порт не слушается.
- Smoke-тесты loopback.

---

## 6. Открытые вопросы

1. HTML: embedded resource vs генерация в коде.
2. Порядок с [014]: status HTTP сразу после Tomlyn loader или отдельным PR.
