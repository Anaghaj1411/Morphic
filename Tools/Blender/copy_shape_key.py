# OLD LANTERN — select the old mesh, then Run Script.
# Copies one shape key into:
#   Tools/Blender/copied_shape_key.json

import json
from pathlib import Path

import bpy
from mathutils import Vector

# Change this if the key is not named exactly this.
SHAPE_KEY_NAME = "Pop Up Bamboo"
OUT_PATH = Path(r"C:\Users\anagh\Documents\Morphic\Tools\Blender\copied_shape_key.json")


def compact(name):
    return "".join(ch for ch in name.upper() if ch.isalnum())


def find_key(keys, wanted):
    needle = compact(wanted)
    exact = keys.get(wanted)
    if exact is not None:
        return exact
    for key in keys:
        if needle in compact(key.name) or compact(key.name) in needle:
            return key
    return None


obj = bpy.context.object
if obj is None or obj.type != "MESH" or obj.data.shape_keys is None:
    raise RuntimeError("Select the OLD lantern mesh that has the bamboo shape key.")

keys = obj.data.shape_keys.key_blocks
basis = keys.get("Basis") or keys.get("basis") or keys[0]
source = find_key(keys, SHAPE_KEY_NAME)
if source is None:
    names = [key.name for key in keys]
    raise RuntimeError("Could not find %s. Keys: %s" % (SHAPE_KEY_NAME, names))

payload = {
    "name": "Pop Up Bamboo",
    "source_name": source.name,
    "vertex_count": len(basis.data),
    "basis": [list(v.co) for v in basis.data],
    "shape": [list(v.co) for v in source.data],
}
OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
OUT_PATH.write_text(json.dumps(payload), encoding="utf-8")
print("MORPHIC: Copied", source.name, "verts", payload["vertex_count"], "to", OUT_PATH)
