# ADR 008 (MCP): резолв `active_scope` и карта workspace → scope

**Статус:** зеркало решения в каноне KB  
**Дата:** 2026-05-12  

**Канонический текст (KB + публичный контур):** в репозитории **agent-notes** — `knowledge/adr/008-workspace-scope-map-hot-mcp-and-public-cut.md`.

---

## Контекст для разработчиков MCP

Инструменты **`read_hot_context`**, **`route_context`**, **`memory_health`** принимают опциональный параметр **`active_scope`** и при его отсутствии выводят scope из цепочки, реализованной в **`NotesStorage.ResolveScope`** (см. исходники: парсинг секций hot-документа).

---

## Реализованный контракт (код)

1. Если **`active_scope`** передан и не пустой — нормализация алиасов (`NormalizeScope`: `dts`/`current-projects` → `door-to-singularity`, `ptl` → `portal`, `hrv`/`edwh` → `harvester` и т.д.).
2. Иначе — **`TryResolveScopeFromWorkspaceMap`**: содержимое секции **`workspace-scope-map-v1`** (fallback: legacy **`scope-map-v1`**) в распарсенном **`agent-notes.md`**; строки вида `path => scope`, самый длинный префикс пути к `workspace_path` выигрывает.
3. Иначе — секция **`active-scope`**, поле `current:`.
4. Иначе — **`door-to-singularity`**.

Карта путей в публичной сборке **kb-public не должна** присутствовать: в каноне для автора первая граница **`<!-- public-cut -->`** стоит **до** секции карты; полный hot с картой — только в локальном/полном клоне канона.

---

## Планируемая эволюция (не в коде до отдельного PR)

Чтение карты из **отдельного файла** (например под `knowledge/work/local/…` в каноне) при пустой секции — см. раздел **часть B** в каноническом ADR. После внедрения обновить этот файл и `NotesStorageTests`.
