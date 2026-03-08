# Agent Notes MCP

MCP-сервер для долговременных заметок агента. Базовый файл: `workspace_path/.cascade-ide/agent-notes.md` (или путь из `AGENT_NOTES_FILE`). Для непрерывности между сессиями и до суммаризации — подключается в Cursor без Cascade IDE.

## Central Wiki

- Единая wiki команды: [Agent Notes Wiki](http://193.124.113.7/Krawler/agent-notes/-/wikis/home)
- В этом проекте wiki не дублируется; здесь только ссылка на центральный источник.

MLP-v1 в этом репозитории:

- атомарная запись;
- автоматические ревизии перед изменениями;
- безопасный append и точечный upsert по секциям;
- поиск по заметкам;
- rollback к ревизии.

Новые возможности v0.3.0:

- тонкая загрузка hot-context (`read_hot_context`);
- целевое извлечение из архивных ревизий (`extract_from_archive`);
- полуавтоматическое ужатие hot-context (`compact_hot_context`).

Новые возможности v0.4.0:

- health-check памяти (`memory_health`);
- router-first контекст-пакет по запросу (`route_context`);
- контракт `KB v2` для эксплуатационного режима (`KB-V2-CONTRACT.md`).

Новые возможности v0.5.0:

- запись и чтение файлов слоя **knowledge** канона: `write_knowledge_file` (полная замена), `append_knowledge_file` (добавить в конец без перезаписи), `upsert_knowledge_section` (точечное вставка/обновление секции по маркерам `<!-- section:ID -->`), `read_knowledge_file`. Путь к канону — `canon_path` или `AGENT_NOTES_CANON_PATH`. Меньше риска случайно перезаписать весь файл при точечных правках.

## Стек

- C#, .NET 10, win-x64, self-contained (как dotnet-debug-mcp и roslyn-mcp).

## Публикация

```bash
dotnet publish -c Release -o publish
```

Рекомендуется junction: например `D:\agent-notes-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\agent-notes-mcp\AgentNotesMcp.exe`, `args`: `[]`.

### Релизы (без Runner)

GitLab Runner не используется (нет Docker/Linux). Чтобы выложить win-x64 в релиз с твоего Windows:

1. Задай переменные: `GITLAB_URL` (например `http://193.124.113.7`), `GITLAB_TOKEN` (Personal Access Token с api).
2. Из корня репо:
   - только залить zip в Generic Package и добавить ссылку в **существующий** релиз:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08`
   - создать релиз от текущего коммита и привязать zip:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease`

Релиз должен уже существовать (создан вручную или через API), если не передаёшь `-CreateRelease`.

## Тулы