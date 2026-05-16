# ADR 013: Localhost status surface (AgentNotesStatus)

**Статус:** Proposed  
**Дата:** 2026-05-16  

**Связано:** [008](008-workspace-scope-map-resolution.md); [014](014-agent-notes-local-settings-toml-v1.md) — секция **`[status]`** в `.cursor/agent-notes.toml`; в каноне KB — `knowledge/adr/013-agent-notes-mcp-local-settings-toml-v1.md`, [012](012-multi-canon-workspace-resolution-v1.md) (multi-canon).

**Вне scope:** remote operator / PWA / Operator Gateway (Cascade IDE) — другие ADR и продукты.

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

По умолчанию **выключено** — см. **`[status].enabled`** в TOML из **`--config`** ([014](014-agent-notes-local-settings-toml-v1.md)).

### 2.2. Конфигурация status

**Не** отдельный файл и **не** env. Секция в том же TOML, путь к которому задан в `mcp.json` как у DBHub:

```toml
[status]
enabled = true
port = 17341
bind = "127.0.0.1"

[status.preview]
# workspace_path = "..."   # для превью scope на странице
```

`bind` в v1: только `127.0.0.1`; иное — warning и принудительно loopback.

**Runtime** (не конфиг): `{workspace}/.cascade-ide/agent-notes-status.runtime.json` — `pid`, `port`, `url`, `config_source`.

### 2.3. Содержимое страницы (read-only)

| Блок | Источник |
|------|----------|
| Версия MCP, PID, uptime | процесс |
| `config_path` | абсолютный путь из **`--config`** ([014](014-agent-notes-local-settings-toml-v1.md)) |
| Effective canon / notes path | слитые settings + legacy env («present / overridden») |
| Scope, `memory_health` | существующий код; `?workspace_path=` или `[status.preview]` |
| Карта workspace | метаданные (N правил), без полного дампа путей в HTML |
| Tools | `ToolCatalog` |
| Secondary canon | флаг «настроен», без содержимого ([012] в KB) |

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
| 2 | Ring buffer последних tool calls; `GET /hot-preview` (размеры секций); `--status-only` CLI |

---

## 4. Альтернативы

| Вариант | Почему нет |
|---------|------------|
| Только tool `debug_status` | нет браузера в один клик |
| Env `AGENT_NOTES_STATUS_*` | дублирует TOML [014] |
| IDE / PWA status | другой продукт |

---

## 5. Критерии принятия

- `[status].enabled = true` + `--config` → браузер по URL из runtime json: версия, canon, scope, `memory_health`, `config_path`.
- `enabled = false` → порт не слушается.
- Smoke-тесты loopback.

---

## 6. Открытые вопросы

1. HTML: embedded resource vs генерация в коде.
2. Порядок с [014]: status HTTP сразу после Tomlyn loader или отдельным PR.
