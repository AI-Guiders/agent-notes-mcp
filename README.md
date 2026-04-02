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
- контракт совместного действия агентов и людей (`coexistence-framework-v1.md`).

Новые возможности v0.5.0:

- запись и чтение файлов слоя **knowledge** канона: `write_knowledge_file` (полная замена), `append_knowledge_file` (добавить в конец без перезаписи), `upsert_knowledge_section` (точечное вставка/обновление секции по маркерам `<!-- section:ID -->`), `read_knowledge_file`. Путь к канону — `canon_path` или `AGENT_NOTES_CANON_PATH`. Меньше риска случайно перезаписать весь файл при точечных правках.

## Стек

- C#, .NET 10, win-x64, self-contained (как dotnet-debug-mcp и roslyn-mcp).

## Публикация

Публиковать **только** основной проект (в solution `AgentNotesMcp.slnx` ещё тесты — иначе `dotnet publish` на `.slnx` может смешать вывод тестового проекта с `publish/`).

**Self-contained (win-x64):** в `AgentNotesMcp.csproj` заданы `<SelfContained>true</SelfContained>` и `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, поэтому команда ниже уже кладёт в `publish/` **полный** рантайм .NET под Windows x64 — отдельно ставить shared runtime на машину не нужно.

```bash
dotnet publish AgentNotesMcp.csproj -c Release -o publish
```

Явно те же настройки (если убрать RID/SelfContained из csproj или переопределить):

```bash
dotnet publish AgentNotesMcp.csproj -c Release -o publish -r win-x64 --self-contained true
```

Другой RID (например `linux-x64`) — передай `-r <rid>`; кросс-сборка нескольких платформ с Windows — см. `scripts/publish-release-win.ps1` ниже.

Рекомендуется junction: например `D:\agent-notes-mcp` → каталог `publish`; в Cursor в mcp.json указать `command`: `D:\agent-notes-mcp\AgentNotesMcp.exe`, `args`: `[]`.

### Релиз Ubuntu 25.10 (GitLab CI)

Для **linux-x64** есть пайплайн на образе **ubuntu:25.10**: при push **тега** вида `v2026.03.22-ubuntu2510` job собирает self-contained zip и job **release** создаёт [GitLab Release](http://193.124.113.7/Krawler/agent-notes-mcp/-/releases) с прикреплённым `agent-notes-mcp-linux-x64.zip`.

На `main` без тега этот пайплайн не запускается (только по тегу `v*`).

### Релизы с Windows (без Runner / кросс-компиляция)

Скрипт собирает с Windows релизы для **win-x64**, **linux-x64** и **osx-x64** и кладёт в Generic Package по одному zip на платформу.

1. Задай переменные: `GITLAB_URL` (например `http://193.124.113.7`), `GITLAB_TOKEN` (Personal Access Token с api).
2. Из корня репо:
   - залить артефакты в **существующий** релиз:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08`
   - создать релиз от текущего коммита и привязать все zip:  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease`
   - только часть платформ (например без macOS):  
     `.\scripts\publish-release-win.ps1 -Version 2026.03.08 -Rids win-x64,linux-x64 -CreateRelease`

По умолчанию собираются все три платформы; при ошибке сборки одной остальные всё равно заливаются.

## Тулы

Полный перечень имён и описаний (как у инструментов в MCP) — **[docs/MCP-TOOLS.md](docs/MCP-TOOLS.md)**. Тот же источник даёт `mcp-tools.manifest.json` в корне проекта. Обновить оба файла из кода:

`dotnet run --project tools/ExportMcpManifest -- --write` (рабочий каталог — корень `agent-notes-mcp`).

`workspace_path` — каталог workspace (корень проекта в Cursor).  
Файл: `workspace_path/.cascade-ide/agent-notes.md` (**если не задана** переменная окружения `AGENT_NOTES_FILE`).  
Ревизии: `workspace_path/.cascade-ide/.revisions/*.md`.

### Слой knowledge (канон)

Инструменты `write_knowledge_file` и `read_knowledge_file` работают с каталогом **knowledge/** репозитория-канона (agent-notes), а не с текущим workspace. Это нужно, когда агент работает в другом workspace (например PersonalCursorFolder), а править надо файлы в каноне (index, kb-*, playbook-* и т.д.).

- **canon_path** — корень репо agent-notes (каталог, в котором лежит подкаталог `knowledge/`). Опционален, если задана переменная окружения **AGENT_NOTES_CANON_PATH**.
- **file_path** — относительный путь внутри `knowledge/`, например `kb-music-acoustics-v1.md`, `playbook-music-v1.md`. Не допускаются `..` и абсолютные пути.
- **Когда что использовать:** полная замена файла — `write_knowledge_file`; добавить блок в конец — `append_knowledge_file`; вставить/обновить секцию по ID — `upsert_knowledge_section`; удалить секцию по ID — `delete_knowledge_section`. Append, upsert и delete снижают риск затереть весь файл.
- Рекомендация: в окружении, где запускается MCP, задать `AGENT_NOTES_CANON_PATH=d:\Experiments\agent-notes` (или свой путь к канону), тогда при вызове можно не передавать `canon_path`.

## Формат секций для upsert

`upsert_agent_notes_section` использует маркеры:

```md
<!-- section:team-rules -->
... содержимое секции ...
<!-- /section:team-rules -->
```

Если секция уже есть — заменяется целиком; если нет — добавляется в конец файла.

### Workspace scope map (опционально)

Чтобы `read_hot_context` выбирал scope по workspace автоматически, можно добавить секцию:

```md
<!-- section:workspace-scope-map-v1 -->
- d:\Experiments\PersonalCursorFolder => current-projects
- c:\Projects\EDW.Portal.Repo => portal
<!-- /section:workspace-scope-map-v1 -->
```

Поддерживаются разделители `=>`, `:` и `=`.  
Матч по пути: сначала exact, иначе longest-prefix (с проверкой границы `\`), после нормализации `/` vs `\` и хвостового `\`.

Примечание про “глобальный” режим:

- Если задан `AGENT_NOTES_FILE`, заметки читаются/пишутся в один общий файл для всех workspace.
- При этом `workspace_path` **всё равно важен**: он влияет на выбор scope через `workspace-scope-map-v1` (и используется как fallback, если `active_scope` не передан).

## Репозиторий и субмодуль (legacy заметка)

Проект предназначен для отдельного репо на GitLab и подключения как субмодуль в репо **open** (financial-open), наряду с dotnet-debug-mcp, roslyn-mcp, cascade-ide.

Этот README и код в каталоге `open/agent-notes-mcp` отражают **legacy‑состояние** проекта (эксперименты до публикации Integrity POST и TPM‑архитектуры).  
Актуальный репозиторий и место публикации могут отличаться; конкретный URL и хост следует настраивать в своём окружении самостоятельно.

Пример добавления субмодуля в своём репозитории:

```bash
git submodule add <URL_ТВОЕГО_REPO_AGENT_NOTES_MCP> agent-notes-mcp
```

> Важно: любые ранние реализации integrity/POST из legacy‑репозиториев **не считаются** TPM‑корнями или авторитетными источниками для Integrity POST — валидными являются только версии `integrity-core` и спецификации, опубликованные через назначенные TPM‑узлы (см. `knowledge/META/integrity-post-spec-v1.md` и документы kb-public).
