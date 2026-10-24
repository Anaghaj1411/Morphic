"""Make lid and handle regions follow the teapot's Width/Height/Middle keys.

The teapot is one mesh with shape keys. This script uses each part shape key
to identify that part's vertices, then transfers the nearest body's movement
into the existing Width, Height, and Middle keys. It does not add shape keys.

Lid 1 already sits on the pot in Basis, so it follows from Basis. Lid 2 is
collapsed in Basis and only becomes a full lid in the Lid 2 key, so it follows
from that expanded pose. Both lids then get the same Height lift, Middle rise,
and Width/Middle scale.

Run from Blender's Scripting workspace. The mesh object and shape-key names
are configurable below. Running it again is safe; results are rebuilt from
Basis each time.
"""

import re
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


OBJECT_NAME = "Cylinder.002"
PART_KEY_NAMES = ("Lid 1", "Lid 2", "Handle 1", "Handle 2")
LID_KEY_NAMES = ("Lid 1", "Lid 2")
BODY_KEY_NAMES = ("Width", "Height", "Middle")
NEIGHBOR_COUNT = 6
VERTEX_EPSILON = 1e-5


def normalize_key_name(name):
    compact = re.sub(r"[^a-z0-9]+", "", name.lower())
    return re.sub(r"(\D)0+(\d+)", r"\1\2", compact)


def find_key(key_blocks, requested_name):
    normalized = normalize_key_name(requested_name)
    for key in key_blocks:
        if normalize_key_name(key.name) == normalized:
            return key
    return None


obj = bpy.data.objects.get(OBJECT_NAME)
if obj is None:
    active = bpy.context.view_layer.objects.active
    if active is not None and active.type == "MESH":
        obj = active
if obj is None or obj.type != "MESH":
    mesh_names = [item.name for item in bpy.context.scene.objects if item.type == "MESH"]
    raise RuntimeError(f"Select the teapot mesh or set OBJECT_NAME. Meshes: {mesh_names}")

shape_keys = obj.data.shape_keys
if shape_keys is None:
    raise RuntimeError(f"{obj.name!r} has no shape keys.")
key_blocks = shape_keys.key_blocks
basis = key_blocks.get("Basis")
if basis is None:
    raise RuntimeError("The teapot mesh needs a Basis shape key.")

part_keys = []
missing_parts = []
for name in PART_KEY_NAMES:
    key = find_key(key_blocks, name)
    if key is None:
        missing_parts.append(name)
    else:
        part_keys.append(key)

body_keys = []
missing_body_keys = []
for name in BODY_KEY_NAMES:
    key = find_key(key_blocks, name)
    if key is None:
        missing_body_keys.append(name)
    else:
        body_keys.append(key)

if missing_parts or missing_body_keys:
    raise RuntimeError(
        f"Missing part keys: {missing_parts}; missing body keys: {missing_body_keys}. "
        f"Available keys: {[key.name for key in key_blocks]}"
    )

vertex_count = len(basis.data)
for key in part_keys + body_keys:
    if len(key.data) != vertex_count:
        raise RuntimeError(f"Shape key {key.name!r} has a different vertex count from Basis.")

# A vertex belongs to a part when that part's own key moves it away from Basis.
part_vertex_sets = {}
for key in part_keys:
    moved = {
        index for index, base_point in enumerate(basis.data)
        if (key.data[index].co - base_point.co).length > VERTEX_EPSILON
    }
    if not moved:
        raise RuntimeError(
            f"Shape key {key.name!r} does not move any vertices, so it cannot "
            "identify that part."
        )
    part_vertex_sets[key.name] = moved

part_vertices = set().union(*part_vertex_sets.values())
body_vertices = [index for index in range(vertex_count) if index not in part_vertices]
if not body_vertices:
    raise RuntimeError("Part keys cover the whole mesh; no body vertices remain as references.")

height_key = find_key(key_blocks, "Height")
width_key = find_key(key_blocks, "Width")
middle_key = find_key(key_blocks, "Middle")
lid1_key = find_key(key_blocks, "Lid 1")
lid2_key = find_key(key_blocks, "Lid 2")
lid_cover_keys = [key for key in (lid1_key, lid2_key) if key is not None]
lid_cover_vertices = set()
for lid_key in lid_cover_keys:
    lid_cover_vertices.update(part_vertex_sets[lid_key.name])
if not lid_cover_vertices:
    raise RuntimeError("Lid 1 and Lid 2 did not identify any lid vertices.")

# Lid 1 already sits on the pot in Basis. Lid 2 is collapsed in Basis and only
# becomes a full lid in its own key, so follow that expanded pose instead.
lid_rest = {}
if lid1_key is not None:
    for vertex_index in part_vertex_sets[lid1_key.name]:
        lid_rest[vertex_index] = basis.data[vertex_index].co.copy()
if lid2_key is not None:
    for vertex_index in part_vertex_sets[lid2_key.name]:
        lid_rest[vertex_index] = lid2_key.data[vertex_index].co.copy()

height_axis = None
body_top_delta = None
middle_top_delta = 0.0
width_fit = {}
middle_fit = {}
if height_key is not None and lid_cover_keys:
    axis_metrics = []
    for axis in range(3):
        basis_values = [basis.data[i].co[axis] for i in body_vertices]
        height_values = [height_key.data[i].co[axis] for i in body_vertices]
        extent_delta = (max(height_values) - min(height_values)) - (
            max(basis_values) - min(basis_values)
        )
        top_delta = max(height_values) - max(basis_values)
        axis_metrics.append((axis, extent_delta, top_delta))

    height_axis, extent_delta, body_top_delta = max(
        axis_metrics,
        key=lambda item: max(abs(item[1]), abs(item[2])),
    )
    if abs(body_top_delta) <= VERTEX_EPSILON:
        raise RuntimeError(
            "The Height shape key changes the teapot, but its upper surface "
            "does not move on the detected axis. Axis measurements "
            f"(axis, extent change, top change): {axis_metrics}"
        )

    if middle_key is not None:
        base_middle_top = max(basis.data[i].co[height_axis] for i in body_vertices)
        changed_middle_top = max(middle_key.data[i].co[height_axis] for i in body_vertices)
        middle_top_delta = changed_middle_top - base_middle_top

    if width_key is not None:
        for axis in range(3):
            if axis == height_axis:
                continue
            base_values = [basis.data[i].co[axis] for i in body_vertices]
            width_values = [width_key.data[i].co[axis] for i in body_vertices]
            base_min, base_max = min(base_values), max(base_values)
            width_min, width_max = min(width_values), max(width_values)
            base_size = base_max - base_min
            scale = (width_max - width_min) / base_size if base_size > VERTEX_EPSILON else 1.0
            width_fit[axis] = (
                (base_min + base_max) * 0.5,
                (width_min + width_max) * 0.5,
                scale,
            )

    if middle_key is not None:
        for axis in range(3):
            if axis == height_axis:
                continue
            base_values = [basis.data[i].co[axis] for i in body_vertices]
            middle_values = [middle_key.data[i].co[axis] for i in body_vertices]
            base_min, base_max = min(base_values), max(base_values)
            middle_min, middle_max = min(middle_values), max(middle_values)
            base_size = base_max - base_min
            scale = (middle_max - middle_min) / base_size if base_size > VERTEX_EPSILON else 1.0
            middle_fit[axis] = (
                (base_min + base_max) * 0.5,
                (middle_min + middle_max) * 0.5,
                scale,
            )


def write_lid_target(body_key, vertex_index, target):
    rest = lid_rest[vertex_index]
    base_point = basis.data[vertex_index].co
    body_key.data[vertex_index].co = base_point + (target - rest)


def apply_lid_follow(body_key):
    if height_axis is None:
        return

    if body_key == height_key:
        for vertex_index in lid_cover_vertices:
            target = lid_rest[vertex_index].copy()
            target[height_axis] = target[height_axis] + body_top_delta
            write_lid_target(body_key, vertex_index, target)
        return

    if body_key == width_key:
        for vertex_index in lid_cover_vertices:
            rest = lid_rest[vertex_index]
            target = rest.copy()
            for axis, (base_center, target_center, scale) in width_fit.items():
                target[axis] = target_center + (rest[axis] - base_center) * scale
            target[height_axis] = rest[height_axis]
            write_lid_target(body_key, vertex_index, target)
        return

    if body_key == middle_key:
        for vertex_index in lid_cover_vertices:
            rest = lid_rest[vertex_index]
            target = rest.copy()
            for axis, (base_center, target_center, scale) in middle_fit.items():
                target[axis] = target_center + (rest[axis] - base_center) * scale
            target[height_axis] = rest[height_axis] + middle_top_delta
            write_lid_target(body_key, vertex_index, target)


tree = KDTree(len(body_vertices))
for tree_index, vertex_index in enumerate(body_vertices):
    tree.insert(basis.data[vertex_index].co, tree_index)
tree.balance()

neighbor_count = min(max(1, NEIGHBOR_COUNT), len(body_vertices))
nearest_body_vertices = {}
for part_name, indices in part_vertex_sets.items():
    for vertex_index in indices:
        point = basis.data[vertex_index].co
        nearest = tree.find_n(point, neighbor_count)
        exact = [item for item in nearest if item[2] < 1e-8]
        if exact:
            nearest_body_vertices[vertex_index] = [(body_vertices[exact[0][1]], 1.0)]
            continue
        weighted = []
        total = 0.0
        for coordinate, tree_index, distance in nearest:
            weight = 1.0 / max(distance * distance, 1e-12)
            weighted.append((body_vertices[tree_index], weight))
            total += weight
        nearest_body_vertices[vertex_index] = [
            (source_index, weight / total) for source_index, weight in weighted
        ]

updated_counts = {key.name: len(part_vertex_sets[key.name]) for key in part_keys}
lid_counts = {key.name: len(part_vertex_sets[key.name]) for key in lid_cover_keys}
for body_key in body_keys:
    for vertex_index, neighbors in nearest_body_vertices.items():
        base_point = basis.data[vertex_index].co
        movement = base_point * 0.0
        for source_index, weight in neighbors:
            source_delta = body_key.data[source_index].co - basis.data[source_index].co
            movement += source_delta * weight
        body_key.data[vertex_index].co = base_point + movement

    apply_lid_follow(body_key)

# Mark shape-key coordinate edits dirty so Blender writes them into the file.
shape_keys.update_tag()
obj.data.update()
bpy.context.view_layer.update()

print(
    f"Updated {', '.join(key.name for key in body_keys)} on {obj.name!r}. "
    f"Part vertices: {updated_counts}. Lid follow applied to {lid_counts}. "
    "Lid 1 follows from Basis. Lid 2 follows from its expanded Lid 2 pose. "
    "Turn Lid 2 up, then slide Width / Height / Middle to verify. "
    "No extra shape keys were added."
)
