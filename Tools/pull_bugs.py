import sys, os, asyncio, inspect
from pymobiledevice3.lockdown import create_using_usbmux
from pymobiledevice3.services.house_arrest import HouseArrestService

BUNDLE = "com.nainital.openworld.H53YSDBJ3A"
DEST = sys.argv[1]

async def aw(x):
    return await x if inspect.isawaitable(x) else x

async def main():
    ld = await aw(create_using_usbmux())
    ha = await HouseArrestService.create(ld, BUNDLE, documents_only=True)
    root = "/Documents/BugReports"
    names = await aw(ha.listdir(root))
    names = [n for n in names if n not in (".", "..")]
    print("on phone:", len(names))
    new = 0
    for n in sorted(names):
        local = os.path.join(DEST, n)
        if os.path.isdir(local) and os.listdir(local):
            continue
        await aw(ha.pull(f"{root}/{n}", DEST))
        print("pulled", n); new += 1
    print("new:", new)

asyncio.run(main())
