"""
Morphic - Lantern Base Shape Generator (Blender)
================================================

Procedurally builds a "lantern" base shape for the Morphic sculpting app and
exports it as FBX (with + without shape keys) and a .blend file.

Blend shape names/order match SculptShapeKeyController's name search:
    index 0 : Width
    index 1 : Puff
    index 2 : BottomFlatten   (matches "flatten")
    index 3 : TopGather       (matches "gather")
    index 4 : TopPinch        (matches "toppinch" / "top pinch" / "pinch")

Usage (headless):
    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" ^
        --background --factory-startup --python create_lantern_base.py -- ^
        --outdir "C:\\Users\\anagh\\Documents\\Morphic\\Assets\\BaseShapes\\Models"

Usage (interactive): open the script in Blender's Scripting tab and Run.
"""

import math
import os
import sys

import bpy


# ---------------------------------------------------------------------------
# Tunable parameters
# ---------------------------------------------------------------------------

SEGMENTS = 64          # radial resolution of the lathe
HEIGHT = 1.36          # total lantern height (Unity units)
MAX_RADIUS = 0.63      # widest radius (the open top rim)

# Silhouette modelled on a sky / Kongming lantern: a small rounded base that
# flares outward and upward to a wide open top rim, with a slightly flattened
# top. Deliberately NOT a finished lantern - it is a clean, evenly spaced
# surface for the user to sculpt. Tapered rather than boxy so Inflate/Indent
# and Flatten all have room to work.

INCLUDE_HANDLE = False  # sky lanterns have no hanging ring
HANDLE_MAJOR_R = 0.15
HANDLE_MINOR_R = 0.030

EXPORT_KEYS = True     # export LanternBase.fbx (with shape keys)
EXPORT_STATIC = True   # export LanternBase_Static.fbx (no shape keys)
SAVE_BLEND = True      # save LanternBase.blend

# Lantern silhouette: list of (radius, z). z runs from 0 (bottom) to HEIGHT.
PROFILE = [
    (0.000, 0.000),   # bottom centre
    (0.170, 0.000),   # small rounded base
    (0.285, 0.035),   # base rim
    (0.330, 0.110),
    (0.375, 0.260),
    (0.425, 0.470),
    (0.480, 0.700),   # flare outward
    (0.535, 0.920),
    (0.585, 1.110),
    (0.620, 1.230),
    (0.632, 1.290),   # widest point (top rim)
    (0.600, 1.330),   # top edge
    (0.480, 1.350),   # slightly flattened top
    (0.250, 1.360),
    (0.000, 1.362),   # top centre
]


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def clamp01(v):
    return 0.0 if v < 0.0 else (1.0 if v > 1.0 else v)


def smoothstep(edge0, edge1, x):
    if edge1 == edge0:
        return 0.0
    t = clamp01((x - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.objects,
    ):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def build_lantern_body(segments, profile):
    """Create the lathed body mesh object and return it."""
    verts = []
    rings = []

    for (radius, z) in profile:
        if radius <= 1e-5:
            idx = len(verts)
            verts.append((0.0, 0.0, z))
            rings.append([idx])          # collapsed pole
        else:
            ring = []
            for s in range(segments):
                angle = 2.0 * math.pi * s / segments
                x = radius * math.cos(angle)
                y = radius * math.sin(angle)
                idx = len(verts)
                verts.append((x, y, z))
                ring.append(idx)
            rings.append(ring)

    faces = []
    for i in range(len(rings) - 1):
        lower = rings[i]
        upper = rings[i + 1]

        if len(lower) == 1 and len(upper) > 1:
            pole = lower[0]
            for s in range(segments):
                s2 = (s + 1) % segments
                faces.append((pole, upper[s2], upper[s]))
        elif len(upper) == 1 and len(lower) > 1:
            pole = upper[0]
            for s in range(segments):
                s2 = (s + 1) % segments
                faces.append((lower[s], lower[s2], pole))
        else:
            for s in range(segments):
                s2 = (s + 1) % segments
                faces.append((lower[s], lower[s2], upper[s2], upper[s]))

    mesh = bpy.data.meshes.new("LanternBase_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.validate(verbose=False)
    mesh.update()

    obj = bpy.data.objects.new("LanternBase", mesh)
    bpy.context.collection.objects.link(obj)

    return obj


def add_handle(center_z):
    """Add a torus ring (hanging loop) on top of the lantern."""
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        location=(0.0, 0.0, center_z),
        major_radius=HANDLE_MAJOR_R,
        minor_radius=HANDLE_MINOR_R,
        major_segments=32,
        minor_segments=12,
    )
    handle = bpy.context.active_object
    handle.name = "LanternHandle"
    return handle


def join_objects(main, other):
    bpy.ops.object.select_all(action="DESELECT")
    main.select_set(True)
    other.select_set(True)
    bpy.context.view_layer.objects.active = main
    bpy.ops.object.join()


def cleanup_geometry(obj):
    """Recalculate normals outward and shade smooth."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    bpy.ops.object.shade_smooth()


def center_geometry(obj):
    """Translate the mesh so its bounding-box centre sits on the origin.

    Morphic's PinchSculptBrush maps the hand position onto a sphere centred on
    the object's PIVOT (transform.position), and FistRotateSculpture rotates
    around that pivot. A mesh whose centre is offset from the pivot (e.g. a base
    resting on z=0) therefore sculpts in the wrong place and swings out of frame
    when rotated. Centering the geometry makes the lantern behave like the
    built-in sphere base.
    """
    mesh = obj.data
    if len(mesh.vertices) == 0:
        return

    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    cx = (min(xs) + max(xs)) * 0.5
    cy = (min(ys) + max(ys)) * 0.5
    cz = (min(zs) + max(zs)) * 0.5

    for v in mesh.vertices:
        v.co.x -= cx
        v.co.y -= cy
        v.co.z -= cz

    mesh.update()



# ---------------------------------------------------------------------------
# Shape key definitions
# ---------------------------------------------------------------------------

def region_weights(t):
    """Return (belly, bottom, top) influence weights for a normalised height t."""
    belly = math.exp(-((t - 0.40) / 0.35) ** 2)
    bottom = clamp01(1.0 - t / 0.35)
    top = smoothstep(0.60, 1.0, t)
    return belly, bottom, top


def shape_width(x, y, z, t, belly, bottom, top, height):
    scale = 1.0 + 0.30 * belly
    return (x * scale, y * scale, z)


def shape_puff(x, y, z, t, belly, bottom, top, height):
    s = 1.15
    # Geometry is centred on the origin, so inflate about z = 0.
    return (x * s, y * s, z * s)


def shape_bottom_flatten(x, y, z, t, belly, bottom, top, height):
    rscale = 1.0 + 0.28 * bottom
    z_new = z * (1.0 - 0.35 * bottom)
    return (x * rscale, y * rscale, z_new)


def shape_top_gather(x, y, z, t, belly, bottom, top, height):
    rscale = 1.0 - 0.50 * top
    z_new = z - 0.12 * height * top
    return (x * rscale, y * rscale, z_new)


def shape_top_pinch(x, y, z, t, belly, bottom, top, height):
    rscale = 1.0 - 0.75 * top
    z_new = z + 0.12 * height * top
    return (x * rscale, y * rscale, z_new)


SHAPE_KEYS = [
    ("Width", shape_width),
    ("Puff", shape_puff),
    ("BottomFlatten", shape_bottom_flatten),
    ("TopGather", shape_top_gather),
    ("TopPinch", shape_top_pinch),
]


def add_shape_keys(obj):
    mesh = obj.data

    basis = obj.shape_key_add(name="Basis", from_mix=False)
    basis.value = 0.0

    verts = mesh.vertices

    # Derive the vertical range from the (already centred) geometry so the
    # shape keys stay correct regardless of where the mesh sits.
    zs = [v.co.z for v in verts]
    z_min = min(zs) if zs else 0.0
    z_max = max(zs) if zs else 1.0
    span = max(z_max - z_min, 1e-6)

    for name, fn in SHAPE_KEYS:
        key = obj.shape_key_add(name=name, from_mix=False)
        key.value = 0.0
        for i, v in enumerate(verts):
            x, y, z = v.co.x, v.co.y, v.co.z
            t = clamp01((z - z_min) / span)
            belly, bottom, top = region_weights(t)
            nx, ny, nz = fn(x, y, z, t, belly, bottom, top, span)
            key.data[i].co = (nx, ny, nz)

    mesh.update()


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

def export_fbx(obj, filepath, export_shape_keys):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    desired = {
        "filepath": filepath,
        "use_selection": True,
        "apply_scale_options": "FBX_SCALE_NONE",
        "axis_forward": "-Z",
        "axis_up": "Y",
        "global_scale": 1.0,
        "bake_anim": False,
        "add_leaf_bones": False,
        "mesh_smooth_type": "FACE",
        "use_mesh_modifiers": True,
        "use_mesh_edges": False,
        "use_custom_props": False,
        "path_mode": "AUTO",
        "export_shapekeys": export_shape_keys,
    }

    supported = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
    kwargs = {k: v for k, v in desired.items() if k in supported}
    bpy.ops.export_scene.fbx(**kwargs)


def duplicate_without_shape_keys(obj):
    dup = obj.copy()
    dup.data = obj.data.copy()
    dup.name = obj.name + "_Static"
    bpy.context.collection.objects.link(dup)

    bpy.ops.object.select_all(action="DESELECT")
    dup.select_set(True)
    bpy.context.view_layer.objects.active = dup

    if dup.data.shape_keys is not None:
        dup.shape_key_clear()

    return dup


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    outdir = None
    if "--outdir" in argv:
        outdir = argv[argv.index("--outdir") + 1]

    if not outdir:
        outdir = os.path.dirname(os.path.abspath(__file__))

    return outdir


def main():
    outdir = parse_args()
    os.makedirs(outdir, exist_ok=True)

    clear_scene()

    body = build_lantern_body(SEGMENTS, PROFILE)

    if INCLUDE_HANDLE:
        handle = add_handle(HEIGHT)
        join_objects(body, handle)

    cleanup_geometry(body)

    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Centre the geometry on the object pivot so it sculpts and rotates like
    # the built-in sphere (see center_geometry docstring).
    center_geometry(body)

    if EXPORT_KEYS:
        add_shape_keys(body)
        path = os.path.join(outdir, "LanternBase.fbx")
        export_fbx(body, path, export_shape_keys=True)
        print("[Morphic] wrote", path)

    if EXPORT_STATIC:
        static = duplicate_without_shape_keys(body)
        path = os.path.join(outdir, "LanternBase_Static.fbx")
        export_fbx(static, path, export_shape_keys=False)
        print("[Morphic] wrote", path)
        bpy.data.objects.remove(static, do_unlink=True)

    if SAVE_BLEND:
        path = os.path.join(outdir, "LanternBase.blend")
        bpy.ops.wm.save_as_mainfile(filepath=path)
        print("[Morphic] wrote", path)


if __name__ == "__main__":
    main()
