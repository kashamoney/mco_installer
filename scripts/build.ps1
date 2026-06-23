param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "",
    [switch]$StrictPayload
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\McoInstaller\McoInstaller.csproj"
$payload = Join-Path $repoRoot "payload"

if (-not $Output) {
    $Output = Join-Path $repoRoot "dist\$Runtime"
}

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

[xml]$projectXml = Get-Content -LiteralPath $project
$projectVersion = $projectXml.Project.PropertyGroup |
    ForEach-Object { $_.VersionPrefix } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if (-not $projectVersion) {
    $projectVersion = $projectXml.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
}

if (-not $projectVersion) {
    throw "Missing <VersionPrefix> or <Version> in $project"
}

if (-not (Test-Path (Join-Path $payload "server.json"))) {
    throw "Missing payload\server.json"
}

$hasUpdateZip = Test-Path (Join-Path $payload "update.zip")
$hasUpdateDir = Test-Path (Join-Path $payload "update")
$hasCert = (Test-Path (Join-Path $payload "server.crt")) -or (Test-Path (Join-Path $payload "server.cer"))
$hasKey = (Test-Path (Join-Path $payload "pub.key")) -or (Test-Path (Join-Path $payload "pubori.key"))

$missing = @()
if (-not ($hasUpdateZip -or $hasUpdateDir)) { $missing += "payload\update.zip or payload\update\" }
if (-not $hasCert) { $missing += "payload\server.crt" }
if (-not $hasKey) { $missing += "payload\pub.key" }

if ($missing.Count -gt 0) {
    $message = "Recommended payload file(s) missing:`n - " + ($missing -join "`n - ")
    if ($StrictPayload) {
        throw $message
    }

    Write-Warning $message
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $Output `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=false `
    /p:IncludeNativeLibrariesForSelfExtract=true

$publishedInstaller = Join-Path $Output "McoInstaller.exe"
$versionedInstaller = Join-Path $Output ("McoInstaller-v{0}.exe" -f $projectVersion)
Copy-Item -LiteralPath $publishedInstaller -Destination $versionedInstaller -Force

$userReadme = Join-Path $repoRoot "USER_README.txt"
if (Test-Path $userReadme) {
    $readmeText = Get-Content -LiteralPath $userReadme -Raw
    $readmeText = $readmeText.Replace("{{INSTALLER_VERSION}}", $projectVersion)
    Set-Content -LiteralPath (Join-Path $Output "README.txt") -Value $readmeText -NoNewline
}

Write-Host "Published installer:"
Write-Host $publishedInstaller
Write-Host $versionedInstaller
