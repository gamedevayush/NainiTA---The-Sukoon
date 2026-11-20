param([string]$Pattern, [int]$Count = 1, [int]$WaitSec = 300)
# Waits until the Editor.log has at least $Count lines matching $Pattern (counted from this call's start).
$L = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
$base = (Select-String -Path $L -Pattern $Pattern -SimpleMatch).Count
$t = 0
while ($t -lt $WaitSec) {
  $n = (Select-String -Path $L -Pattern $Pattern -SimpleMatch).Count - $base
  if ($n -ge $Count) { "seen '$Pattern' x$n after $t s"; exit 0 }
  Start-Sleep 1; $t++
}
"TIMEOUT waiting for '$Pattern'"
