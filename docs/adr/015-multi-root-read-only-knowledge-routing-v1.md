# ADR 015: Multi-root read-only knowledge routing (`knowledge_root_id`)

**Статус:** Accepted  
**Дата:** 2026-05-18 (доп. 2026-05-18: реестр `work/local` + hot-указатель)  
**Связано:** [014](014-agent-notes-local-settings-toml-v1.md) (TOML `[[knowledge.read_only]]`), KB [012-multi-canon-workspace-resolution-v1.md](https://github.com/KarataevDmitry/personal-knowledge-base/blob/main/knowledge/adr/012-multi-canon-workspace-resolution-v1.md), [011](https://github.com/KarataevDmitry/personal-knowledge-base/blob/main/knowledge/adr/011-aiguiders-org-collaborative-kb-repo-v1.md) (team KB → `AI-Guiders/kb`)

---

## Контекст

В **2.0** TOML уже парсит `[[knowledge.read_only]]`, но тулы знали только `knowledge_path` → primary. Нужен end-to-end smoke: **group**-kb как второй корень, чтение по `id`, запрет записи.

**Проблема маршрутизации:** `route_context` индексирует только **hot primary** (`agent-notes.md`). Файл **X** только в **group** сам не находится — нужен реестр + постоянный указатель в hot (аналог `workspace-scope-map`).

## chmod ugo ↔ три контура KB

| chmod | Роль | `knowledge_root_id` | Репозиторий |
|-------|------|----------------------|-------------|
| **u** (user) | личный primary, запись, hot | *(default / primary)* | `agent-notes` |
| **g** (group) | командная коллаборативная KB | **`group`** | **`AI-Guiders/kb`** (private) |
| **o** (other) | публичный срез | **`public`** (read_only) | `kb-public` |

## Решение (MCP)

| Механизм | Поведение |
|----------|-----------|
| `knowledge_root_id` | Резолв в `[knowledge.roots]` или `[[knowledge.read_only]]` (case-insensitive `id`). |
| `knowledge_path` | Абсолютный путь к корню репо с `knowledge/`. Взаимоисключим с `knowledge_root_id`. |
| По умолчанию | Primary (**user**) из `[knowledge].primary`. |
| **Запись** | Только primary; **group** / **public** read-only → `InvalidOperationException`. |
| **Чтение** | Primary, named root, read-only (**group**, **public**, …). |
| Status | `read_only_routing_enabled: true`, если есть `[[knowledge.read_only]]`. |

Реализация: `AgentNotes.Core.KnowledgeRootResolution`, вызов из `NotesStorage` и MCP `ToolHandlers`.

## Реестр «файл → root» + hot (личный канон)

По аналогии с **`workspace-scope-map-v1.md`** (путь → scope), в **личном** каноне:

| Артефакт | Где | Назначение |
|----------|-----|------------|
| **`work/local/knowledge-roots-index-v1.md`** | primary KB, machine-local | строки `knowledge/relative/path => group` \| `public` \| `user` — где живёт SSOT текста |
| **Секция `knowledge-roots-routing-v1`** | `agent-notes.md` (hot) | постоянный контракт: если в TOML есть `group` → смотри реестр, читай `read_knowledge_file(..., knowledge_root_id=group)` |
| **Пути клонов** | TOML `--config` `[[knowledge.read_only]]` | **не** в реестре KB (только id + disk path) |

Шаблоны в каноне: `knowledge/work/local/knowledge-roots-index-v1.example.md`, `hot-section-knowledge-roots-routing-v1.example.md`.

### Формат реестра

- Одна строка — одна запись: `relative/path/under/knowledge/ => group` (без `/` в конце — один файл; с `/` в конце — префикс каталога, см. AgentNotes.Core ADR 016, Core 2.1.2+)
- `user` или отсутствие второй части = primary (после import в personal строку убрать или сменить на `user`)
- Строки с `#` — комментарии; без полнотекстовых копий playbook из group

### Поток агента

1. `route_context` — при запросе про group/public/roots/chmod/registry или совпадении строки реестра MCP подмешивает hot-секцию **`knowledge-roots-routing-v1`**, хиты из `work/local/knowledge-roots-index-v1.md` и короткий preview из read-only root (`knowledge_roots_overlay_applied` в JSON).
2. Явно при необходимости: `read_knowledge_file("work/local/knowledge-roots-index-v1.md")` на primary.
3. Для строк `foo.md => group` → `read_knowledge_file("foo.md", knowledge_root_id: "group")`.

## group-kb (smoke)

Локальный шаблон: `Financial/software/open/group-kb` (целевой remote — **`AI-Guiders/kb`**, private).

```toml
[[knowledge.read_only]]
id = "group"
path = "D:/Experiments/PersonalCursorFolder/Financial/software/open/group-kb"
```

Проверка: `read_knowledge_file` + `file_path=group/smoke-test-v1.md` + `knowledge_root_id=group`.

## Не в scope

- Авто-merge group → user (personal).

## Критерии принятия

- Unit-тесты `MultiRootKnowledgeTests` + `GroupKbCloneIntegrationTests` зелёные.
- Ручной MCP: чтение smoke по `knowledge_root_id=group`.
- Запись в group-kb через MCP отклоняется.
- В каноне: example-реестр + example hot-секция; `work/local/README` описывает реестр.
