#!/usr/bin/env python3
"""Pull RoundedRect Texture2D/Sprite blobs out of SculptingLab.unity."""
from __future__ import annotations

import math
import re
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets" / "SculptingEnhancement" / "Scenes" / "SculptingLab.unity"
PNG = ROOT / "Assets" / "Resources" / "MorphicUI" / "RoundedRect.png"
SPRITE_REF = "{fileID: 21300000, guid: b8e4f17c6a9d4e2f8c1a5b7d3e0f2946, type: 3}"
TEX_REF = "{fileID: 2800000, guid: b8e4f17c6a9d4e2f8c1a5b7d3e0f2946, type: 3}"


def write_rounded_png(path: Path, size: int = 96, radius: float = 18.0) -> None:
    rows = []
    for y in range(size):
        row = bytearray()
        for x in range(size):
            px = x + 0.5
            py = y + 0.5
            cx = min(max(px, radius), size - radius)
            cy = min(max(py, radius), size - radius)
            dist = math.hypot(px - cx, py - cy)
            alpha = max(0.0, min(1.0, radius - dist + 0.5))
            row.extend((255, 255, 255, int(round(alpha * 255.0))))
        rows.append(bytes(row))

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    raw = b"".join(b"\x00" + row for row in rows)
    png = b"".join(
        (
            b"\x89PNG\r\n\x1a\n",
            chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)),
            chunk(b"IDAT", zlib.compress(raw, 9)),
            chunk(b"IEND", b""),
        )
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def strip_scene(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    parts = re.split(r"(?=^--- !u!)", text, flags=re.M)
    header = parts[0]
    sprite_ids = []
    tex_ids = []
    kept = [header]
    dropped_sprites = 0
    dropped_textures = 0

    for doc in parts[1:]:
        match = re.match(r"--- !u!(\d+) &(-?\d+)", doc)
        if match is None:
            kept.append(doc)
            continue
        type_id, file_id = match.group(1), match.group(2)
        head = doc[:1200]
        if type_id == "213" and "m_Name: RoundedRect" in head:
            sprite_ids.append(file_id)
            dropped_sprites += 1
            continue
        if type_id == "28" and "image data:" in head and (
            "m_Width: 96" in head or "m_CompleteImageSize: 36864" in head
        ):
            tex_ids.append(file_id)
            dropped_textures += 1
            continue
        kept.append(doc)

    out = "".join(kept)
    for file_id in sprite_ids:
        out = re.sub(r"\{fileID: " + file_id + r"\}", SPRITE_REF, out)
    for file_id in tex_ids:
        out = re.sub(r"\{fileID: " + file_id + r"\}", TEX_REF, out)

    path.write_text(out, encoding="utf-8")
    print(
        f"Dropped sprites={dropped_sprites} textures={dropped_textures} "
        f"bytes {len(text)} -> {len(out)}"
    )


if __name__ == "__main__":
    write_rounded_png(PNG)
    print("Wrote", PNG, "bytes", PNG.stat().st_size)
    strip_scene(SCENE)
