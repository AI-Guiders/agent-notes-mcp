# ADR 008 (MCP): резолв `active_scope` и карта workspace → scope

**Статус:** Accepted (зеркало KB)  
**Дата:** 2026-05-12  

**Канонический текст (KB):** `knowledge/adr/008-workspace-scope-map-hot-mcp-and-public-cut.md`.

## Связанные ADR

| ADR | Роль |
|-----|------|
| [014](014-agent-notes-local-settings-toml-v1.md) | `[workspace]` в TOML (`--config`, MCP 2.0) |

## Резюме

- Цепочка **`ResolveScope`:** явный `active_scope` → карта путей → секция `active-scope` → fallback `door-to-singularity`.
- С **MCP 2.0** алиасы и `scope_map` из TOML; META JSON и `mcp-resolve-paths-v1.json` в рантайме **не** читаются.
- Карта путей в публичном kb-public **за public-cut**; полный hot — только локальный клон.

---

## Контекст для разработчиков MCP

Инструменты **`read_hot_context`**, **`route_context`**, **`memory_health`** принимают опциональный параметр **`active_scope`** и при его отсутствии выводят scope из цепочки, реализованной в **`NotesStorage.ResolveScope`** (см. исходники: парсинг секций hot-документа).

---

## Реализованный контракт (код)

0. **Bootstrap путей:** при загруженном **`--config`** (MCP 2.0) — **`[workspace].scope_map`** и **`scope_aliases`** из TOML. Без runtime (in-proc / тесты) — **embedded** `mcp-resolve-paths-defaults.json` в **AgentNotes.Core** (`work/local/...`). Файл **`knowledge/META/mcp-resolve-paths-v1.json`** в **2.0 не читается**.

1. Если **`active_scope`** передан и не пустой — нормализация алиасов из файла по **`scope_alias_map`** (см. п.0). Встроенной таблицы в коде нет. Формат строк как у карты workspace: краткий ключ, затем `=>` / `:` / `=` и **канонический** id slice (совпадает с суффиксом секции `scope-<id>`). Строки-пути Windows в этот файл не кладутся (отфильтровываются).
2. Иначе — **`TryResolveScopeFromWorkspaceMap`**: файл по **`workspace_scope_map`** (см. п.0); иначе содержимое секции **`workspace-scope-map-v1`** (fallback: legacy **`scope-map-v1`**); строки вида `path => scope`, самый длинный префикс пути к **`workspace_path`** выигрывает. Это **отдельная** ось: **путь → slice**, не алиасы коротких имён.
3. Иначе — секция **`active-scope`**: если есть строка **`current:`** — её значение (легаси-оверрайд), снова через словарь алиасов из п.1.
4. Иначе — **`door-to-singularity`**.

Карта путей в публичной сборке **kb-public не должна** присутствовать: в каноне для автора первая граница **`<!-- public-cut -->`** стоит **до** секции карты; полный hot с картой — только в локальном/полном клоне канона.

---

## Эволюция

- **MCP 2.0 ([014](014-agent-notes-local-settings-toml-v1.md)):** п.0 — TOML `[workspace]`; META JSON удалён из кода.
- Карта и алиасы по-прежнему в markdown под primary knowledge root; при смене алиасов — **`scope-alias-map-v1.md`** и тесты **`TestScopeAliasesMd`**.
