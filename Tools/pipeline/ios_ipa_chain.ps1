# After the Xcode zip is made (make_ipa.ps1): upload it to the "ios-build" release, run the "iOS IPA" workflow on
# GitHub's Mac, wait, download NainiTA-unsigned.ipa and fix its frameworks for Sideloadly. The PowerShell twin of
# ios_ipa_chain.sh, for a PC without Git Bash; uses the GitHub CLI (gh), signed in as gamedevayush.
param([string]$Log)
$ErrorActionPreference = 'Continue'
$R = 'gamedevayush/NainiTA---The-Sukoon'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$B = "$root\Builds"
$Z = "$B\NainitalGame-iOS-Xcode.zip"
$OUT = "$B\NainiTA-unsigned.ipa"
function Say($s) { $line = "$(Get-Date -Format HH:mm:ss) $s"; if ($Log) { $line | Out-File $Log -Append -Encoding utf8 } else { $line } }

if (-not (Test-Path $Z)) { Say "NO ZIP - stopping"; exit 1 }
Say ("zip: {0:N0} MB" -f ((Get-Item $Z).Length / 1MB))

# the old assets go, so the IPA fetched at the end is surely the new one
foreach ($a in 'NainitalGame-iOS-Xcode.zip', 'NainiTA-unsigned.ipa') { gh release delete-asset ios-build $a -R $R -y 2>$null | Out-Null }
Say "uploading"
gh release upload ios-build $Z -R $R --clobber
if ($LASTEXITCODE -ne 0) { Say "UPLOAD FAILED - stopping"; exit 1 }
Say "uploaded"

$t0 = (Get-Date).ToUniversalTime()
gh workflow run ios-ipa.yml -R $R --ref main -f tag=ios-build
if ($LASTEXITCODE -ne 0) { Say "DISPATCH FAILED - stopping"; exit 1 }
$run = $null
for ($i = 0; $i -lt 30 -and -not $run; $i++) {
    Start-Sleep 10
    $runs = gh run list -R $R --workflow ios-ipa.yml -L 5 --json databaseId,createdAt | ConvertFrom-Json
    foreach ($wr in $runs) { if ([datetime]$wr.createdAt -ge $t0.AddSeconds(-30)) { $run = $wr.databaseId; break } }   # not $r: PowerShell names ignore case, $r is $R
}
if (-not $run) { Say "RUN NOT FOUND - stopping"; exit 1 }
Say "run $run : https://github.com/$R/actions/runs/$run"

$last = ''
for ($i = 0; $i -lt 400; $i++) {
    $st = gh run view $run -R $R --json status,conclusion | ConvertFrom-Json
    $s = "$($st.status) $($st.conclusion)"
    if ($s -ne $last) { Say "run: $s"; $last = $s }
    if ($st.status -eq 'completed') { break }
    Start-Sleep 45
}
if ($last -ne 'completed success') { Say "RUN DID NOT SUCCEED: $last"; exit 1 }

Remove-Item $OUT -ErrorAction SilentlyContinue
gh release download ios-build -R $R -p NainiTA-unsigned.ipa -D $B --clobber
if (-not (Test-Path $OUT)) { Say "IPA NOT DOWNLOADED"; exit 1 }
Say ("IPA: $OUT  {0:N0} MB" -f ((Get-Item $OUT).Length / 1MB))
# Unity's pre-signed UnityRuntime.framework fails on the phone after Sideloadly (0xe8008001): fix_ipa_frameworks.py
python "$PSScriptRoot\fix_ipa_frameworks.py" $OUT 2>&1 | ForEach-Object { Say $_ }
if ($LASTEXITCODE -ne 0) { Say "FRAMEWORK FIX FAILED"; exit 1 }
Say "ALL DONE"
