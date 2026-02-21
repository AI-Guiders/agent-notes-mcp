# Agent Notes MCP

MCP-сервер с двумя тулами: **write_agent_notes** и **read_agent_notes**. Агент сам решает, когда, что и в каком формате сохранять в заметки; файл лежит в `workspace_path/.cascade-ide/agent-notes.md`. Для непрерывности между сессиями и до суммаризации — подключается в Cursor без Cascade IDE.

## Стек

- C#, .NET 10, win-x64, self-contained (как dotnet-debug-mcp и roslyn-mcp).

## Публикация

```bash
dotnet publish -c Release -o publish
```

Рекомендуется junction: например `D:\agent-notes-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\agent-notes-mcp\AgentNotesMcp.exe`, `args`: `[]`.

## Тулы

| Имя | Описание | Аргументы |
|-----|----------|----------|
| `write_agent_notes` | Записать заметки (перезапись файла). | `workspace_path`, `content` |
| `read_agent_notes` | Прочитать заметки. | `workspace_path` |

`workspace_path` — каталог workspace (корень проекта в Cursor). Файл: `workspace_path/.cascade-ide/agent-notes.md`.

## Репозиторий и субмодуль

Проект предназначен для отдельного репо на GitLab и подключения как субмодуль в репо **open** (financial-open), наряду с dotnet-debug-mcp, roslyn-mcp, cascade-ide.

После создания репо на GitLab (например `Krawler/agent-notes-mcp`), пуша туда этого кода и перехода в каталог `open` выполнить:

```bash
git submodule add http://193.124.113.7/Krawler/agent-notes-mcp.git agent-notes-mcp
```
