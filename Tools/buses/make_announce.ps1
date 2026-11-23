# Bus stop announcements with the Windows Hindi voice (24 Nov 2026; the hi-IN speech pack: Microsoft Kalpana / Hemant,
# OneCore voices - only the WinRT synthesizer sees them, not System.Speech). Reads Tools\buses\announce.json, writes
# Assets\Resources\Audio\announce\<slug>_next.ogg / _here.ogg (a bus PA: band-limited, a short echo) and
# Assets\Resources\Audio\announce_chime.ogg (ding-dong). Needs ffmpeg on PATH or the winget one.
#   powershell -ExecutionPolicy Bypass -File Tools\buses\make_announce.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$cfg = Get-Content (Join-Path $PSScriptRoot "announce.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$out = Join-Path $root "Assets\Resources\Audio\announce"
New-Item -ItemType Directory -Force $out | Out-Null
$tmp = Join-Path $env:TEMP "nt_announce"
New-Item -ItemType Directory -Force $tmp | Out-Null
$ff = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (-not $ff) { $ff = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if (-not $ff) { throw "ffmpeg not found" }

# WinRT speech
[void][Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
[void][Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, [Type]$t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(-1) | Out-Null; $task.Result }

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -like "*$($cfg.voice)*" } | Select-Object -First 1
if (-not $voice) { throw "voice $($cfg.voice) not installed (Settings > Time & language > Speech: Hindi)" }
$synth.Voice = $voice
$synth.Options.SpeakingRate = 0.92
Write-Output "voice: $($voice.DisplayName) ($($voice.Language))"

function Say([string]$text, [string]$file) {
    $stream = Await ($synth.SynthesizeTextToStreamAsync($text)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $size = [uint32]$stream.Size
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    Await ($reader.LoadAsync($size)) ([uint32]) | Out-Null
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    [System.IO.File]::WriteAllBytes($file, $bytes)
}

$n = 0
foreach ($s in $cfg.stops) {
    $slug = $s[0]; $name = $s[1]
    foreach ($kind in @("next", "here")) {
        $text = [string]::Format($cfg.$kind, $name)
        $wav = Join-Path $tmp "$slug`_$kind.wav"
        Say $text $wav
        $ogg = Join-Path $out "$slug`_$kind.ogg"
        & $ff -v error -y -i $wav -af "highpass=f=280,lowpass=f=4200,aecho=0.8:0.45:35:0.22,volume=4.8,alimiter=limit=0.95:attack=5:release=60" -ac 1 -ar 44100 -c:a libvorbis -q:a 4 $ogg
        $n++
    }
}
# the chime: two soft bell tones, high then low
$chime = Join-Path $root "Assets\Resources\Audio\announce_chime.ogg"
& $ff -v error -y -f lavfi -i "sine=f=880:d=0.55" -f lavfi -i "sine=f=698:d=0.8" -filter_complex "[0]afade=t=out:st=0.05:d=0.5,volume=0.5[a];[1]afade=t=out:st=0.05:d=0.75,volume=0.5,adelay=420|420[b];[a][b]amix=inputs=2:duration=longest:normalize=0,aecho=0.8:0.5:60:0.3" -ac 1 -ar 44100 -c:a libvorbis -q:a 4 $chime
Write-Output "announcements: $n clips + chime -> $out"
