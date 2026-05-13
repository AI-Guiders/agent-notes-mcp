# Сборка, релизы и CI (для держателей конвейера)

Здесь — то, что нужно **тем, кто собирает артефакты и настраивает GitLab**, а не для первого знакомства с MCP. Публичный обзор — в корневом `README.md`.

## Локальный publish (Windows)

Скрипт **`publish-and-deploy.ps1`** в корне репо: self-contained `win-x64`, копирование в фиксированный каталог (по умолчанию `D:\agent-notes-mcp`), остановка процесса, если он держит файлы.

```powershell
.\publish-and-deploy.ps1
```

Ручная сборка:

```bash
dotnet publish AgentNotesMcp.csproj -c Release -o publish
```

В `AgentNotesMcp.csproj` заданы `SelfContained` и `win-x64` для типичного сценария; другой RID — `-r <rid>`. Кросс-сборка нескольких платформ с Windows — `scripts/publish-release-win.ps1`.

## Релиз Ubuntu 25.10 (GitLab CI)

Пайплайн на образе **ubuntu:25.10** для **linux-x64**: при push **тега** вида `v2026.03.22-ubuntu2510` собирается self-contained zip, job **release** создаёт GitLab Release с `agent-notes-mcp-linux-x64.zip`. На `main` без тега пайплайн не запускается (только по тегу `v*`).

URL релизов зависит от инстанса GitLab, где подключён CI (см. `.gitlab-ci.yml` и настройки проекта).

## Релизы с Windows (Generic Package / релиз в GitLab)

Скрипт `scripts/publish-release-win.ps1` собирает **win-x64**, **linux-x64**, **osx-x64** и заливает zip в Generic Package.

1. Переменные окружения: `GITLAB_URL` (базовый URL своего GitLab), `GITLAB_TOKEN` (PAT с `api`).
2. Примеры:

```powershell
.\scripts\publish-release-win.ps1 -Version 2026.03.08
.\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease
.\scripts\publish-release-win.ps1 -Version 2026.03.08 -Rids win-x64,linux-x64 -CreateRelease
```

## Несколько remote (зеркала)

Если нужен один `origin` с несколькими push-URL и отдельный `github`, настройка зависит от хостов команды — не фиксируется в публичном README; см. внутреннюю wiki или операционные заметки канона.
