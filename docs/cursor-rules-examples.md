# Cursor: примеры правил (копипаст)

Готовые фрагменты для `.cursor/rules/*.mdc` в корне **того workspace**, который открыт в Cursor. Репозиторий открыт; можно копировать в свой форк или в монорепо.

## Integrity POST + канон KB

Только если ведёшь канон agent-notes с `knowledge/` и хочешь то же в Cursor.

Если рядом с проектом есть клон **канона** (каталог `knowledge/META/integrity-core.md`) или ты работаешь из корня канона — добавь правило целостности.

**Эталон в каноне (обновляется там же):**  
https://github.com/KarataevDmitry/knowledge-base/blob/main/knowledge/META/cursor-rule-integrity-post-example.md  

Скопируй из этого файла блок между `~~~mdc` и `~~~` в `.cursor/rules/integrity-core-immutable.mdc` (или своё имя). Пути `knowledge/META/...` должны резолвиться из корня открытого workspace; при другом layout поправь пути в копии.

**Связка с Agent Notes MCP 2.0:** в `mcp.json` укажи `--config` на TOML, где `[knowledge].primary` указывает на корень клона с `knowledge/` — тогда агент читает тот же KB, что и правило.

---

## Дополнительно

Правила под другие MCP (Roslyn, Git, отладка, рефакторинг в IDE) — только в **их** репозиториях: `roslyn-mcp`, `git-mcp`, `dotnet-debug-mcp`, `cascade-ide` и т.д. — у каждого свой `docs/cursor-rules-examples.md` без канона KB.
