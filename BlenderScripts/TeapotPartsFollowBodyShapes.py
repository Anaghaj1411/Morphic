"""Make lid, shaft, and handle regions follow the teapot body keys.

Lid 1, Lid 2, Shaft 1, Shaft 2, Handle 1, and Handle 2 follow Width, Height,
and Middle. Lid 1 follows the Height key along with the other parts.

The script preserves the part poses already edited in the file. It only writes
their Width, Height, and Middle follow shapes; Lid 1 follows Height as well.
"""

import re
import bmesh
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


OBJECT_NAME = "Cylinder.002"
PART_KEY_NAMES = ("Lid 1", "Lid 2", "Shaft 1", "Shaft 2", "Handle 1", "Handle 2")
LID_KEY_NAMES = ("Lid 1", "Lid 2")
SIDE_KEY_NAMES = ("Shaft 1", "Shaft 2", "Handle 1", "Handle 2")
BODY_KEY_NAMES = ("Width", "Height", "Middle")
NEIGHBOR_COUNT = 6
VERTEX_EPSILON = 1e-5
COLLAPSE_SPAN_RATIO = 2.0
SHAFT1_HEIGHT_FACTOR = 0.48


def normalize_key_name(name):
    compact = re.sub(r"[^a-z0-9]+", "", name.lower())
    return re.sub(r"(\D)0+(\d+)", r"\1\2", compact)


def find_key(key_blocks, requested_name):
    normalized = normalize_key_name(requested_name)
    for key in key_blocks:
        if normalize_key_name(key.name) == normalized:
            return key
    return None


def bbox_span(indices, get_co):
    if not indices:
        return 0.0
    xs = [get_co(index)[0] for index in indices]
    ys = [get_co(index)[1] for index in indices]
    zs = [get_co(index)[2] for index in indices]
    return max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))


def is_collapsed_part(indices, part_key):
    basis_span = bbox_span(indices, lambda index: basis.data[index].co)
    key_span = bbox_span(indices, lambda index: part_key.data[index].co)
    return key_span > max(basis_span, VERTEX_EPSILON) * COLLAPSE_SPAN_RATIO


obj = bpy.data.objects.get(OBJECT_NAME)
if obj is None:
    active = bpy.context.view_layer.objects.active
    if active is not None and active.type == "MESH":
        obj = active
if obj is None or obj.type != "MESH":
    mesh_names = [item.name for item in bpy.data.objects if item.type == "MESH"]
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

lid_keys = [find_key(key_blocks, name) for name in LID_KEY_NAMES]
lid_keys = [key for key in lid_keys if key is not None]
side_keys = [find_key(key_blocks, name) for name in SIDE_KEY_NAMES]
side_keys = [key for key in side_keys if key is not None]

lid_vertices = set()
for key in lid_keys:
    lid_vertices.update(part_vertex_sets[key.name])
side_vertices = set()
for key in side_keys:
    side_vertices.update(part_vertex_sets[key.name])
follow_vertices = lid_vertices | side_vertices

height_axis = None
body_top_delta = None
body_min = body_max = 0.0
height_min = height_max = 0.0
middle_top_delta = 0.0
width_fit = {}
middle_fit = {}
if height_key is not None:
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

    body_min = min(basis.data[i].co[height_axis] for i in body_vertices)
    body_max = max(basis.data[i].co[height_axis] for i in body_vertices)
    height_min = min(height_key.data[i].co[height_axis] for i in body_vertices)
    height_max = max(height_key.data[i].co[height_axis] for i in body_vertices)

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


def body_center_point():
    center = basis.data[body_vertices[0]].co.copy() * 0.0
    for vertex_index in body_vertices:
        center += basis.data[vertex_index].co
    center /= len(body_vertices)
    return center


def shaft_attach(shaft_key, vertices):
    body_center = body_center_point()
    ranked = []
    for vertex_index in vertices:
        ranked.append(((shaft_key.data[vertex_index].co - body_center).length, vertex_index))
    ranked.sort()
    attach_count = max(8, len(ranked) // 5)
    attach = shaft_key.data[ranked[0][1]].co.copy() * 0.0
    for _distance, vertex_index in ranked[:attach_count]:
        attach += shaft_key.data[vertex_index].co
    attach /= attach_count
    return attach


def flatten_shaft1_horizontal():
    """Level the shaft's long axis without moving its wall connection."""
    shaft_key = find_key(key_blocks, "Shaft 1")
    if shaft_key is None or height_axis is None:
        return
    vertices = part_vertex_sets.get(shaft_key.name)
    if not vertices:
        return

    attach = shaft_attach(shaft_key, vertices)
    desired = attach - body_center_point()
    desired[height_axis] = 0.0
    if desired.length <= VERTEX_EPSILON:
        return
    desired.normalize()

    points = [shaft_key.data[index].co.copy() for index in vertices]
    centroid = sum(points, points[0].copy() * 0.0) / len(points)
    covariance = [[0.0] * 3 for _ in range(3)]
    for point in points:
        delta = point - centroid
        for row in range(3):
            for column in range(3):
                covariance[row][column] += delta[row] * delta[column]

    # Power iteration finds the shaft's main lengthwise direction. Rotate
    # around the existing attachment point so its base stays in the wall.
    current = Vector((1.0, 0.37, 0.19)).normalized()
    for _iteration in range(32):
        current = Vector(tuple(
            sum(covariance[row][column] * current[column] for column in range(3))
            for row in range(3)
        ))
        if current.length <= VERTEX_EPSILON:
            return
        current.normalize()
    if current.dot(desired) < 0.0:
        current.negate()
    rotation = current.rotation_difference(desired)
    for vertex_index in vertices:
        offset = shaft_key.data[vertex_index].co - attach
        shaft_key.data[vertex_index].co = attach + rotation @ offset


def raise_shaft1():
    """Slide the flattened Shaft 1 up the pot wall a little."""
    shaft_key = find_key(key_blocks, "Shaft 1")
    if shaft_key is None or height_axis is None:
        return
    vertices = part_vertex_sets.get(shaft_key.name)
    if not vertices:
        return

    target = body_min + (body_max - body_min) * SHAFT1_HEIGHT_FACTOR
    current = sum(shaft_key.data[index].co[height_axis] for index in vertices)
    current /= len(vertices)
    delta = target - current
    if abs(delta) <= VERTEX_EPSILON:
        return
    for vertex_index in vertices:
        shaft_key.data[vertex_index].co[height_axis] += delta


def hide_shaft1_stub(margin=0.8, shrink=0.12):
    """Park the Shaft 1 Basis stub inside the pot on every key except Shaft 1."""
    shaft1_key = find_key(key_blocks, "Shaft 1")
    if shaft1_key is None or height_axis is None:
        return
    vertices = part_vertex_sets.get(shaft1_key.name)
    if not vertices:
        return

    stub_center = basis.data[next(iter(vertices))].co.copy() * 0.0
    for vertex_index in vertices:
        stub_center += basis.data[vertex_index].co
    stub_center /= len(vertices)

    body_center = body_center_point()
    outward = stub_center - body_center
    outward[height_axis] = 0.0
    if outward.length <= VERTEX_EPSILON:
        return
    outward.normalize()

    wall_out = max(
        (basis.data[index].co - body_center).dot(outward) for index in body_vertices
    )
    inside = body_center + outward * (wall_out - margin)
    hidden = {}
    for vertex_index in vertices:
        local = basis.data[vertex_index].co - stub_center
        hidden[vertex_index] = inside + local * shrink

    for key in key_blocks:
        if key == shaft1_key:
            continue
        for vertex_index in vertices:
            key.data[vertex_index].co = hidden[vertex_index].copy()
    for vertex_index in vertices:
        basis.data[vertex_index].co = hidden[vertex_index]


part_rest = {}
rest_sources = {}
for key in part_keys:
    indices = part_vertex_sets[key.name]
    use_key_pose = (
        is_collapsed_part(indices, key)
        or normalize_key_name(key.name) == "shaft1"
    )
    rest_sources[key.name] = key.name if use_key_pose else "Basis"
    for vertex_index in indices:
        part_rest[vertex_index] = (
            key.data[vertex_index].co.copy() if use_key_pose
            else basis.data[vertex_index].co.copy()
        )


def write_part_target(body_key, vertex_index, target):
    rest = part_rest[vertex_index]
    base_point = basis.data[vertex_index].co
    body_key.data[vertex_index].co = base_point + (target - rest)


def scaled_target(rest, fit, lift=0.0):
    target = rest.copy()
    for axis, (base_center, target_center, scale) in fit.items():
        target[axis] = target_center + (rest[axis] - base_center) * scale
    target[height_axis] = rest[height_axis] + lift
    return target


def height_target(rest):
    target = rest.copy()
    span = body_max - body_min
    if span <= VERTEX_EPSILON:
        target[height_axis] = rest[height_axis] + body_top_delta
        return target
    factor = (rest[height_axis] - body_min) / span
    target[height_axis] = height_min + factor * (height_max - height_min)
    return target


def apply_part_follow(body_key):
    if height_axis is None:
        return

    if body_key == height_key:
        for vertex_index in follow_vertices:
            write_part_target(body_key, vertex_index, height_target(part_rest[vertex_index]))
        return

    if body_key == width_key:
        for vertex_index in follow_vertices:
            write_part_target(
                body_key,
                vertex_index,
                scaled_target(part_rest[vertex_index], width_fit),
            )
        return

    if body_key == middle_key:
        for vertex_index in lid_vertices:
            write_part_target(
                body_key,
                vertex_index,
                scaled_target(part_rest[vertex_index], middle_fit, middle_top_delta),
            )
        for vertex_index in side_vertices:
            write_part_target(
                body_key,
                vertex_index,
                scaled_target(part_rest[vertex_index], middle_fit),
            )


def tuck_shaft1_root_follow(body_key):
    """Inset only the Shaft 1 root in Width/Middle follow shapes."""
    if body_key not in (width_key, middle_key) or height_axis is None:
        return
    shaft_key = find_key(key_blocks, "Shaft 1")
    vertices = part_vertex_sets.get(shaft_key.name) if shaft_key else None
    if not vertices:
        return

    center = body_center_point()
    shaft_center = sum(
        (shaft_key.data[index].co for index in vertices),
        shaft_key.data[next(iter(vertices))].co.copy() * 0.0,
    ) / len(vertices)
    outward = shaft_center - center
    outward[height_axis] = 0.0
    if outward.length <= VERTEX_EPSILON:
        return
    outward.normalize()

    projections = {
        index: (shaft_key.data[index].co - center).dot(outward)
        for index in vertices
    }
    low = min(projections.values())
    high = max(projections.values())
    span = high - low
    if span <= VERTEX_EPSILON:
        return

    horizontal_spans = []
    for axis in range(3):
        if axis == height_axis:
            continue
        values = [basis.data[index].co[axis] for index in body_vertices]
        horizontal_spans.append(max(values) - min(values))
    inset = max(horizontal_spans) * 0.035
    blend_span = span * 0.32
    for index, projection in projections.items():
        weight = max(0.0, 1.0 - (projection - low) / blend_span)
        if weight:
            body_key.data[index].co = (
                body_key.data[index].co - outward * (inset * weight)
            )


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
for body_key in body_keys:
    for vertex_index, neighbors in nearest_body_vertices.items():
        base_point = basis.data[vertex_index].co
        movement = base_point * 0.0
        for source_index, weight in neighbors:
            source_delta = body_key.data[source_index].co - basis.data[source_index].co
            movement += source_delta * weight
        body_key.data[vertex_index].co = base_point + movement

    apply_part_follow(body_key)
    tuck_shaft1_root_follow(body_key)

# Commit the edited coordinates through Edit Mode so Blender saves each key.
key_coordinates = {
    body_key.name: [point.co.copy() for point in body_key.data]
    for body_key in body_keys
}
if obj.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
for body_key in body_keys:
    obj.active_shape_key_index = key_blocks.find(body_key.name)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_mesh = bmesh.from_edit_mesh(obj.data)
    edit_mesh.verts.ensure_lookup_table()
    for vertex_index, vertex in enumerate(edit_mesh.verts):
        vertex.co = key_coordinates[body_key.name][vertex_index]
    bmesh.update_edit_mesh(obj.data, loop_triangles=False, destructive=False)
    bpy.ops.object.mode_set(mode="OBJECT")

bpy.context.view_layer.update()
bpy.context.view_layer.update()

print(
    f"Updated {', '.join(key.name for key in body_keys)} on {obj.name!r}. "
    f"Part vertices: {updated_counts}. Rest poses: {rest_sources}."
)
