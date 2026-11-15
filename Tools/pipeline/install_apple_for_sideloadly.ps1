# What Sideloadly needs on Windows to see the iPhone, installed once. Run it elevated (it asks for UAC itself).
#  - iTunes and iCloud, the web versions (not the Microsoft Store ones), and Sideloadly (winget).
#  - Apple Mobile Device Support: the service and USB driver that let a program talk to the phone. The winget iTunes
#    left it out on 16 Nov 2026 and Sideloadly said "no Apple devices detected" with the phone plugged in; it is taken
#    from Apple's own iTunes installer (signature checked) and installed on its own.
# Then: unplug and replug the phone, unlock it, tap "Trust This Computer", restart Sideloadly.
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    return
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$log = Join-Path $root 'Logs\apple_installs.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
function Say($s) { "$(Get-Date -Format HH:mm:ss) $s" | Tee-Object -FilePath $log -Append }

foreach ($id in 'Apple.iTunes', 'Apple.iCloud', 'iOSGods.Sideloadly') {
    Say "== $id"
    winget install --id $id -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity 2>&1 |
        Where-Object { $_ -match '\w' -and $_ -notmatch '^\s*[-\\|/]\s*$' } | Select-Object -Last 3 | ForEach-Object { Say $_ }
}

if (-not (Get-Service -Name 'Apple Mobile Device Service' -ErrorAction SilentlyContinue)) {
    $dir = Join-Path $root 'Builds\apple_itunes'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $exe = Join-Path $dir 'iTunes64Setup.exe'
    if (-not (Test-Path $exe)) {
        Say "downloading Apple's iTunes installer"
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri 'https://www.apple.com/itunes/download/win64' -OutFile $exe -UseBasicParsing
    }
    $sig = Get-AuthenticodeSignature $exe
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Apple Inc\.') { Say "STOP: installer signature is not Apple's"; return }
    $sevenZip = 'C:\Program Files\7-Zip\7z.exe'
    if (-not (Test-Path $sevenZip)) { Say "STOP: 7-Zip is needed to unpack the installer (winget install 7zip.7zip)"; return }
    & $sevenZip x $exe "-o$dir\x" -y | Out-Null
    $msi = Join-Path $dir 'x\AppleMobileDeviceSupport64.msi'
    Say "installing Apple Mobile Device Support"
    $p = Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /passive /norestart" -Wait -PassThru
    Say "msiexec exit $($p.ExitCode)"
}
$svc = Get-Service -Name 'Apple Mobile Device Service' -ErrorAction SilentlyContinue
Say ("Apple Mobile Device Service: " + $(if ($svc) { $svc.Status } else { 'MISSING' }))
