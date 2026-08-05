#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the test suites that execute production code, merges their coverage, and fails
    when it drops below the recorded floor.

.DESCRIPTION
    Coverage is collected by Microsoft.Testing.Extensions.CodeCoverage, the Microsoft
    Testing Platform collector. That extension can produce a report but cannot gate on
    one: its only options are coverage-output, coverage-output-format and
    coverage-settings. The threshold check therefore lives here rather than in
    .runsettings, and this script is what CI should call in Phase 2.

    Finmy.ArchitectureTests is deliberately left out. It reflects over the production
    assemblies without executing them, so including it reports every one of its ~2000
    loaded lines as uncovered and drags the number down for no reason.

    Thresholds sit a couple of points under the measured number, leaving room for the small
    run-to-run variation the container-backed tests introduce. They are meant to ratchet
    upward. Raise them when real tests arrive; never lower them to make a build pass.
#>
[CmdletBinding()]
param(
    [double] $LineThreshold = 60.0,
    [double] $BranchThreshold = 54.0,
    [switch] $SkipIntegration
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$settings = Join-Path $repositoryRoot '.runsettings'
$resultsDirectory = Join-Path $repositoryRoot 'TestResults'
$merged = Join-Path $resultsDirectory 'merged.cobertura.xml'

if (Test-Path $resultsDirectory) {
    Remove-Item $resultsDirectory -Recurse -Force
}

$projects = @(Join-Path $repositoryRoot 'tests/Finmy.UnitTests/Finmy.UnitTests.csproj')

if (-not $SkipIntegration) {
    # Needs Docker Desktop for Testcontainers.
    $projects += Join-Path $repositoryRoot 'tests/Finmy.IntegrationTests/Finmy.IntegrationTests.csproj'
}

foreach ($project in $projects) {
    Write-Host "Running $(Split-Path $project -Leaf)" -ForegroundColor Cyan

    dotnet test $project -- --coverage --coverage-settings $settings --coverage-output-format cobertura

    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed in $project (exit code $LASTEXITCODE). Coverage not evaluated."
    }
}

$reports = @(Get-ChildItem $resultsDirectory -Recurse -Filter '*.cobertura.xml' | Select-Object -ExpandProperty FullName)

if ($reports.Count -eq 0) {
    throw "No cobertura reports under $resultsDirectory. Check that .runsettings parses: the collector reports a malformed settings file as 'Zero tests ran'."
}

dotnet dotnet-coverage merge -o $merged -f cobertura $reports | Out-Null

[xml] $report = Get-Content $merged
$lineRate = [double] $report.coverage.'line-rate' * 100
$branchRate = [double] $report.coverage.'branch-rate' * 100

Write-Host ''
Write-Host ('Lines    {0,6:N2}%  ({1}/{2})  threshold {3:N2}%' -f $lineRate, $report.coverage.'lines-covered', $report.coverage.'lines-valid', $LineThreshold)
Write-Host ('Branches {0,6:N2}%  ({1}/{2})  threshold {3:N2}%' -f $branchRate, $report.coverage.'branches-covered', $report.coverage.'branches-valid', $BranchThreshold)
Write-Host ''

$failures = @()

if ($lineRate -lt $LineThreshold) {
    $failures += ('Line coverage {0:N2}% is below the {1:N2}% floor.' -f $lineRate, $LineThreshold)
}

if ($branchRate -lt $BranchThreshold) {
    $failures += ('Branch coverage {0:N2}% is below the {1:N2}% floor.' -f $branchRate, $BranchThreshold)
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host 'Coverage thresholds met.' -ForegroundColor Green
