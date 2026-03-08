# Publish win-x64 build to GitLab: Generic Package + release asset link.
# Run from repo root on Windows (no Runner / Docker required).
#
# Required env (or pass -GitLabUrl / -Token):
#   GITLAB_URL   - e.g. http://193.124.113.7
#   GITLAB_TOKEN - Personal Access Token (api, read_api, write_repository)
#
# Usage:
#   .\scripts\publish-release-win.ps1 -Version 2026.03.08
#   .\scripts\publish-release-win.ps1 -Version 2026.03.08 -Tag v2026.03.08
#   .\scripts\publish-release-win.ps1 -Version 2026.03.08 -CreateRelease  # create release from current main

param(
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [string] $Tag = "v$Version",
    [string] $GitLabUrl,
    [string] $Token,
    [string] $ProjectPath = "Krawler/agent-notes-mcp",
    [switch] $CreateRelease
)

$ErrorActionPreference = "Stop"
$baseUrl = if ($GitLabUrl) { $GitLabUrl.TrimEnd('/') } else { $env:GITLAB_URL?.TrimEnd('/') }
$token  = if ($Token) { $Token } else { $env:GITLAB_TOKEN }
if (-not $baseUrl -or -not $token) {
    Write-Error "Set GITLAB_URL and GITLAB_TOKEN (or pass -GitLabUrl and -Token)."
}
$projectId = $ProjectPath -replace '/', '%2F'
$api = "$baseUrl/api/v4"
$zipName = "agent-notes-mcp-win-x64.zip"
$pkgName = "agent-notes-mcp"

# Build
$outDir = "publish-release-temp"
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
dotnet publish -c Release -o $outDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed." }

# Zip
$zipPath = Join-Path $PWD $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath
Remove-Item -Recurse -Force $outDir

# Upload to Generic Package
$uploadUrl = "$api/projects/$projectId/packages/generic/$pkgName/$Version/$zipName"
Write-Host "Uploading to $uploadUrl ..."
Invoke-RestMethod -Uri $uploadUrl -Method Put -InFile $zipPath -Headers @{ "PRIVATE-TOKEN" = $token } -ContentType "application/octet-stream"
Write-Host "Uploaded."

# Create release if requested (tag from current commit)
if ($CreateRelease) {
    $commitSha = (git rev-parse HEAD).Trim()
    $body = @{
        tag_name  = $Tag
        ref       = $commitSha
        name     = "Release $Tag"
        description = "Pre-built win-x64 (no Runner)."
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$api/projects/$projectId/releases" -Method Post -Headers @{ "PRIVATE-TOKEN" = $token } -Body $body -ContentType "application/json"
    Write-Host "Release $Tag created."
}

# Add asset link to release (release must already exist)
$assetUrl = "$api/projects/$projectId/packages/generic/$pkgName/$Version/$zipName"
$linkBody = @{ name = $zipName; url = $assetUrl; link_type = "package" } | ConvertTo-Json
try {
    Invoke-RestMethod -Uri "$api/projects/$projectId/releases/$Tag/assets/links" -Method Post -Headers @{ "PRIVATE-TOKEN" = $token } -Body $linkBody -ContentType "application/json; charset=utf-8"
    Write-Host "Asset link added to release $Tag."
} catch {
    Write-Warning "Could not add release asset link (release may not exist or link already present): $_"
}

Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Write-Host "Done."
