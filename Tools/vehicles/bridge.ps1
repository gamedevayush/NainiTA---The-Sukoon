param([string]$Cmd, [string]$Arg = $null, [int]$WaitSec = 120)
# One bridge command: writes arg.txt (if given) and cmd.txt with a fresh id, waits for result_<id>.txt, prints it.
$B = (Resolve-Path "$PSScriptRoot\..\..\Bridge").Path
if ($PSBoundParameters.ContainsKey('Arg')) { Set-Content "$B\arg.txt" $Arg -Encoding ascii }
$id = "k" + [DateTime]::Now.ToString("HHmmssfff")
Set-Content "$B\cmd.txt" "$id $Cmd" -Encoding ascii
$t = 0
while (-not (Test-Path "$B\result_$id.txt") -and $t -lt ($WaitSec * 2)) { Start-Sleep -Milliseconds 500; $t++ }
if (Test-Path "$B\result_$id.txt") { "[$Cmd] " + ((Get-Content "$B\result_$id.txt") -join " ") } else { "[$Cmd] no result after $WaitSec s" }

