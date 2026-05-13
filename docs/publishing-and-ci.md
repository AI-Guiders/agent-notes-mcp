# Сборка и релизы (PowerShell)

Здесь — **скрипты `.ps1`**, которыми собирают и при необходимости заливают артефакты. **Отдельного GitLab CI / пайплайна в эксплуатации нет** (файл `.gitlab-ci.yml` в репо может лежать как черновик или исторический хвост — ориентир по факту: только PowerShell). Публичный обзор MCP — в корневом `README.md`.

## Локальный publish (Windows)

**`publish-and-deploy.ps1`** в корне репо: self-contained `win-x64`, копирование в фиксированный каталог (по умолчанию `D:\agent-notes-mcp`), остановка процесса, если он держит файлы.

```powershell
.\publish-and-deploy.ps1
```

Ручная сборка:

```bash
dotnet publish AgentNotesMcp.csproj -c Release -o publish
```

В `AgentNotesMcp.csproj` заданы `SelfContained` и `win-x64` для типичного сценария; другой RID — `-r <rid>`.

## Кросс-платформенный релиз с Windows (`publish-release-win.ps1`)

**`scripts/publish-release-win.ps1`** с машины на Windows собирает **win-x64**, **linux-x64**, **osx-x64** и заливает zip в **Generic Package** GitLab через HTTP API (не через job’ы CI).

1. Переменные окружения: `GITLAB_URL` (базовый URL инстанса GitLab), `GITLAB_TOKEN` (PAT с `api`).
2. Примеры:

```powershell
.\scripts\publish-release-win.ps1 -Version 2026.03.08
.\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease
.\scripts\publish-release-win.ps1 -Version 2026.03.08 -Rids win-x64,linux-x64 -CreateRelease
```

По умолчанию собираются все три платформы; при ошибке одной остальные всё равно могут быть залиты (см. поведение в скрипте).

## Несколько remote (зеркала)

Если нужен один `origin` с несколькими push-URL и отдельный `github`, настройка зависит от хостов команды — см. внутреннюю wiki или операционные заметки канона.
