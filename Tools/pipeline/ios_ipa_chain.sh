#!/usr/bin/env bash
# After the Xcode zip is made: upload it to the ios-build release, run the "iOS IPA" workflow on GitHub's Mac, wait,
# download NainiTA-unsigned.ipa. The token comes from Windows Credential Manager at run time and is never written.
set -u
export GCM_INTERACTIVE=never GIT_TERMINAL_PROMPT=0
R=gamedevayush/NainiTA---The-Sukoon
REL=442570853
B="$(cd "$(dirname "$0")/../.." && pwd)/Builds"   # the project's Builds folder (this file is in Tools/pipeline)
Z=$B/NainitalGame-iOS-Xcode.zip
OUT=$B/NainiTA-unsigned.ipa
TAR_PID=${1:-1}   # make_ipa.ps1 passes a finished pid: the zip is done

while tasklist //FI "PID eq $TAR_PID" | grep -q "$TAR_PID"; do sleep 15; done
echo "zip done: $(du -m "$Z" | cut -f1) MB"
python -c "
import zipfile,sys
z=zipfile.ZipFile(sys.argv[1])
n=z.namelist()
ok=any(x.lstrip('./').startswith('Unity-iPhone.xcodeproj/project.pbxproj') for x in n)
print('zip entries', len(n), 'xcodeproj', ok)
sys.exit(0 if ok else 1)
" "$(cygpath -w "$Z")" || { echo "ZIP BAD - stopping"; exit 1; }

TOK=$(printf 'protocol=https\nhost=github.com\nusername=gamedevayush\n\n' | git credential fill 2>/dev/null | sed -n 's/^password=//p')
[ -n "$TOK" ] || { echo "NO TOKEN - stopping"; exit 1; }
H=(-H "Authorization: token $TOK" -H "Accept: application/vnd.github+json" -H "User-Agent: nainita-ios-build")

# the 12 Nov assets go, so the IPA fetched at the end is surely the new one
for id in $(curl -s "${H[@]}" "https://api.github.com/repos/$R/releases/$REL/assets" | python -c "import json,sys; [print(a['id']) for a in json.load(sys.stdin) if a['name'] in ('NainitalGame-iOS-Xcode.zip','NainiTA-unsigned.ipa')]"); do
  curl -s -o /dev/null -w "deleted old asset $id: %{http_code}\n" -X DELETE "${H[@]}" "https://api.github.com/repos/$R/releases/assets/$id"
done

echo "uploading $(date +%H:%M:%S)"
code=$(curl -s -X POST -T "$Z" "${H[@]}" -H "Content-Type: application/zip" \
  "https://uploads.github.com/repos/$R/releases/$REL/assets?name=NainitalGame-iOS-Xcode.zip" \
  -o /dev/null -w "%{http_code} %{time_total}")
echo "upload: http $code s  $(date +%H:%M:%S)"
[ "${code%% *}" = "201" ] || { echo "UPLOAD FAILED - stopping"; exit 1; }

T0=$(date -u +%Y-%m-%dT%H:%M:%SZ)
curl -s -o /dev/null -w "workflow dispatched: %{http_code}\n" -X POST "${H[@]}" \
  "https://api.github.com/repos/$R/actions/workflows/ios-ipa.yml/dispatches" -d '{"ref":"main","inputs":{"tag":"ios-build"}}'

RUN=""
for i in $(seq 1 20); do
  sleep 10
  RUN=$(curl -s "${H[@]}" "https://api.github.com/repos/$R/actions/workflows/ios-ipa.yml/runs?per_page=5&event=workflow_dispatch" | python -c "
import json,sys
for r in json.load(sys.stdin).get('workflow_runs',[]):
    if r['created_at'] >= '$T0': print(r['id']); break")
  [ -n "$RUN" ] && break
done
[ -n "$RUN" ] || { echo "RUN NOT FOUND - stopping"; exit 1; }
echo "run $RUN: https://github.com/$R/actions/runs/$RUN"

last=""
for i in $(seq 1 120); do
  st=$(curl -s "${H[@]}" "https://api.github.com/repos/$R/actions/runs/$RUN" | python -c "import json,sys; d=json.load(sys.stdin); print(d.get('status'), d.get('conclusion'))")
  [ "$st" != "$last" ] && { echo "$(date +%H:%M:%S) run: $st"; last=$st; }
  case "$st" in completed*) break;; esac
  sleep 45
done
case "$st" in
  "completed success") ;;
  *) echo "RUN DID NOT SUCCEED: $st"
     curl -s "${H[@]}" "https://api.github.com/repos/$R/actions/runs/$RUN/jobs" | python -c "
import json,sys
for j in json.load(sys.stdin).get('jobs',[]):
    for s in j.get('steps',[]):
        if s.get('conclusion') not in ('success','skipped',None): print('  failed step:', s['name'], s['conclusion'])"
     exit 1;;
esac

AID=$(curl -s "${H[@]}" "https://api.github.com/repos/$R/releases/$REL/assets" | python -c "import json,sys; [print(a['id']) for a in json.load(sys.stdin) if a['name']=='NainiTA-unsigned.ipa']")
[ -n "$AID" ] || { echo "IPA NOT ON THE RELEASE"; exit 1; }
curl -s -L -H "Authorization: token $TOK" -H "Accept: application/octet-stream" -H "User-Agent: nainita-ios-build" \
  "https://api.github.com/repos/$R/releases/assets/$AID" -o "$OUT"
echo "IPA: $OUT  $(du -m "$OUT" | cut -f1) MB"
python -c "
import zipfile,sys
z=zipfile.ZipFile(sys.argv[1])
apps=[n for n in z.namelist() if n.startswith('Payload/') and n.endswith('.app/Info.plist')]
print('IPA ok:', apps[:1])
" "$(cygpath -w "$OUT")"
# Unity's pre-signed UnityRuntime.framework fails on the phone after Sideloadly (0xe8008001): make it like an
# unsigned Xcode build (fix_ipa_frameworks.py)
python "$(cygpath -w "$(dirname "$0")/fix_ipa_frameworks.py")" "$(cygpath -w "$OUT")" || { echo "FRAMEWORK FIX FAILED"; exit 1; }
echo "ALL DONE"
