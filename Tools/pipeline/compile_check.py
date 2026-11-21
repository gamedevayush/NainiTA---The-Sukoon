"""Compile check without touching the running Unity editor (22 Nov 2026: several agents write code at once; only one may
drive the editor). Uses Unity's own Roslyn (csc) with the references and defines of the last editor compile
(Library/Bee/artifacts/*/Assembly-CSharp*.rsp) on the CURRENT .cs files: Assembly-CSharp first (into a temp folder),
then Assembly-CSharp-Editor against it. Prints the errors (warnings with --warn); exit 0 = both compile.

  python Tools/pipeline/compile_check.py [--warn] [--only PATH_FRAGMENT]

--only keeps just the error lines whose file path contains the fragment (an agent checking its own files).
"""
import glob, os, re, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data"
DOTNET = os.path.join(UNITY, "NetCoreRuntime", "dotnet.exe")
CSC = os.path.join(UNITY, "DotNetSdkRoslyn", "csc.dll")


def newest_rsp(name):
    c = glob.glob(os.path.join(ROOT, "Library", "Bee", "artifacts", "*", name))
    if not c:
        sys.exit("no " + name + " under Library/Bee (open the project in Unity once)")
    return max(c, key=os.path.getmtime)


def sources(editor):
    out = []
    for p in glob.glob(os.path.join(ROOT, "Assets", "**", "*.cs"), recursive=True):
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        parts = rel.split("/")
        is_editor = "Editor" in parts
        if is_editor != editor:
            continue
        # code under a folder with its own .asmdef is another assembly
        d, own = os.path.dirname(p), False
        while len(d) >= len(ROOT):
            if glob.glob(os.path.join(d, "*.asmdef")):
                own = True
                break
            nd = os.path.dirname(d)
            if nd == d:
                break
            d = nd
        if not own:
            out.append(rel)
    return out


def build(rsp_name, editor, tmp, runtime_dll=None):
    lines = open(newest_rsp(rsp_name), encoding="utf-8-sig").read().splitlines()
    keep = []
    for ln in lines:
        s = ln.strip()
        if not s or s.endswith('.cs"') or s.endswith(".cs"):
            continue
        if s.startswith("-out:") or s.startswith("-refout:") or s.startswith("-generatedfilesout") or s.startswith("-pathmap"):
            continue
        if runtime_dll and s.startswith("-r:") and re.search(r'Assembly-CSharp(\.ref)?\.dll"?$', s):   # (the editor references the runtime's .ref.dll)
            s = f'-r:"{runtime_dll}"'
        keep.append(s)
    out = os.path.join(tmp, "Assembly-CSharp-Editor.dll" if editor else "Assembly-CSharp.dll")
    keep.append(f'-out:"{out}"')
    keep += [f'"{s}"' for s in sources(editor)]
    rsp = os.path.join(tmp, ("editor" if editor else "runtime") + ".rsp")
    open(rsp, "w", encoding="utf-8").write("\n".join(keep))
    r = subprocess.run([DOTNET, CSC, "/noconfig", "@" + rsp], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.returncode, r.stdout + r.stderr, out


def main():
    warn = "--warn" in sys.argv
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
    tmp = tempfile.mkdtemp(prefix="ntcheck_")
    ok = True
    code, log, dll = build("Assembly-CSharp.rsp", False, tmp)
    for name, (c, lg) in (("runtime", (code, log)),):
        pass
    results = [("runtime", code, log)]
    if code == 0:
        code2, log2, _ = build("Assembly-CSharp-Editor.rsp", True, tmp, dll)
        results.append(("editor", code2, log2))
    else:
        results.append(("editor", -1, "(skipped: the runtime assembly does not compile)\n"))
    for name, c, lg in results:
        lines = [l for l in lg.splitlines() if (": error " in l) or (warn and ": warning " in l)]
        if only:
            lines = [l for l in lines if only.replace("/", "\\") in l or only in l]
        print(f"[{name}] {'OK' if c == 0 else ('FAILED' if c > 0 else 'skipped')}  ({len(lines)} lines shown)")
        for l in lines[:80]:
            print("  " + l.strip())
        if c != 0:
            ok = False
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
