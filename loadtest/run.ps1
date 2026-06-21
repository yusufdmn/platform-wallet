# Loads the repo-root .env, maps the values k6 needs, and runs the load test.
# Usage:  ./loadtest/run.ps1
# Override accounts:  ./loadtest/run.ps1 -AccountIds "guid1,guid2,guid3"

param(
    [string]$AccountIds,
    [string]$EnvFile = (Join-Path $PSScriptRoot "..\.env")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) { throw "No .env found at $EnvFile" }

# Parse KEY=VALUE lines (ignore comments/blanks, strip surrounding quotes).
$envMap = @{}
foreach ($line in Get-Content $EnvFile) {
    $t = $line.Trim()
    if ($t -eq "" -or $t.StartsWith("#")) { continue }
    $i = $t.IndexOf("=")
    if ($i -lt 1) { continue }
    $k = $t.Substring(0, $i).Trim()
    $v = $t.Substring($i + 1)
    # Strip trailing inline comment (whitespace followed by '#').
    $v = [regex]::Replace($v, '\s+#.*$', '')
    $v = $v.Trim().Trim('"').Trim("'")
    $envMap[$k] = $v
}

function Need($name) {
    if (-not $envMap.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($envMap[$name])) {
        throw "Missing '$name' in $EnvFile"
    }
    return $envMap[$name]
}

$env:GATEWAY_URL          = Need "GATEWAY_URL"
$env:KEYCLOAK_AUTHORITY   = Need "KEYCLOAK_AUTHORITY"
# The data-plane client secret lives under KEYCLOAK_CLIENT_SECRET in .env.
$env:LEDGER_CLIENT_SECRET = Need "KEYCLOAK_CLIENT_SECRET"
if ($envMap.ContainsKey("LEDGER_CLIENT_ID")) { $env:LEDGER_CLIENT_ID = $envMap["LEDGER_CLIENT_ID"] }

# Accounts: DBs are fresh and accounts are created on first mint, so fixed GUIDs
# are fine. -AccountIds flag wins; else ACCOUNT_IDS in .env; else the script's
# built-in defaults.
if ($AccountIds) {
    $env:ACCOUNT_IDS = $AccountIds
} elseif ($envMap.ContainsKey("ACCOUNT_IDS")) {
    $env:ACCOUNT_IDS = $envMap["ACCOUNT_IDS"]
}

Write-Host "Gateway:  $($env:GATEWAY_URL)"
Write-Host "Accounts: $($env:ACCOUNT_IDS)"
k6 run (Join-Path $PSScriptRoot "wallet-load.js")
