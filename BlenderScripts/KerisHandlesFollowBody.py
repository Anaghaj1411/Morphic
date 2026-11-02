"""Keris Handle 1 / Handle 2 — keep them glued for Unity.

Paste into Blender: Scripting workspace → New → Run Script.
Select the Keris mesh first (or set OBJECT_NAME below).

What this is for
----------------
Unity dropped the old Handle 1 / Handle 2 keys because they were sparse,
and the coded overlay never sat on the tang. Sculpt the handles in Blender
again, then run this so they:

  1. Sit on the tang (thicker blade end) at full Handle 1 / Handle 2.
  2. Follow Curve, Sharpness, Height, and Weight/Width with that tang,
     so they stay attached when those sliders move.
  3. Write dense deltas Unity will not drop on FBX import (split verts
     at the same position get the same delta).

How to sculpt before you run
----------------------------
- Basis: handles collapsed into the tang (hidden inside / flush).
- Handle 1 = 1.0: J-hook grown OUT of the tang, inner face on the end.
- Handle 2 = 1.0: temple-pillar hulu grown OUT of the same tang.
- Do not sculpt handles in Curve / Height / Weight — this script copies
  the tang's motion into those keys for the handle verts.

Export to Unity
---------------
File → Export → FBX:
  - Selected Objects
  - Apply Scalings: FBX All
  - Add Leaf Bones: OFF
  - Smoothing: Face
  - Apply Modifiers: ON
Then reimport the FBX in Unity with Read/Write Enabled.
"""

import re
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

OBJECT_NAME = ""  # leave empty to use the selected mesh
HANDLE_KEY_NAMES = ("Handle 1", "Handle 2")
BODY_KEY_NAMES = ("Curve", "Sharpness", "Height", "Weight", "Width")
NEIGHBOR_COUNT = 6
VERTEX_EPSILON = 1e-5
TANG_BAND = 0.04
HANDLE_INNER_FRAC = 0.22
UNITY_SPLIT_EPS = 1e-4


def normalize_key_name(name):
    compact = re.sub(r"[^a-z0-9]+", "", name.lower())
    return re.sub(r"(\D)0+(\d+)", r"\1\2", compact)


def find_key(key_blocks, requested_name):
    normalized = normalize_key_name(requested_name)
    for key in key_blocks:
        if normalize_key_name(key.name) == normalized:
            return key
    return None


def selected_mesh():
    if OBJECT_NAME:
        obj = bpy.data.objects.get(OBJECT_NAME)
        if obj is not None and obj.type == "MESH":
            return obj
    obj = bpy.context.view_layer.objects.active
    if obj is not None and obj.type == "MESH":
        return obj
    meshes = [item.name for item in bpy.context.scene.objects if item.type == "MESH"]
    raise RuntimeError("Select the Keris mesh first. Meshes: " + ", ".join(meshes))


obj = selected_mesh()
shape_keys = obj.data.shape_keys
if shape_keys is None:
    raise RuntimeError(f"{obj.name!r} has no shape keys. Add Basis, Handle 1, Handle 2 first.")
key_blocks = shape_keys.key_blocks
basis = key_blocks.get("Basis") or key_blocks[0]

handle_keys = []
missing_handles = []
for name in HANDLE_KEY_NAMES:
    key = find_key(key_blocks, name)
    if key is None:
        missing_handles.append(name)
    else:
        handle_keys.append(key)
if missing_handles:
    raise RuntimeError(
        f"Missing handle keys: {missing_handles}. Available: {[k.name for k in key_blocks]}"
    )

body_keys = []
for name in BODY_KEY_NAMES:
    key = find_key(key_blocks, name)
    if key is not None and key not in body_keys:
        body_keys.append(key)

vertex_count = len(basis.data)
for key in handle_keys + body_keys:
    if len(key.data) != vertex_count:
        raise RuntimeError(f"Shape key {key.name!r} vertex count does not match Basis.")

# Handle island = verts that Handle 1 or Handle 2 actually move.
handle_islands = {}
all_handle_verts = set()
for key in handle_keys:
    moved = {
        i for i in range(vertex_count)
        if (Vector(key.data[i].co) - Vector(basis.data[i].co)).length > VERTEX_EPSILON
    }
    if not moved:
        raise RuntimeError(
            f"{key.name!r} does not move any vertices. Sculpt the full handle at 1.0 first."
        )
    handle_islands[key.name] = moved
    all_handle_verts.update(moved)

body_verts = [i for i in range(vertex_count) if i not in all_handle_verts]
if len(body_verts) < 16:
    raise RuntimeError("Handle keys cover almost the whole mesh; cannot find the blade tang.")


def axis_span(indices, coords):
    xs = [coords[i].x for i in indices]
    ys = [coords[i].y for i in indices]
    zs = [coords[i].z for i in indices]
    return (
        (max(xs) - min(xs), 0),
        (max(ys) - min(ys), 1),
        (max(zs) - min(zs), 2),
    )


basis_coords = [Vector(basis.data[i].co) for i in range(vertex_count)]
long_axis = max(axis_span(body_verts, basis_coords), key=lambda item: item[0])[1]


def along_of(index, coords=None):
    src = coords if coords is not None else basis_coords
    return src[index][long_axis]


body_along = [along_of(i) for i in body_verts]
min_l, max_l = min(body_along), max(body_along)
length = max(max_l - min_l, VERTEX_EPSILON)
band = max(length * TANG_BAND, VERTEX_EPSILON)


def band_indices(end_value):
    return [i for i in body_verts if abs(along_of(i) - end_value) <= band]


def mean_thickness(indices):
    if not indices:
        return 0.0
    other = [axis for axis in range(3) if axis != long_axis]
    acc = 0.0
    for i in indices:
        acc += abs(basis_coords[i][other[0]]) + abs(basis_coords[i][other[1]])
    return acc / len(indices)


min_band = band_indices(min_l)
max_band = band_indices(max_l)
tang_at_min = mean_thickness(min_band) >= mean_thickness(max_band)
tang_end = min_l if tang_at_min else max_l
tang_indices = min_band if tang_at_min else max_band
if not tang_indices:
    raise RuntimeError("Could not find tang-end vertices on the blade.")

tang_center = Vector((0.0, 0.0, 0.0))
for i in tang_indices:
    tang_center += basis_coords[i]
tang_center /= len(tang_indices)
out_sign = -1.0 if tang_at_min else 1.0


def snap_handle_to_tang(handle_key, island):
    """Slide the full handle so its inner face sits on the tang end."""
    coords = [Vector(handle_key.data[i].co) for i in island]
    alongs = [c[long_axis] for c in coords]
    inner_limit = (
        min(alongs) + (max(alongs) - min(alongs)) * HANDLE_INNER_FRAC
        if not tang_at_min
        else max(alongs) - (max(alongs) - min(alongs)) * HANDLE_INNER_FRAC
    )
    inner = []
    for idx, co in zip(island, coords):
        if tang_at_min:
            if co[long_axis] >= inner_limit:
                inner.append(co)
        elif co[long_axis] <= inner_limit:
            inner.append(co)
    if not inner:
        inner = coords
    inner_center = Vector((0.0, 0.0, 0.0))
    for co in inner:
        inner_center += co
    inner_center /= len(inner)
    offset = tang_center - inner_center
    # Keep the handle on the tang face; do not slide it along the blade.
    offset[long_axis] = tang_center[long_axis] - inner_center[long_axis]
    for i in island:
        handle_key.data[i].co = Vector(handle_key.data[i].co) + offset
    return offset


snap_report = {}
for key in handle_keys:
    snap_report[key.name] = tuple(snap_handle_to_tang(key, handle_islands[key.name]))

# Handle verts follow the nearest tang/body verts in Curve / Height / Weight / Sharpness.
tree = KDTree(len(body_verts))
for tree_index, vertex_index in enumerate(body_verts):
    tree.insert(basis_coords[vertex_index], tree_index)
tree.balance()
neighbor_count = min(max(1, NEIGHBOR_COUNT), len(body_verts))

nearest_body = {}
for vertex_index in all_handle_verts:
    nearest = tree.find_n(basis_coords[vertex_index], neighbor_count)
    exact = [item for item in nearest if item[2] < 1e-8]
    if exact:
        nearest_body[vertex_index] = [(body_verts[exact[0][1]], 1.0)]
        continue
    weighted = []
    total = 0.0
    for _co, tree_index, distance in nearest:
        weight = 1.0 / max(distance * distance, 1e-12)
        weighted.append((body_verts[tree_index], weight))
        total += weight
    nearest_body[vertex_index] = [
        (source, weight / total) for source, weight in weighted
    ]

follow_counts = {}
for body_key in body_keys:
    n = 0
    for vertex_index, neighbors in nearest_body.items():
        movement = Vector((0.0, 0.0, 0.0))
        for source_index, weight in neighbors:
            source_delta = Vector(body_key.data[source_index].co) - basis_coords[source_index]
            movement += source_delta * weight
        body_key.data[vertex_index].co = basis_coords[vertex_index] + movement
        n += 1
    follow_counts[body_key.name] = n

# Unity splits verts on import. Copy each handle delta onto coinciding verts
# so the shape is not dropped as "sparse".
unity_padded = {}
position_buckets = {}
for i, co in enumerate(basis_coords):
    key = (
        round(co.x / UNITY_SPLIT_EPS),
        round(co.y / UNITY_SPLIT_EPS),
        round(co.z / UNITY_SPLIT_EPS),
    )
    position_buckets.setdefault(key, []).append(i)

for handle_key in handle_keys:
    island = handle_islands[handle_key.name]
    padded = 0
    for i in list(island):
        co = basis_coords[i]
        bucket = (
            round(co.x / UNITY_SPLIT_EPS),
            round(co.y / UNITY_SPLIT_EPS),
            round(co.z / UNITY_SPLIT_EPS),
        )
        delta = Vector(handle_key.data[i].co) - basis_coords[i]
        if delta.length <= VERTEX_EPSILON:
            continue
        for j in position_buckets.get(bucket, ()):
            if j in island:
                continue
            handle_key.data[j].co = basis_coords[j] + delta
            island.add(j)
            all_handle_verts.add(j)
            padded += 1
    unity_padded[handle_key.name] = (len(island), padded)

shape_keys.update_tag()
obj.data.update()
bpy.context.view_layer.update()

axis_name = ("X", "Y", "Z")[long_axis]
print(
    f"Keris handles on {obj.name!r} (tang at {axis_name} {'min' if tang_at_min else 'max'})."
)
for name, offset in snap_report.items():
    print(f"  {name}: snapped inner face onto tang, offset {tuple(round(v, 4) for v in offset)}")
if body_keys:
    print(f"  Followed body keys: {', '.join(k.name for k in body_keys)} on {len(all_handle_verts)} handle verts.")
else:
    print("  No Curve/Sharpness/Height/Weight keys found — snap + Unity pad only.")
for name, (count, padded) in unity_padded.items():
    print(f"  {name}: {count} verts after Unity pad (+{padded} coinciding).")
print("Sculpt Handle 1 / Handle 2 at 1.0, then export FBX and reimport in Unity.")
