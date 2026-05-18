# ADR (agent-notes-mcp)

Короткие архитектурные решения, специфичные для **сервера MCP** и его контракта с hot-файлом `agent-notes.md` и каноном KB.

**Полный канон KB** (включая ADR, затрагивающие и KB, и MCP): репозиторий **agent-notes**, каталог `knowledge/adr/`. При открытом каноне рядом с MCP читай оттуда как источник истины.

Шапка ADR в этом репо — [snippets/adr-header-convention-mcp.md](snippets/adr-header-convention-mcp.md).

## Список

| ADR | Тема |
|-----|------|
| [008](008-workspace-scope-map-resolution.md) | Резолв `active_scope`, карта workspace → scope, связь с `public-cut` в KB |
| [013](013-localhost-status-surface-v1.md) | **AgentNotesStatus:** localhost HTTP, секция `[status]` в TOML |
| [014](014-agent-notes-local-settings-toml-v1.md) | **TOML по `--config`** (как DBHub); зеркало KB ADR 013; Tomlyn, вывод legacy env |
| [015](015-multi-root-read-only-knowledge-routing-v1.md) | **`knowledge_root_id`** + read-only roots; chmod ugo (**group**); group-kb smoke |
