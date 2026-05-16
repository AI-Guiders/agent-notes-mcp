# ADR (agent-notes-mcp)

Короткие архитектурные решения, специфичные для **сервера MCP** и его контракта с hot-файлом `agent-notes.md` и каноном KB.

**Полный канон KB** (включая ADR, затрагивающие и KB, и MCP): репозиторий **agent-notes**, каталог `knowledge/adr/`. При открытом каноне рядом с MCP читай оттуда как источник истины.

## Список

| ADR | Тема |
|-----|------|
| [008](008-workspace-scope-map-resolution.md) | Резолв `active_scope`, карта workspace → scope, связь с `public-cut` в KB |
| [013](013-localhost-status-surface-v1.md) | **AgentNotesStatus:** opt-in HTTP на `127.0.0.1` — резолв путей, scope, `memory_health`, версия MCP (не IDE/PWA) |
