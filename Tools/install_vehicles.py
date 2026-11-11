"""Copy the processed vehicles (make_vehicle_lods.py output) into the Unity project, phone-sized.

python -I install_vehicles.py <Processed/vehicles> <Unity Assets/Nainital/Vehicles>

- FBX and JSON go to <dst>/Models.
- Textures go to <dst>/Textures at no more than 1024 px.
- Each ORM map (R occlusion, G roughness, B metal) becomes a URP mask "<name>_ms.png" (R metal, G occlusion, A smoothness);
  the JSON gets an "ms" entry for it.
"""
import json, os, shutil, sys
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
models, texs = os.path.join(dst, "Models"), os.path.join(dst, "Textures")
os.makedirs(models, exist_ok=True)
os.makedirs(texs, exist_ok=True)
MAX = 1024


def fit(im):
    if max(im.size) > MAX:
        k = MAX / max(im.size)
        im = im.resize((max(1, round(im.size[0] * k)), max(1, round(im.size[1] * k))), Image.LANCZOS)
    return im


for fn in sorted(os.listdir(src)):
    if not fn.endswith(".json"):
        continue
    name = fn[:-5]
    meta = json.load(open(os.path.join(src, fn)))
    shutil.copyfile(os.path.join(src, name + ".fbx"), os.path.join(models, name + ".fbx"))
    for m in meta["materials"]:
        for key in ("color", "normal"):
            if m.get(key):
                im = fit(Image.open(os.path.join(src, "Textures", m[key])))
                out = os.path.splitext(m[key])[0] + (".png" if key == "normal" else ".jpg")
                if key == "color":
                    im.convert("RGB").save(os.path.join(texs, out), quality=92)
                else:
                    im.convert("RGB").save(os.path.join(texs, out))
                m[key] = out
        m["ms"] = ""
        if m.get("orm"):
            orm = fit(Image.open(os.path.join(src, "Textures", m["orm"])).convert("RGB"))
            o, r, mt = orm.split()
            smooth = r.point(lambda v: 255 - v)
            ms = Image.merge("RGBA", (mt, o, Image.new("L", orm.size, 0), smooth))
            out = f"{name}_{os.path.splitext(m['orm'])[0].split('_')[-2]}_ms.png"
            ms.save(os.path.join(texs, out))
            m["ms"] = out
    json.dump(meta, open(os.path.join(models, fn), "w"), indent=1)
    print(name, meta["size"], meta["tris"], [(m["color"], m["normal"], m["ms"]) for m in meta["materials"] if m["color"]])
