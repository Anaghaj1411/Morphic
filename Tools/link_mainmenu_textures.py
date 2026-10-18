import os
import re

ROOT = r"C:\Users\anagh\Documents\Morphic\Assets\Art\Environments\MainMenu"
TEX = os.path.join(ROOT, "Texture")
MAT = os.path.join(ROOT, "Materials")

NAME_TO_TEX = {
    "wood": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "wood.001": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "polished oak wood": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "polished oak wood.001": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "oak wood2": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "oak wood2.001": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "oak wood2.002": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "wood floor walnut": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "trunk": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "trunk.001": ("Wood_Diffuse(Oak).jpg", "Wood_Normal(LIght Oak).jpg"),
    "brick wall": ("brick.jpg", None),
    "brick wall.001": ("brick.jpg", None),
    "brick wall.002": ("brick.jpg", None),
    "brick wall.003": ("brick.jpg", None),
    "brick wall.004": ("brick.jpg", None),
    "brick wall.005": ("brick.jpg", None),
    "brick wall.006": ("brick.jpg", None),
    "brick wall.007": ("brick.jpg", None),
    "travertine marble": ("travertine2_BaseColor.jpg", "travertine2_Normal.jpg"),
    "travertine marble.001": ("travertine2_BaseColor.jpg", "travertine2_Normal.jpg"),
    "stylized grass": (
        "Stylized_HandpaintedGrass_02_basecolor.jpg",
        "Stylized_HandpaintedGrass_02_normalogl.jpg",
    ),
    "japanese shoji screen": ("Japanese_Shoji_Screen_Base_Color.jpg", None),
    "roof tiles": ("RoofingTiles008_2K_Color.jpg", None),
    "roof tiles.001": ("RoofingTiles008_2K_Color.jpg", None),
    "roof tiles.002": ("RoofingTiles008_2K_Color.jpg", None),
    "tiles": ("pavement03_diffuse.jpg", "pavement03_normal_opengl.jpg"),
    "tiles.001": ("pavement03_diffuse.jpg", "pavement03_normal_opengl.jpg"),
    "stone pavement": ("pavement03_diffuse.jpg", "pavement03_normal_opengl.jpg"),
    "stone pavement.001": ("pavement03_diffuse.jpg", "pavement03_normal_opengl.jpg"),
    "asphalt procedural": ("road.png", "garage_floor_nor_gl_4k.jpg"),
    "backgorund morphic": ("Backgorund Morphic.png", None),
    "morphic- start button": ("Morphic- Start Button 2.png", None),
    "morphic-settings": ("Morphic-Settings 2.png", None),
    "morphic-languae": ("Morphic-Languae 2.png", None),
    "morphic-2.logopng": ("Morphic-2.LOGOpng.png", None),
    "woven gingham fabric": ("cloth.jpg", None),
}


def guid_for(filename):
    meta = os.path.join(TEX, filename + ".meta")
    if not os.path.isfile(meta):
        return None
    with open(meta, encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


def canonical_name(stem):
    name = stem.lower()
    name = re.sub(r"\s+[12]$", "", name)
    return name


def tex_ref(guid):
    return "{fileID: 2800000, guid: %s, type: 3}" % guid


def set_map(text, prop, guid):
    pattern = r"(- %s:\r?\n        m_Texture: )\{fileID: 0\}" % prop
    return re.sub(pattern, r"\1" + tex_ref(guid), text, count=1)


def add_normal_keyword(text):
    if "_NORMALMAP" in text:
        return text
    empty = "  m_ValidKeywords: []"
    if empty in text:
        return text.replace(
            empty,
            "  m_ValidKeywords:\n  - _NORMALMAP",
            1,
        )
    filled = "  m_ValidKeywords:\n"
    if filled in text:
        return text.replace(filled, filled + "  - _NORMALMAP\n", 1)
    return text


patched = 0
skipped = 0
unmapped = []

for dirpath, _, files in os.walk(MAT):
    for name in files:
        if not name.endswith(".mat"):
            continue
        path = os.path.join(dirpath, name)
        stem = os.path.splitext(name)[0]
        key = canonical_name(stem)
        mapping = NAME_TO_TEX.get(key)
        if mapping is None:
            unmapped.append(stem)
            continue

        with open(path, encoding="utf-8") as f:
            text = f.read()

        if "m_Texture: {fileID: 2800000" in text.split("_BumpMap:")[0]:
            skipped += 1
            continue

        albedo, normal = mapping
        albedo_guid = guid_for(albedo)
        if not albedo_guid:
            unmapped.append(stem + " (missing " + albedo + ")")
            continue

        new_text = set_map(text, "_BaseMap", albedo_guid)
        new_text = set_map(new_text, "_MainTex", albedo_guid)
        if normal:
            normal_guid = guid_for(normal)
            if normal_guid:
                new_text = set_map(new_text, "_BumpMap", normal_guid)
                new_text = add_normal_keyword(new_text)

        if new_text != text:
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(new_text)
            patched += 1
        else:
            skipped += 1

print("patched", patched)
print("skipped", skipped)
print("unmapped count", len(unmapped))
generic = [n for n in unmapped if n.lower().startswith("material")]
named = [n for n in unmapped if not n.lower().startswith("material")]
print("named unmatched:", ", ".join(sorted(named)[:40]))
