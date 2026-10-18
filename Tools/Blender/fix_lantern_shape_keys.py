# Paste into Blender: Scripting workspace → New → Run Script
# Select the lantern mesh first.
#
# - Sliders stay 0..1
# - Height = straight up, Width = sideways
# - Top circle pop goes STRAIGHT up the lantern axis
#   and lands flush on the body peak
# - Height lifts the lid so it stays on the peak
# - Circle lid is sized to the rectangle lid
# - Width grows that same lid, like Height lifts it

import bpy
from mathutils import Vector

SKIP_NAMES = ("BASIS",)
HEIGHT_BOOST = 1.16
# Leave False now that the lid size is right. Only Height/Width follow is applied.
RESHAPE_LID = False
# Height is already right. Do not keep nudging the rectangle.
RECTANGLE_LIFT_FRAC = 0.0
RECTANGLE_HEIGHT_EXTRA = 1.0


def selected_mesh():
    obj = bpy.context.object
    if obj is None or obj.type != "MESH":
        raise RuntimeError("Select the lantern mesh object first.")
    if obj.data.shape_keys is None:
        raise RuntimeError("This mesh has no shape keys.")
    return obj


def find_key(keys, *needles):
    for key in keys:
        compact = key.name.upper().replace(" ", "").replace("_", "")
        if any(needle in compact for needle in needles):
            return key
    return None


def detect_up(basis_coords, height_key):
    if height_key is not None:
        total = Vector((0.0, 0.0, 0.0))
        for index, dest in enumerate(height_key.data):
            total += Vector(dest.co) - basis_coords[index]
        if total.length > 1e-8:
            return total.normalized()
    xs = [v.x for v in basis_coords]
    ys = [v.y for v in basis_coords]
    zs = [v.z for v in basis_coords]
    spans = (
        (max(xs) - min(xs), Vector((1.0, 0.0, 0.0))),
        (max(ys) - min(ys), Vector((0.0, 1.0, 0.0))),
        (max(zs) - min(zs), Vector((0.0, 0.0, 1.0))),
    )
    return sorted(spans, key=lambda item: item[0], reverse=True)[0][1]


def project_along_axis(delta, axis):
    return axis * delta.dot(axis)


def project_off_axis(delta, axis):
    return delta - project_along_axis(delta, axis)


def moving_indices(key, basis_coords, min_ratio=0.18):
    lengths = []
    for index, dest in enumerate(key.data):
        lengths.append((Vector(dest.co) - basis_coords[index]).length)
    peak = max(lengths) if lengths else 0.0
    if peak < 1e-8:
        return []
    cutoff = peak * min_ratio
    return [index for index, length in enumerate(lengths) if length >= cutoff]


def mean_point(points):
    total = Vector((0.0, 0.0, 0.0))
    for point in points:
        total += point
    return total / float(len(points))


def radial(point, up):
    return point - up * point.dot(up)


def planar_center(points, up):
    helper = Vector((1.0, 0.0, 0.0)) if abs(up.x) < 0.9 else Vector((0.0, 1.0, 0.0))
    axis_a = up.cross(helper).normalized()
    axis_b = up.cross(axis_a).normalized()
    sides = [radial(point, up) for point in points]
    a_vals = [side.dot(axis_a) for side in sides]
    b_vals = [side.dot(axis_b) for side in sides]
    return axis_a * ((min(a_vals) + max(a_vals)) * 0.5) + axis_b * ((min(b_vals) + max(b_vals)) * 0.5)


def max_radius(points, up):
    return max((radial(point, up).length for point in points), default=0.0)


def body_width_scale(width_key, basis_coords, body, up):
    ratios = []
    for index in body:
        rest_side = radial(basis_coords[index], up)
        if rest_side.length < 1e-6:
            continue
        wide_side = radial(Vector(width_key.data[index].co), up)
        ratios.append(wide_side.length / rest_side.length)
    if not ratios:
        return 1.0
    ratios.sort()
    mid = ratios[len(ratios) // 4 : 3 * len(ratios) // 4] or ratios
    return sum(mid) / float(len(mid))


def lid_bounds(points, up):
    radii = [radial(point, up).length for point in points]
    heights = [point.dot(up) for point in points]
    return max(radii, default=0.0), min(heights, default=0.0), max(heights, default=0.0)


def reference_points(ref_key, basis_coords):
    if ref_key is None:
        return []
    return [Vector(ref_key.data[index].co) for index in moving_indices(ref_key, basis_coords)]


def land_ring(pop_key, basis_coords, up, toward_top, height_key, width_key, size_ref_key):
    lid = moving_indices(pop_key, basis_coords)
    if not lid:
        print("MORPHIC: No moving verts on", pop_key.name)
        return

    body = [index for index in range(len(basis_coords)) if index not in set(lid)]
    if not body:
        print("MORPHIC: No body verts for", pop_key.name)
        return

    lid_now = [Vector(pop_key.data[index].co) for index in lid]
    body_points = [basis_coords[index] for index in body]
    axis = planar_center(body_points, up)
    lateral = planar_center(lid_now, up) - axis
    lid_extent = min((point.dot(up) for point in lid_now), default=0.0) if toward_top else max(
        (point.dot(up) for point in lid_now), default=0.0
    )
    body_extent = max((basis_coords[index].dot(up) for index in body), default=0.0) if toward_top else min(
        (basis_coords[index].dot(up) for index in body), default=0.0
    )
    landed = [point - lateral + up * (body_extent - lid_extent) for point in lid_now]
    max_r, _, _ = lid_bounds(landed, up)

    ref_points = reference_points(size_ref_key, basis_coords)
    if ref_points:
        target_r, ref_low, ref_high = lid_bounds(ref_points, up)
        sit = ref_low if toward_top else ref_high
        target_h = abs(ref_high - ref_low) * HEIGHT_BOOST
        print("MORPHIC: Size ref", size_ref_key.name, "radius", round(target_r, 5), "height", round(target_h, 5))
    else:
        target_r, sit, target_h = max_r, body_extent, None
        print("MORPHIC: No rectangle size ref for", pop_key.name)

    cur_r, cur_low, cur_high = lid_bounds(landed, up)
    r_fit = (target_r / cur_r) if target_r and cur_r > 1e-8 else 1.0
    cur_h = abs(cur_high - cur_low)
    h_fit = (target_h / cur_h) if target_h and cur_h > 1e-8 else HEIGHT_BOOST
    fitted = []
    for point in landed:
        height = point.dot(up)
        new_h = sit + (height - cur_low) * h_fit if toward_top else sit - (cur_high - height) * h_fit
        fitted.append(up * new_h + radial(point, up) * r_fit)
    recenter = axis - planar_center(fitted, up)
    fitted = [point + recenter for point in fitted]

    for dest in pop_key.data:
        dest.co = Vector(dest.co)
    for index in body:
        pop_key.data[index].co = basis_coords[index].copy()
    for index, point in zip(lid, fitted):
        pop_key.data[index].co = point

    attach_follow(lid, fitted, body, basis_coords, up, toward_top, height_key, width_key, None)
    print(
        "MORPHIC:",
        pop_key.name,
        "landed on axis at",
        "top" if toward_top else "bottom",
        "lid verts",
        len(lid),
    )


def attach_follow(lid, fitted, body, basis_coords, up, toward_top, height_key, width_key, topper_key):
    if height_key is not None:
        cap = []
        for index in body:
            rise = (Vector(height_key.data[index].co) - basis_coords[index]).dot(up)
            height = basis_coords[index].dot(up)
            cap.append((height, rise))
        cap.sort(key=lambda item: item[0], reverse=toward_top)
        sample = cap[: max(1, len(cap) // 12)]
        ride = sum(item[1] for item in sample) / float(len(sample))
        for index in lid:
            rest = basis_coords[index]
            height_key.data[index].co = rest + up * ride
        print("MORPHIC: Height ride", round(ride, 5))

    if width_key is not None:
        body_r0 = max((radial(basis_coords[index], up).length for index in body), default=0.0)
        body_r1 = max((radial(Vector(width_key.data[index].co), up).length for index in body), default=0.0)
        grow = (body_r1 / body_r0) if body_r0 > 1e-8 else 1.0
        if abs(grow - 1.0) < 0.01:
            grow = body_width_scale(width_key, basis_coords, body, up)
        for index, point in zip(lid, fitted):
            rest = basis_coords[index]
            width_key.data[index].co = rest + radial(point, up) * (grow - 1.0)
        print("MORPHIC: Width grow", round(grow, 4), "body", round(body_r0, 5), "->", round(body_r1, 5))

    if topper_key is not None and toward_top:
        cap = []
        for index in body:
            cap.append((basis_coords[index].dot(up), index))
        cap.sort(key=lambda item: item[0], reverse=True)
        sample = [index for _, index in cap[: max(1, len(cap) // 12)]]
        shift = Vector((0.0, 0.0, 0.0))
        rest_r = []
        topper_r = []
        for index in sample:
            rest = basis_coords[index]
            dest = Vector(topper_key.data[index].co)
            shift += dest - rest
            rest_side = radial(rest, up).length
            if rest_side > 1e-6:
                rest_r.append(rest_side)
                topper_r.append(radial(dest, up).length)
        shift /= float(len(sample))
        grow = (sum(topper_r) / sum(rest_r)) if rest_r and sum(rest_r) > 1e-8 else 1.0
        for index, point in zip(lid, fitted):
            rest = basis_coords[index]
            topper_key.data[index].co = rest + shift + radial(point, up) * (grow - 1.0)
        print("MORPHIC: Topper follow shift", tuple(round(v, 5) for v in shift), "grow", round(grow, 4))


def follow_existing(pop_key, basis_coords, up, toward_top, height_key, width_key, topper_key):
    lid = moving_indices(pop_key, basis_coords)
    if not lid:
        print("MORPHIC: No moving verts on", pop_key.name)
        return
    body = [index for index in range(len(basis_coords)) if index not in set(lid)]
    fitted = [Vector(pop_key.data[index].co) for index in lid]
    attach_follow(lid, fitted, body, basis_coords, up, toward_top, height_key, width_key, topper_key)
    print("MORPHIC: Kept", pop_key.name, "and attached Height/Width/Topper follow")


def raise_piece(pop_key, basis_coords, up, toward_top, height_key, lift_frac, ride_boost):
    lid = moving_indices(pop_key, basis_coords)
    if not lid:
        return
    points = [Vector(pop_key.data[index].co) for index in lid]
    low = min((point.dot(up) for point in points), default=0.0)
    high = max((point.dot(up) for point in points), default=0.0)
    lift = abs(high - low) * lift_frac
    direction = up if toward_top else -up
    for index, point in zip(lid, points):
        pop_key.data[index].co = point + direction * lift
    if height_key is not None and ride_boost != 1.0:
        for index in lid:
            rest = basis_coords[index]
            current = Vector(height_key.data[index].co)
            height_key.data[index].co = rest + (current - rest) * ride_boost
    print("MORPHIC: Raised", pop_key.name, "lift", round(lift, 5), "height extra", ride_boost)


def main():
    obj = selected_mesh()
    keys = obj.data.shape_keys.key_blocks
    basis = keys.get("Basis") or keys.get("basis") or keys[0]
    basis_coords = [Vector(v.co) for v in basis.data]
    height_key = find_key(keys, "HEIGHT")
    width_key = find_key(keys, "WIDTH")
    top_pop = find_key(keys, "TOPCIRCLEPOP", "TOPCIRCLE")
    bottom_pop = find_key(keys, "CIRCLEBOTTOMPOP", "BOTTOMCIRCLE")
    top_rect = find_key(keys, "TOPRECTANGLE")
    bottom_rect = find_key(keys, "RECTANGLEBOTTOM", "BOTTOMRECTANGLE")
    topper_key = find_key(keys, "TOPPER")
    up = detect_up(basis_coords, height_key)

    print("MORPHIC: Up axis is", tuple(up))
    print("MORPHIC: Keys", [key.name for key in keys])
    print("MORPHIC: Circle", getattr(top_pop, "name", None), "Rect", getattr(top_rect, "name", None))

    for key in keys:
        if key.name.upper() in SKIP_NAMES:
            key.slider_min = 0.0
            key.slider_max = 1.0
            key.value = 0.0
            continue

        old_max = key.slider_max if key.slider_max > 0.0 else 1.0
        bake_scale = old_max if old_max > 1.001 else 1.0
        squeeze = key is height_key or key is width_key

        for index, dest in enumerate(key.data):
            rest = basis_coords[index]
            delta = (Vector(dest.co) - rest) * bake_scale
            if squeeze and key is height_key:
                delta = project_along_axis(delta, up)
            elif squeeze and key is width_key:
                delta = project_off_axis(delta, up)
            dest.co = rest + delta

        key.slider_min = 0.0
        key.slider_max = 1.0
        key.value = 0.0
        print("MORPHIC: Normalized", key.name, "old max", old_max)

    tops = ((top_pop, top_rect, True), (bottom_pop, bottom_rect, False))
    if RESHAPE_LID:
        for pop_key, ref_key, toward_top in tops:
            if pop_key is not None:
                land_ring(pop_key, basis_coords, up, toward_top, height_key, width_key, ref_key)
        if top_rect is not None:
            follow_existing(top_rect, basis_coords, up, True, height_key, width_key, topper_key)
        if bottom_rect is not None:
            follow_existing(bottom_rect, basis_coords, up, False, height_key, width_key, None)
    else:
        for pop_key, toward_top, follow_topper in (
            (top_pop, True, topper_key),
            (top_rect, True, topper_key),
            (bottom_pop, False, None),
            (bottom_rect, False, None),
        ):
            if pop_key is not None:
                follow_existing(pop_key, basis_coords, up, toward_top, height_key, width_key, follow_topper)

    if top_rect is not None and RECTANGLE_LIFT_FRAC != 0.0:
        raise_piece(
            top_rect,
            basis_coords,
            up,
            True,
            height_key,
            RECTANGLE_LIFT_FRAC,
            RECTANGLE_HEIGHT_EXTRA,
        )

    bpy.context.view_layer.update()
    print("MORPHIC: Circle and rectangle tops follow Height, Width, and Topper.")


if __name__ == "__main__":
    main()
