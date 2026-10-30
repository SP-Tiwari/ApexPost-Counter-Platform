<#
.SYNOPSIS
Runs ApexPost with fictional demo data and working demo logins.

.DESCRIPTION
Uses the "demo" launch profile (Development, DemoData:Initialize=true, data
kept under src/ApexPost.Web/data/demo). The demo staff password is read from
user-secrets (DemoData:StaffPassword); if none is set, a random one is
generated and stored there. No password is ever written to the repository.

.PARAMETER Password
Use this demo password instead of the stored or generated one.

.PARAMETER Reset
Delete the demo data folder first, so the seed and demo logins are recreated
(needed for a changed password to take effect).

.EXAMPLE
./scripts/run-demo.ps1
./scripts/run-demo.ps1 -Reset -Password 'my-local-demo-password'
#>
[CmdletBinding()]
param(
	[string]$Password,
	[switch]$Reset
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\src\ApexPost.Web')).Path
$demoData = Join-Path $project 'data\demo'
$secretKey = 'DemoData:StaffPassword'

function New-DemoPassword {
	$alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
	$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
	$buffer = New-Object byte[] 1
	$chars = New-Object System.Collections.Generic.List[char]
	while ($chars.Count -lt 20) {
		$rng.GetBytes($buffer)
		# Rejection sampling keeps every character equally likely.
		if ($buffer[0] -lt (256 - (256 % $alphabet.Length))) {
			$chars.Add($alphabet[$buffer[0] % $alphabet.Length])
		}
	}
	-join $chars
}

function Get-StoredPassword {
	$prefix = "$secretKey = "
	$line = dotnet user-secrets list --project $project |
		Where-Object { $_.StartsWith($prefix) } |
		Select-Object -First 1
	if ($line) { $line.Substring($prefix.Length) } else { $null }
}

if ($Reset -and (Test-Path $demoData)) {
	Write-Host "Removing demo data folder $demoData"
	Remove-Item -Recurse -Force $demoData
}

$stored = Get-StoredPassword
if ($Password) {
	if ($stored -and $stored -ne $Password -and -not $Reset -and (Test-Path $demoData)) {
		Write-Warning 'Existing demo logins keep their old password. Add -Reset to recreate them with the new one.'
	}
	dotnet user-secrets set $secretKey $Password --project $project | Out-Null
}
elseif (-not $stored) {
	$Password = New-DemoPassword
	dotnet user-secrets set $secretKey $Password --project $project | Out-Null
	Write-Host 'Generated a demo staff password and stored it in user-secrets.'
}
else {
	$Password = $stored
}

Write-Host ''
Write-Host 'ApexPost demo logins (branch DM01), all with the same password:'
Write-Host '  demo-staff-01  Counter Clerk'
Write-Host '  demo-staff-02  Branch Manager'
Write-Host '  demo-staff-03  Auditor'
Write-Host '  demo-staff-04  Administrator'
Write-Host "  Password: $Password"
Write-Host '  (demo-staff-05 to demo-staff-20 repeat the roles for branches DM02-DM05.)'
Write-Host ''
Write-Host 'Starting https://localhost:7034 - press Ctrl+C to stop.'

dotnet run --project $project --launch-profile demo
