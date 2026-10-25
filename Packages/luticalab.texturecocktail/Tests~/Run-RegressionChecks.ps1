<#
Run against a closed TextureCocktail project with the matching Unity Editor version:
  .\Packages\luticalab.texturecocktail\Tests~\Run-RegressionChecks.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe'
The graphical batch editor is required for pixel/readback checks. Other projects may remain open.
Fixtures are excluded from regular package import and copied only for this run.
#>
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [string]$ProjectPath
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($ProjectPath)) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
}
$fixtureFolder = Join-Path $ProjectPath ('Assets/TextureCocktailChecks_' + [guid]::NewGuid().ToString('N'))
$editorFolder = Join-Path $fixtureFolder 'Editor'
$logPath = Join-Path $ProjectPath 'Logs/TextureCocktailRegressionChecks.log'
try {
    New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path $logPath) -Force | Out-Null
    Get-ChildItem -Path $PSScriptRoot -File | Where-Object { $_.Extension -in '.cs', '.shader' } |
        Copy-Item -Destination $editorFolder
    $arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $ProjectPath),
        '-executeMethod', 'TextureCocktailRegressionChecks.Run', '-quit', '-logFile', ('"{0}"' -f $logPath))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru
    # Wait only for this Editor; Unity may leave shared licensing/compiler children alive.
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        if (Test-Path $logPath) { Get-Content $logPath -Tail 100 }
        throw "Unity regression checks failed (exit $($process.ExitCode)). Log: $logPath"
    }
    $success = Select-String -Path $logPath -Pattern 'TEXTURE_COCKTAIL_CHECKS_PASSED'
    if (-not $success) { throw "Unity exited without a successful test result. Log: $logPath" }
    $success | ForEach-Object { Write-Output $_.Line }
    Write-Output "Unity log: $logPath"
} finally {
    if (Test-Path $fixtureFolder) { Remove-Item -LiteralPath $fixtureFolder -Recurse -Force }
    if (Test-Path ($fixtureFolder + '.meta')) { Remove-Item -LiteralPath ($fixtureFolder + '.meta') -Force }
}
