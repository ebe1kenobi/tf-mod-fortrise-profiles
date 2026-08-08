#!/usr/bin/env python3
"""Verifie que l'assemblage de la forge donne bien les planches attendues.

Le mod `FR5tf-mod-fortrise-archer-brones` contient un archer complet, fabrique a la
main puis eprouve en jeu. Ses planches sont donc une reference : si la forge, partie
de la meme planche source et des memes coordonnees, ne les retrouve pas au pixel
pres, c'est la forge qui a tort.

Ce script transcrit le calcul de `VSCode/Core/ForgeBuild.cs` - pas celui de
`forge_preview.py` - et le fait partir du VIVIER decoupe, comme le C# en jeu. Il
verifie donc la chaine entiere : case du vivier, fenetre de decoupe, accolement,
teintes d'equipe, silhouette de flash, portraits.

Toute divergence est un bug de l'un des deux cotes, jamais une preference. Quand
`ForgeBuild.cs` change, ce fichier change avec lui ou la verification ne veut plus
rien dire.

Usage :
    python forge_verify.py
    python forge_verify.py --bank ... --ref ...
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

from forge_preview import BODY_SLOTS, CORPSE_SLOTS, FRAME, LAYOUT, WINDOW


DEFAULT_BANK = Path(r"D:\__dev\code\archive\tf-archer\sprites")
DEFAULT_REF = Path(
    r"D:\__dev\code\FR5tf-mod-fortrise-archer-brones\ModFile\Content\Atlas\sprites"
)

# Les deux archers du mod de reference, et la planche dont chacun sort.
CASES = [("indiannabrones_anim", "Brones"), ("BRODREAD_anim", "Brones_Alt")]

RED_HUE = 0.0
BLUE_HUE = 220.0
TINT_MIN_VALUE = 40
TINT_MIN_CHROMA = 12


# --- transcription de ForgeBuild.cs -----------------------------------------


def read_pose(bank: Path, sheet: str, key: str) -> np.ndarray | None:
    """ForgeBuild.ReadPose + ForgeBuild.Window."""
    col, row = LAYOUT[key]
    path = bank / sheet / f"r{row:02d}c{col:02d}.png"

    if not path.exists():
        return None

    cell = np.array(Image.open(path).convert("RGBA"))
    height, width = cell.shape[:2]
    window = np.zeros((FRAME, FRAME, 4), np.uint8)

    for y in range(FRAME):
        source_y = WINDOW[1] + y
        if not 0 <= source_y < height:
            continue

        for x in range(FRAME):
            source_x = WINDOW[0] + x
            if 0 <= source_x < width and cell[source_y, source_x, 3] != 0:
                window[y, x] = cell[source_y, source_x]

    return window


def assemble(bank: Path, sheet: str, slots, fallback: np.ndarray):
    """ForgeBuild.Assemble."""
    strip = np.zeros((FRAME, len(slots) * FRAME, 4), np.uint8)
    substituted: list[str] = []

    for slot in slots:
        pose = read_pose(bank, sheet, slot.key)
        if pose is None:
            pose = fallback
            substituted.append(slot.key)

        strip[:, slot.index * FRAME : (slot.index + 1) * FRAME] = pose

    return strip, substituted


def shift_hue(pixel: np.ndarray, hue: float) -> np.ndarray:
    """ForgeBuild.ShiftHue."""
    r, g, b, a = (int(v) for v in pixel)
    high = max(r, g, b)
    low = min(r, g, b)

    if high < TINT_MIN_VALUE or high - low < TINT_MIN_CHROMA:
        return pixel

    value = high / 255.0
    saturation = 0.0 if high == 0 else (high - low) / high

    sector = (hue / 60.0) % 6
    index = int(sector)
    fraction = sector - index

    p = value * (1 - saturation)
    q = value * (1 - fraction * saturation)
    t = value * (1 - (1 - fraction) * saturation)

    rr, gg, bb = [
        (value, t, p),
        (q, value, p),
        (p, value, t),
        (p, q, value),
        (t, p, value),
        (value, p, q),
    ][index]

    return np.array([round(rr * 255), round(gg * 255), round(bb * 255), a], np.uint8)


def tinted(image: np.ndarray, hue: float) -> np.ndarray:
    out = np.zeros_like(image)

    for y in range(image.shape[0]):
        for x in range(image.shape[1]):
            if image[y, x, 3]:
                out[y, x] = shift_hue(image[y, x], hue)

    return out


def silhouette(image: np.ndarray) -> np.ndarray:
    out = np.zeros_like(image)
    opaque = image[:, :, 3] > 0
    out[:, :, 0][opaque] = 255
    out[:, :, 1][opaque] = 255
    out[:, :, 2][opaque] = 255
    out[:, :, 3][opaque] = image[:, :, 3][opaque]
    return out


def enlarged(idle: np.ndarray, width: int, height: int, scale: int) -> np.ndarray:
    out = np.zeros((height, width, 4), np.uint8)
    drawn_w = idle.shape[1] * scale
    drawn_h = idle.shape[0] * scale
    offset_x = (width - drawn_w) // 2
    offset_y = height - drawn_h

    for y in range(drawn_h):
        ty = offset_y + y
        if not 0 <= ty < height:
            continue

        for x in range(drawn_w):
            tx = offset_x + x
            if not 0 <= tx < width:
                continue

            pixel = idle[y // scale, x // scale]
            if pixel[3]:
                out[ty, tx] = pixel

    return out


# --- comparaison -------------------------------------------------------------


class Report:
    def __init__(self) -> None:
        self.checked = 0
        self.failed: list[str] = []

    def compare(self, label: str, built: np.ndarray, reference: Path) -> None:
        self.checked += 1

        if not reference.exists():
            self.failed.append(f"{label} : reference absente")
            print(f"  ABSENTE        {label}")
            return

        ref = np.array(Image.open(reference).convert("RGBA"))

        if ref.shape != built.shape:
            self.failed.append(f"{label} : {ref.shape} attendu, {built.shape} obtenu")
            print(f"  TAILLE         {label}  {ref.shape} vs {built.shape}")
            return

        diff = np.abs(ref.astype(int) - built.astype(int)).sum(axis=2)
        count = int((diff > 0).sum())

        if count:
            self.failed.append(f"{label} : {count} pixels")
            print(f"  DIFF {count:5} px  {label}")
        else:
            print(f"  identique      {label}")


def verify(bank: Path, ref: Path) -> Report:
    report = Report()

    for sheet, name in CASES:
        print(f"{name}  <-  {sheet}")

        fallback = read_pose(bank, sheet, "stand")
        if fallback is None:
            report.failed.append(f"{name} : pose debout introuvable dans le vivier")
            print("  pose debout introuvable, planche ignoree")
            continue

        body, missing_body = assemble(bank, sheet, BODY_SLOTS, fallback)
        corpse, missing_corpse = assemble(bank, sheet, CORPSE_SLOTS, fallback)

        missing = missing_body + missing_corpse
        if missing:
            # Un emplacement comble par la pose debout ne peut pas ressembler a la
            # reference : le signaler evite de chercher un bug de teinte la ou il n'y
            # a qu'une image absente du vivier.
            print("  poses absentes du vivier, comblees : " + ", ".join(missing))

        idle = body[:, :FRAME]

        report.compare(f"{name}.png", body, ref / "player" / f"{name}.png")
        report.compare(f"{name}_red.png", tinted(body, RED_HUE), ref / "player" / f"{name}_red.png")
        report.compare(f"{name}_blue.png", tinted(body, BLUE_HUE), ref / "player" / f"{name}_blue.png")

        corpses = ref / "player" / "corpses"
        report.compare(f"{name}-normal.png", corpse, corpses / f"{name}-normal.png")
        report.compare(f"{name}-redTeam.png", tinted(corpse, RED_HUE), corpses / f"{name}-redTeam.png")
        report.compare(f"{name}-blueTeam.png", tinted(corpse, BLUE_HUE), corpses / f"{name}-blueTeam.png")
        report.compare(f"{name}-flash.png", silhouette(corpse), corpses / f"{name}-flash.png")

        portraits = ref / "portraits"
        report.compare(f"joined{name}.png", enlarged(idle, 60, 120, 4), portraits / f"joined{name}.png")
        report.compare(f"notJoined{name}.png", enlarged(idle, 60, 120, 4), portraits / f"notJoined{name}.png")
        report.compare(f"win{name}.png", enlarged(idle, 50, 50, 2), portraits / f"win{name}.png")
        report.compare(f"lose{name}.png", enlarged(idle, 50, 50, 2), portraits / f"lose{name}.png")

        print()

    return report


def main() -> int:
    parser = argparse.ArgumentParser(description="Verifie l'assemblage de la forge.")
    parser.add_argument("--bank", type=Path, default=DEFAULT_BANK)
    parser.add_argument("--ref", type=Path, default=DEFAULT_REF)
    args = parser.parse_args()

    if not args.bank.is_dir():
        print(f"Vivier introuvable : {args.bank}", file=sys.stderr)
        return 1

    report = verify(args.bank, args.ref)

    if report.failed:
        print(f"{len(report.failed)} ecart(s) sur {report.checked} planches :")
        for line in report.failed:
            print(f"  {line}")
        return 1

    print(f"{report.checked} planches identiques a la reference.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
