# Publish win-x64 build to GitLab: Generic Package + optional GitLab Release.
# Run from repo root on Windows. Required: GITLAB_URL, GITLAB_TOKEN.
# Usage:
#   .\scripts\publish-release-win.ps1 -Version 0.5.1 -CreateRelease

param(
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [string] $Tag = "v$Version",
    [string] $GitLabUrl,
    [string] $Token,
    [string] $ProjectPath = "Krawler/agent-notes-mcp",
    [string] $Rid = "win-x64",
    [switch] $CreateRelease
)

$ErrorActionPreference = "Stop"
$baseUrl = if ($GitLabUrl) { $GitLabUrl.TrimEnd('/') } else { $env:GITLAB_URL?.TrimEnd('/') }
$token  = if ($Token) { $Token } else { $env:GITLAB_TOKEN }
if (-not $baseUrl -or -not $token) { throw "Set GITLAB_URL and GITLAB_TOKEN (or pass -GitLabUrl and -Token)." }

$projectId = $ProjectPath -replace '/', '%2F'
$api = "$baseUrl/api/v4"
$pkgName = "agent-notes-mcp"

$zipName = "agent-notes-mcp-$Rid.zip"
$outDir = "publish-release-temp-$Rid"
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

Write-Host "Building $Rid ..."
dotnet publish -c Release -r $Rid -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid" }

$zipPath = Join-Path $PWD $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$outDir\\*" -DestinationPath $zipPath
Remove-Item -Recurse -Force $outDir
Write-Host "  -> $zipName"

$uploadUrl = "$api/projects/$projectId/packages/generic/$pkgName/$Version/$zipName"
Write-Host "Uploading $zipName ..."
Invoke-RestMethod -Uri $uploadUrl -Method Put -InFile $zipPath -Headers @{ "PRIVATE-TOKEN" = $token } -ContentType "application/octet-stream"

if ($CreateRelease) {
    $commitSha = (git rev-parse HEAD).Trim()
    $body = @{ tag_name = $Tag; ref = $commitSha; name = "Release $Tag"; description = "Pre-built: $Rid (no Runner)." } | ConvertTo-Json
    Invoke-RestMethod -Uri "$api/projects/$projectId/releases" -Method Post -Headers @{ "PRIVATE-TOKEN" = $token } -Body $body -ContentType "application/json"
    Write-Host "Release $Tag created."

    $assetUrl = "$api/projects/$projectId/packages/generic/$pkgName/$Version/$zipName"
    $linkBody = @{ name = $zipName; url = $assetUrl; link_type = "package" } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri "$api/projects/$projectId/releases/$Tag/assets/links" -Method Post -Headers @{ "PRIVATE-TOKEN" = $token } -Body $linkBody -ContentType "application/json; charset=utf-8"
        Write-Host "Asset link added: $zipName"
    } catch {
        Write-Warning "Could not add asset link for $zipName: $_"
    }
}

Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Write-Host "Done."

