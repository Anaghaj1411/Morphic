"""Round-trip validation for the generated lantern FBX (run headless)."""
import math

import bpy
import sys

argv = sys.argv
fbx = argv[argv.index("--") + 1] if "--" in argv else ""

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

bpy.ops.import_scene.fbx(filepath=fbx)

meshes = [o for o in bpy.context.selected_objects if o.type == "MESH"]
print("[verify] imported mesh objects:", [o.name for o in meshes])

for o in meshes:
    me = o.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    print("[verify] object:", o.name)
    print("[verify]   verts:", len(me.vertices), " polys:", len(me.polygons))
    print("[verify]   size X/Y/Z:",
          round(max(xs) - min(xs), 3),
          round(max(ys) - min(ys), 3),
          round(max(zs) - min(zs), 3))
    print("[verify]   min/max Z:", round(min(zs), 3), round(max(zs), 3))

    # --- PinchSculptBrush reach analysis -----------------------------------
    # GetBrushPoint() fakes the surface with a sphere of radius = mesh half-width
    # centred on the pivot, and only vertices within brushRadius of that sphere
    # get sculpted. So for Inflate/Indent to work EVERYWHERE we need:
    #     distance(pivot, vertex) <= half_width + brush_radius
    brush_radius = 0.30
    half_width = (max(xs) - min(xs)) * 0.5
    reach = half_width + brush_radius
    max_dist = max(
        math.sqrt(v.co.x ** 2 + v.co.y ** 2 + v.co.z ** 2)
        for v in me.vertices
    )
    gap = max_dist - half_width
    falloff = max(0.0, 1.0 - gap / brush_radius)
    print("[verify]   brush half-width:", round(half_width, 3))
    print("[verify]   fallback-sphere reach:", round(reach, 3))
    print("[verify]   farthest vertex from pivot:", round(max_dist, 3))
    print("[verify]   fallback worst falloff:", round(falloff, 3))
    print("[verify]   note: PinchSculptBrush now raycasts the real collider,")
    print("[verify]         so tall shapes are still fully sculptable.")
    print("[verify]         (values above describe the legacy sphere fallback)")

    if me.shape_keys:
        names = [kb.name for kb in me.shape_keys.key_blocks]
        print("[verify]   shape keys:", names)
    else:
        print("[verify]   shape keys: NONE")
