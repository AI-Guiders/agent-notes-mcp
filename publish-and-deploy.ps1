# Publish Release (win-x64, self-contained) and mirror to a fixed path for Cursor MCP.
# Run from repo:  cd ...\agent-notes-mcp  ;  .\publish-and-deploy.ps1
# Optional: -Target "D:\agent-notes-mcp"
[CmdletBinding()]
param(
    [string] $Target = "D:\agent-notes-mcp"
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$csproj = Join-Path $here "AgentNotesMcp.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Error "AgentNotesMcp.csproj not found. Run this script from the agent-notes-mcp directory (PSScriptRoot=$here)."
    exit 1
}

Push-Location $here
try {
    # Keep docs/manifests in sync with ToolCatalog.
    & dotnet run --project (Join-Path $here "tools\\ExportMcpManifest\\ExportMcpManifest.csproj") -- --write | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Prefer local tool (repo-pinned), but global install works too.
    if (Test-Path -LiteralPath (Join-Path $here ".config\\dotnet-tools.json")) {
        & dotnet tool restore | Out-Null
        & dotnet aid-publish -Project $csproj -Target $Target -Runtime "win-x64" -Configuration "Release" -SelfContained -KillRunning
    } else {
        & aid-publish -Project $csproj -Target $Target -Runtime "win-x64" -Configuration "Release" -SelfContained -KillRunning
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $Target "AgentNotesMcp.exe"
    $exeJson = $exe.Replace('\', '\\')
    Write-Host ""
    Write-Host "Cursor MCP: paste into mcp.json ->"
    Write-Host @"
  "agent-notes-mcp": {
    "command": "$exeJson",
    "args": []
  }
"@
    Write-Host ""
} finally {
    Pop-Location
}

