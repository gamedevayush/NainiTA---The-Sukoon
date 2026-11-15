"""Make every framework in an unsigned IPA look like one Xcode built with signing off, so Sideloadly signs it.

Unity 6000.4 ships UnityRuntime.framework already signed by Unity (a _CodeSignature folder) and as a FAT binary with
one arm64 slice. Sideloadly left it with a signature the iPhone rejects: "ApplicationVerificationFailed ... Failed to
verify code signature of .../UnityRuntime.framework : 0xe8008001" and on the phone "integrity could not be verified"
(16 Nov 2026). UnityFramework.framework, built by Xcode in the workflow, has no _CodeSignature folder and a thin
binary, and was signed fine. So for each framework with a _CodeSignature folder: drop the folder, thin a FAT binary to
its arm64 slice, and mark the bundle iPhoneOS in its Info.plist.

Usage: python Tools/pipeline/fix_ipa_frameworks.py Builds/NainiTA-unsigned.ipa   (rewrites it in place)
"""
import os
import plistlib
import struct
import sys
import zipfile

CPU_ARM64 = 0x0100000C


def thin_arm64(data):
    """The arm64 slice of a FAT Mach-O, or the data unchanged when it is already thin."""
    if data[:4] != b"\xca\xfe\xba\xbe":
        return data, False
    n = struct.unpack_from(">I", data, 4)[0]
    for k in range(n):
        cpu, sub, off, size, align = struct.unpack_from(">iiIII", data, 8 + 20 * k)
        if cpu == CPU_ARM64:
            return data[off:off + size], True
    raise SystemExit("FAT binary without an arm64 slice")


def main():
    ipa = sys.argv[1]
    src = zipfile.ZipFile(ipa)
    names = src.namelist()
    signed = set()
    for n in names:
        parts = n.split("/")
        if "_CodeSignature" in parts and "Frameworks" in parts:
            i = parts.index("Frameworks")
            if i + 1 < len(parts) and parts[i + 1].endswith(".framework"):
                signed.add("/".join(parts[:i + 2]) + "/")
    if not signed:
        print("no pre-signed frameworks: nothing to do")
        return
    tmp = ipa + ".tmp"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, compresslevel=5) as out:
        for info in src.infolist():
            n = info.filename
            fw = next((f for f in signed if n.startswith(f)), None)
            if fw is None:
                with src.open(info) as fsrc, out.open(info, "w", force_zip64=True) as fdst:
                    while True:
                        chunk = fsrc.read(1 << 22)
                        if not chunk:
                            break
                        fdst.write(chunk)
                continue
            rel = n[len(fw):]
            if rel.startswith("_CodeSignature"):
                print("  dropped", n)
                continue
            data = src.read(info)
            exe = os.path.basename(fw.rstrip("/"))[:-len(".framework")]
            if rel == exe:
                data, was_fat = thin_arm64(data)
                print(f"  {n}: {'thinned to arm64' if was_fat else 'already thin'}, {len(data)} bytes")
            elif rel == "Info.plist":
                pl = plistlib.loads(data)
                if not pl.get("CFBundleSupportedPlatforms"):
                    pl["CFBundleSupportedPlatforms"] = ["iPhoneOS"]
                    data = plistlib.dumps(pl, fmt=plistlib.FMT_BINARY)
                    print("  ", n, "marked iPhoneOS")
            out.writestr(info, data)
    src.close()
    os.replace(tmp, ipa)
    print("fixed:", ", ".join(sorted(signed)))


if __name__ == "__main__":
    main()
