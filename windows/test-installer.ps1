$ErrorActionPreference = 'Stop'
$msi = (Resolve-Path 'dist/Moving-Background-Windows-x64.msi').Path
$installFolder = Join-Path $env:LOCALAPPDATA 'Moving Background'
$exe = Join-Path $installFolder 'MovingBackground.exe'
$startMenu = [Environment]::GetFolderPath('Programs')
$shortcut = Join-Path $startMenu 'Moving Background\Moving Background.lnk'

function Invoke-Installer([string] $Operation) {
    $log = Join-Path $env:RUNNER_TEMP "moving-background-$Operation.log"
    $result = Start-Process "$env:SystemRoot\System32\msiexec.exe" -Wait -PassThru -ArgumentList @(
        "/$Operation", ('"{0}"' -f $msi), '/qn', '/norestart', '/L*v', ('"{0}"' -f $log)
    )
    if ($result.ExitCode -ne 0) {
        Get-Content -LiteralPath $log -Tail 100
        throw "Windows Installer exited with $($result.ExitCode)."
    }
}

Invoke-Installer 'i'
try {
    if (!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $shortcut)) {
        throw 'The application or its Start menu shortcut was not installed.'
    }
    if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -notlike '1.4.2*') {
        throw 'The installed executable is not the expected build.'
    }
    $app = Start-Process -FilePath $exe -PassThru
    try {
        if ($app.WaitForExit(5000)) {
            throw "The installed application exited during startup (code $($app.ExitCode))."
        }
    } finally {
        if (!$app.HasExited) {
            Stop-Process -Id $app.Id
            $app.WaitForExit()
        }
    }
} finally {
    Invoke-Installer 'x'
}
if ((Test-Path -LiteralPath $exe) -or (Test-Path -LiteralPath $shortcut)) {
    throw 'Uninstall left the executable or Start menu shortcut behind.'
}
