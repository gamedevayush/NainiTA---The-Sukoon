# Runs Unity once in batch mode at BelowNormal priority for the steps in Bridge\batch.txt, waits, and prints the
# [Batch] / [Probe] / [Nainital] lines and any errors from the log. Usage: powershell -File Tools\pipeline\run_photobatch.ps1
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path   # the project folder (this file is in Tools\pipeline)
New-Item -ItemType Directory -Force (Join-Path $root 'Logs') | Out-Null
$log = Join-Path $root 'Logs\photobatch.log'
Remove-Item $log -ErrorAction SilentlyContinue
Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue
$cmd = '"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -quit -projectPath "' + $root + '" -executeMethod Nainital.EditorTools.PhotoBatch.Run -logFile "' + $log + '"'
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine = $cmd; CurrentDirectory = $root}
$id = $r.ProcessId
Start-Sleep 3
try { (Get-Process -Id $id).PriorityClass = 'BelowNormal' } catch {}
$t0 = Get-Date
while (Get-Process -Id $id -ErrorAction SilentlyContinue) {
    # the main process only: its import workers start at Normal and are left there (never raise them)
    Start-Sleep 5
    if (((Get-Date) - $t0).TotalMinutes -gt 40) { "TIMEOUT - stopping"; Stop-Process -Id $id -Force; break }
}
"unity exited after {0:N1} min" -f ((Get-Date) - $t0).TotalMinutes
if (Test-Path $log) {
    Select-String -Path $log -Pattern '\[Batch\]|\[Probe\]|hillside|WORLD BUILT|error CS|Exception:|Shader error' |
        ForEach-Object { $_.Line.Substring(0, [Math]::Min(260, $_.Line.Length)) }
    $p = Select-String -Path $log -Pattern '^\[Probe\]' -Context 0, 14 | Select-Object -First 1
    if ($p) { $p.Context.PostContext | ForEach-Object { $_.Substring(0, [Math]::Min(200, $_.Length)) } }
}
