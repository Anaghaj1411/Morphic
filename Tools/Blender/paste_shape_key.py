# NEW LANTERN — select the new lantern mesh, then Run Script.
# Reads:
#   Tools/Blender/copied_shape_key.json
# and adds shape key "Pop Up Bamboo".

import json
from pathlib import Path

import bpy
from mathutils import Vector, kdtree

IN_PATH = Path(r"C:\Users\anagh\Documents\Morphic\Tools\Blender\copied_shape_key.json")
NEW_KEY_NAME = "Pop Up Bamboo"


def add_or_replace_key(obj, name):
    keys = obj.data.shape_keys
    if keys is None:
        obj.shape_key_add(name="Basis", from_mix=False)
        keys = obj.data.shape_keys
    existing = keys.key_blocks.get(name)
    if existing is not None:
        obj.shape_key_remove(existing)
    return obj.shape_key_add(name=name, from_mix=False)


obj = bpy.context.object
if obj is None or obj.type != "MESH":
    raise RuntimeError("Select the NEW lantern mesh first.")
if not IN_PATH.exists():
    raise RuntimeError("Run copy_shape_key.py on the old lantern first. Missing " + str(IN_PATH))

data = json.loads(IN_PATH.read_text(encoding="utf-8"))
old_basis = [Vector(p) for p in data["basis"]]
old_shape = [Vector(p) for p in data["shape"]]
if obj.data.shape_keys is None:
    obj.shape_key_add(name="Basis", from_mix=False)

new_basis = obj.data.shape_keys.key_blocks[0]
new_count = len(new_basis.data)
old_count = len(old_basis)
key = add_or_replace_key(obj, NEW_KEY_NAME)

if new_count == old_count:
    for index, dest in enumerate(key.data):
        dest.co = old_shape[index]
    print("MORPHIC: Pasted", NEW_KEY_NAME, "by matching vertex order.")
else:
    tree = kdtree.KDTree(old_count)
    for index, point in enumerate(old_basis):
        tree.insert(point, index)
    tree.balance()
    for index, dest in enumerate(key.data):
        rest = Vector(new_basis.data[index].co)
        _co, old_index, _dist = tree.find(rest)
        dest.co = rest + (old_shape[old_index] - old_basis[old_index])
    print(
        "MORPHIC: Pasted",
        NEW_KEY_NAME,
        "by nearest vertex. Old verts",
        old_count,
        "new verts",
        new_count,
    )

for block in obj.data.shape_keys.key_blocks:
    block.value = 0.0
key.slider_min = 0.0
key.slider_max = 1.0
key.value = 1.0
bpy.context.view_layer.update()
print("MORPHIC: Slide", NEW_KEY_NAME, "to check it. Then set it back to 0 and export FBX.")
