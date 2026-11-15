# Full iPhone build, unattended: Unity batch iOS Xcode project -> zip -> GitHub release upload, macOS workflow, .ipa
# download (Tools\pipeline\ios_ipa_chain.sh). Everything at BelowNormal. Progress lines go to Logs\make_ipa.log.
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path   # the project folder (this file is in Tools\pipeline)
New-Item -ItemType Directory -Force (Join-Path $root 'Logs') | Out-Null
$log = "$root\Logs\make_ipa.log"
function Say($s) { "$(Get-Date -Format HH:mm:ss) $s" | Out-File $log -Append -Encoding utf8 }
"" | Out-File $log -Encoding utf8
Remove-Item Env:ELECTRON_RUN_AS_NODE -ErrorAction SilentlyContinue

# 1. the Xcode project
Say "iOS build starting"
$ulog = "$root\Logs\ios_build.log"
Remove-Item $ulog -ErrorAction SilentlyContinue
$cmd = '"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe" -batchmode -quit -projectPath "' + $root + '" -buildTarget iOS -executeMethod Nainital.EditorTools.IOSBuild.Build -logFile "' + $ulog + '"'
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine = $cmd; CurrentDirectory = $root}
Start-Sleep 3; try { (Get-Process -Id $r.ProcessId).PriorityClass = 'BelowNormal' } catch {}
while (Get-Process -Id $r.ProcessId -ErrorAction SilentlyContinue) { Start-Sleep 10 }
$res = Select-String -Path $ulog -Pattern '\[Nainital\] iOS build' | Select-Object -Last 1
Say ("iOS build: " + $(if ($res) { $res.Line } else { 'NO RESULT LINE' }))
if (-not $res -or $res.Line -notmatch 'Succeeded') { Say "STOP: iOS build failed (see Logs\ios_build.log)"; exit 1 }

# 2. the zip (the project's files at the zip's root, as the workflow expects)
$zip = "$root\Builds\NainitalGame-iOS-Xcode.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Say "zipping"
$z = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine = ('C:\Windows\System32\tar.exe -a -c -f "' + $zip + '" -C "' + $root + '\Builds\iOS" .'); CurrentDirectory = "$root\Builds"}
Start-Sleep 3; try { (Get-Process -Id $z.ProcessId).PriorityClass = 'BelowNormal' } catch {}
while (Get-Process -Id $z.ProcessId -ErrorAction SilentlyContinue) { Start-Sleep 10 }
Say ("zip done: {0:N0} MB" -f ((Get-Item $zip).Length / 1MB))

# 3. upload, workflow on GitHub's Mac, download (the chain waits on a pid; the zip is finished, so any dead pid)
$chain = Join-Path $PSScriptRoot 'ios_ipa_chain.sh'
Say "upload + workflow + download"
& 'C:\Program Files\Git\bin\bash.exe' $chain 1 2>&1 | ForEach-Object { Say $_ }
Say "FINISHED"
