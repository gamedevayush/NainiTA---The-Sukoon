# Synthesises every catalogue line to WAV with Windows (OneCore) text-to-speech voices.
# Hindi voices (Kalpana / Hemant) read the Devanagari text; if they are not installed, the Indian-English
# voices (Heera / Ravi) read the Hinglish text instead. English uses Heera / Ravi.
# Usage: powershell -File gen_voices.ps1 <catalogue.json> <out_dir>
param([string]$Catalogue, [string]$OutDir)

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]

$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]

function Await($op, [Type]$type) {
    $t = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
    $t.Wait() | Out-Null
    return $t.Result
}

$all = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
function FindVoice([string]$name) { $all | Where-Object { $_.DisplayName -like "*$name*" } | Select-Object -First 1 }
$vHiF = FindVoice "Kalpana"; $vHiM = FindVoice "Hemant"
$vEnF = FindVoice "Heera";   $vEnM = FindVoice "Ravi"
if (-not $vEnF) { $vEnF = FindVoice "Zira" }
if (-not $vEnM) { $vEnM = FindVoice "David" }
$hindiOk = [bool]($vHiF -or $vHiM)
if ($vHiF -and -not $vHiM) { $vHiM = $vHiF }
if ($vHiM -and -not $vHiF) { $vHiF = $vHiM }
Write-Output ("Hindi voices: " + $(if ($hindiOk) { "$($vHiF.DisplayName) / $($vHiM.DisplayName)" } else { "not installed - using Indian English voices for Hinglish" }))
Write-Output "English voices: $($vEnF.DisplayName) / $($vEnM.DisplayName)"

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
New-Item -ItemType Directory -Force $OutDir | Out-Null
$lines = Get-Content -Raw -Encoding UTF8 $Catalogue | ConvertFrom-Json

function Speak($voice, [string]$text, [string]$path, [double]$rate) {
    if (Test-Path $path) { return }
    $synth.Voice = $voice
    $synth.Options.SpeakingRate = $rate
    $stream = Await ($synth.SynthesizeTextToStreamAsync($text)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $size = [uint32]$stream.Size
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $null = Await ($reader.LoadAsync($size)) ([uint32])
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    [System.IO.File]::WriteAllBytes($path, $bytes)
}

$n = 0
foreach ($l in $lines) {
    $hiText = if ($hindiOk) { $l.hindi } else { $l.hinglish }
    $k = $l.key
    switch ($l.kind) {
        "npc" {
            Speak $vHiM $hiText (Join-Path $OutDir "$($k)_hm.wav") 1.05
            Speak $vHiF $hiText (Join-Path $OutDir "$($k)_hf.wav") 1.05
            Speak $vEnM $l.english (Join-Path $OutDir "$($k)_em.wav") 1.0
            Speak $vEnF $l.english (Join-Path $OutDir "$($k)_ef.wav") 1.0
        }
        "narrator" {
            Speak $vHiF $hiText (Join-Path $OutDir "$($k)_hf.wav") 1.0
            Speak $vEnF $l.english (Join-Path $OutDir "$($k)_ef.wav") 1.0
        }
        "intro" {
            Speak $vHiM $hiText (Join-Path $OutDir "$($k)_hm.wav") 1.0
            Speak $vEnM $l.english (Join-Path $OutDir "$($k)_em.wav") 1.0
        }
    }
    $n++
}
Write-Output "voiced $n lines -> $OutDir"
