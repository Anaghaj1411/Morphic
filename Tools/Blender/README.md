# Lantern Base Shape (Blender)

Procedurally generated **lantern** base shape for the Morphic sculpting app.

## Files

| File | Description |
|------|-------------|
| `create_lantern_base.py` | Generator script (mesh + shape keys + FBX export) |
| `verify_lantern_base.py` | Round-trip validation helper (imports an FBX and prints stats) |

Generated assets (written to `Assets/BaseShapes/Models/`):

| File | Blend shapes | Use with |
|------|--------------|----------|
| `LanternBase.fbx` | Yes (5) | Sculpt rig (`SculptShapeKeyController` / `SkinnedMeshRenderer`) |
| `LanternBase_Static.fbx` | No | `BaseShapeSelector` (static `MeshFilter` base) |
| `LanternBase.blend` | Yes | Editing / re-export in Blender |

Mesh stats: ~1058 verts, ~1104 polys, size **1.4 × 1.4 × 2.03** (X/Y/Z),
**centred on the origin** (Z spans -1.015 → 1.015).

> **Why centred?** Morphic's `PinchSculptBrush` maps your hand onto a sphere
> centred on the object's **pivot** (`transform.position`), and
> `FistRotateSculpture` rotates around that pivot. A mesh whose centre is offset
> from its pivot (e.g. a base resting on z=0) will sculpt in the wrong spot and
> swing out of frame when rotated. `center_geometry()` translates the mesh so its
> bounding-box centre sits on the origin, which makes the lantern behave exactly
> like the built-in sphere base.

## Shape key mapping

`SculptShapeKeyController.ResolveBlendShapeIndices()` finds blend shapes by
name (case-insensitive substring), so the names below match its defaults:

| Index | Name | Matches code search | Deformation |
|-------|------|---------------------|-------------|
| 0 | `Width` | `width` | Widens the belly horizontally |
| 1 | `Puff` | `puff` | Inflates the whole lantern |
| 2 | `BottomFlatten` | `flatten` | Squashes & widens the base |
| 3 | `TopGather` | `gather` | Pulls the top inward/down |
| 4 | `TopPinch` | `toppinch` / `top pinch` / `pinch` | Pinches the top into a peak |

## Regenerating (headless)

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
    --background --factory-startup `
    --python 'C:\Users\anagh\Documents\Morphic\Tools\Blender\create_lantern_base.py' `
    -- --outdir 'C:\Users\anagh\Documents\Morphic\Assets\BaseShapes\Models'
```

## Regenerating (interactive)

Open `create_lantern_base.py` in Blender's **Scripting** tab and press **Run**.
Output defaults to the script's folder; edit `--outdir` handling or the
`parse_args()` fallback to change it.

## Tuning

Edit the constants at the top of `create_lantern_base.py`:

- `SEGMENTS` – radial resolution (default 48)
- `HEIGHT` / `MAX_RADIUS` – overall size
- `INCLUDE_HANDLE` / `HANDLE_*` – the hanging ring
- `PROFILE` – the silhouette as `(radius, z)` points (the main shape knob)
- `SHAPE_KEYS` + `shape_*()` – blend shape deformations

The script is version-tolerant: it filters FBX exporter kwargs against the
installed Blender version, so it runs on Blender 4.x and 5.x.

## Unity integration

### Path A — Static base (BaseShapeSelector)

1. Let Unity import `LanternBase_Static.fbx` (default FBX import is fine).
2. Create a prefab under `Assets/BaseShapes/Prefabs/` (e.g. `LanternBase.prefab`)
   that contains the imported mesh as a child with a `MeshFilter` + `MeshRenderer`.
3. In the Inspector, assign it to `BaseShapeSelector`'s slots. To add a **5th**
   slot you also need a small code edit (see below) because `BaseShapeSelector`
   currently exposes 4 slots (sphere/bowl/cylinder/cube on keys 6–9).

Minimal code extension for a 5th slot (`BaseShapeSelector.cs`):

```csharp
[Header("Base Shape Templates")]
[SerializeField] private GameObject lanternBase;   // add

public void SelectLantern()               // add
{
    SetBaseShape(lanternBase, "Lantern");
}
```

Then call `SelectLantern()` from a UI button or add a key binding in `Update()`.

> `BaseShapeSelector.SetBaseShape()` copies the source mesh and calls
> `FitMeshToReferenceSize()`, so the lantern is auto-scaled to match the
> current reference clay — absolute FBX size does not matter.

### Path B — Sculpt rig (blend shapes / SkinnedMeshRenderer)

`LanternBase.fbx` imports as a mesh containing 5 blend shapes. Unity creates a
**SkinnedMeshRenderer** for meshes with blend shapes (even without a skeleton),
matching how `MasterClay.fbx` is set up in `SculptingTest.unity`.

1. In `SculptingTest.unity`, duplicate the existing clay object
   (the one with `SculptShapeKeyController`).
2. Swap its `SkinnedMeshRenderer` mesh to the imported `LanternBase` mesh.
3. `SculptShapeKeyController` will auto-resolve the indices by name at `Awake()`
   (`widthIndex` → `Width`, `puffIndex` → `Puff`, `bottomFlattenIndex` →
   `BottomFlatten`, `topGatherIndex` → `TopGather`, `topPinchIndex` → `TopPinch`).

No code changes are required for Path B.
