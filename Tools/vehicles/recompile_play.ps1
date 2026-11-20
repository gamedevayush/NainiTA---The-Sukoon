# Leave Play, compile the changed scripts, show compile errors (if any), enter Play again and wait for it.
$P = Split-Path -Parent $MyInvocation.MyCommand.Path
$L = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
& "$P\bridge.ps1" "call VehicleSetupShots.Stop"
Start-Sleep 3
$errBase = (Select-String -Path $L -Pattern "error CS").Count
& "$P\bridge.ps1" "refresh" -WaitSec 240
Start-Sleep 2
$t = 0
while ($t -lt 180) { $st = Get-Content (Join-Path (Resolve-Path "$PSScriptRoot\..\..\Bridge").Path "status.txt"); if ($st -match "compiling=False") { break }; Start-Sleep 1; $t++ }
$st
$errs = Select-String -Path $L -Pattern "error CS"
if ($errs.Count -gt $errBase) { "COMPILE ERRORS:"; $errs | Select-Object -Last ($errs.Count - $errBase) | ForEach-Object Line; exit 1 }
& "$P\bridge.ps1" "call VehicleSetupShots.Play"
& "$P\waitlog.ps1" "Entering Playmode" 1 90
Start-Sleep 8

