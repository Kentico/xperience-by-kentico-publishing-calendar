<#
.SYNOPSIS
    Refreshes the DancingGoat demo data so the publishing calendar shows current, varied content.

.DESCRIPTION
    Calls DancingGoat's demo data refresh endpoint (POST /api/demo-data/refresh, Development and CI
    environments only, loopback requests only) and prints its summary. The refresh is deterministic
    and relative to the server's local date: running it again the same day gives the same result.
    It changes the database only, never App_Data/CIRepository.

    DancingGoat must be running. Exits with a nonzero code when the refresh fails.

.EXAMPLE
    ./scripts/Update-DemoData.ps1
    ./scripts/Update-DemoData.ps1 -BaseUrl http://localhost:52196
#>
[CmdletBinding()]
param (
    # DancingGoat URL. Defaults to DANCING_GOAT_BASE_URL, then the DancingGoat launch profile
    [string]$BaseUrl = $env:DANCING_GOAT_BASE_URL,

    # Request timeout
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'

if (-not $BaseUrl) {
    $launchSettings = Join-Path $PSScriptRoot '..' 'examples' 'DancingGoat' 'Properties' 'launchSettings.json'
    $BaseUrl = ((Get-Content $launchSettings -Raw | ConvertFrom-Json).profiles.DancingGoat.applicationUrl -split ';')[0]
}

$uri = "$($BaseUrl.TrimEnd('/'))/api/demo-data/refresh"
Write-Host "Refreshing demo data: POST $uri"

try {
    $summary = Invoke-RestMethod -Method Post -Uri $uri -Headers @{ 'X-Demo-Data-Refresh' = 'true' } -TimeoutSec $TimeoutSeconds
}
catch {
    Write-Error "Demo data refresh failed: $($_.Exception.Message) $($_.ErrorDetails.Message)" -ErrorAction Continue
    exit 1
}

$summary | ConvertTo-Json -Depth 5
