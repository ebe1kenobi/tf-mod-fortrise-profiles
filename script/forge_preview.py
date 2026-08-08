#!/usr/bin/env python3
"""Prototype de l'assemblage de la forge, hors du jeu.

Le C# de la forge fera exactement ce que fait ce script : prendre seize images
choisies dans le vivier, les accoler en planches TowerFall, et en deriver les
variantes d'equipe, le flash, les portraits et la statue.

Ce script existe pour une seule raison : l'assemblage est la partie ou l'on se
trompe d'un pixel, et on ne debogue pas un pixel a travers un menu a la manette.
Il ecrit les planches sur le disque et une image de controle ou l'on voit, cote a
cote, ce que le jeu affichera. Les constantes validees ici sont ensuite figees
dans ForgeSlots.cs et ForgeLayout.cs - elles ne doivent diverger nulle part.

Usage :
    python forge_preview.py --sheet BRONAN_anim
    python forge_preview.py --sheet BroLee_anim --window 3,7 --frame 24
    python forge_preview.py --list
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image


DEFAULT_BANK = Path(r"D:\__dev\code\archive\tf-archer\sprites")
DEFAULT_OUT = Path(r"D:\__dev\code\archive\tf-archer\forge")

# Case Broforce. Le vivier la porte dans index.json, mais la table de coordonnees
# ci-dessous n'a de sens que pour cette taille-la.
SOURCE_CELL = 32

# Image TowerFall. 24 et non les 20 des archers d'origine : le personnage
# Broforce fait 13 pixels de large debout, mais ses bras tendus vont jusqu'a 16,
# et une planche de 20 les rognait.
FRAME = 24

# Fenetre de decoupe dans la case source, calee sur le personnage debout : pieds a
# y=30, sommet du chapeau a y=11. La meme pour toutes les poses, sans quoi le
# personnage sauterait d'une image a l'autre au lieu de marcher.
WINDOW = (3, 7)


# ---------------------------------------------------------------------------
# Les seize emplacements
# ---------------------------------------------------------------------------


@dataclass(frozen=True)
class Slot:
    """Un emplacement a remplir, et sa place dans la planche de destination."""

    key: str
    sheet: str  # "body" ou "corpse"
    index: int  # rang dans la planche accolee
    label: str


# L'ordre est celui que spriteData.xml indexe. Le changer decale toutes les
# animations : "run" pointe sur les images 2,1,2,3 et rien ne le rappelle ailleurs.
BODY_SLOTS = [
    Slot("stand", "body", 0, "DEBOUT"),
    Slot("run1", "body", 1, "COURSE 1"),
    Slot("run2", "body", 2, "COURSE 2"),
    Slot("run3", "body", 3, "COURSE 3"),
    Slot("ledge", "body", 4, "REBORD"),
    Slot("jump", "body", 5, "SAUT"),
    Slot("fall", "body", 6, "CHUTE"),
    Slot("dodge", "body", 7, "ESQUIVE"),
    Slot("slide", "body", 8, "GLISSADE"),
    Slot("duck", "body", 9, "ACCROUPI"),
]

CORPSE_SLOTS = [
    Slot("corpse_ground", "corpse", 0, "MORT AU SOL"),
    Slot("corpse_fall", "corpse", 1, "MORT EN CHUTE"),
    Slot("corpse_pinned", "corpse", 2, "MORT CLOUE"),
    Slot("corpse_slouched", "corpse", 3, "MORT AFFAISSE"),
    Slot("corpse_flying", "corpse", 4, "MORT PROJETE"),
    Slot("corpse_ledge", "corpse", 5, "MORT SUR REBORD"),
]

SLOTS = BODY_SLOTS + CORPSE_SLOTS


# ---------------------------------------------------------------------------
# La mise en page canonique des planches Broforce
# ---------------------------------------------------------------------------

# Verifiee sur huit personnages : les memes cases donnent les memes poses d'une
# planche a l'autre. C'est ce qui permet de pre-remplir les seize emplacements
# d'un coup au lieu de faire fouiller dans trente mille images.
#
# Une case peut etre vide chez un personnage donne - le dodge de BROBOCOP l'est.
# L'emplacement reste alors a remplir a la main, et la forge doit le signaler
# plutot que de livrer une image transparente sans rien dire.
LAYOUT = {
    "stand": (0, 0),
    "run1": (1, 1),
    "run2": (3, 1),
    "run3": (5, 1),
    "ledge": (16, 3),
    "jump": (21, 4),
    "fall": (22, 4),
    "dodge": (21, 1),
    "slide": (11, 5),
    "duck": (12, 1),
    "corpse_ground": (16, 7),
    "corpse_fall": (16, 6),
    "corpse_pinned": (17, 6),
    "corpse_slouched": (17, 7),
    "corpse_flying": (19, 6),
    "corpse_ledge": (18, 7),
}


# ---------------------------------------------------------------------------
# Teinte
# ---------------------------------------------------------------------------


def shift_hue(pixel: tuple[int, int, int, int], hue: float) -> tuple[int, int, int, int]:
    """Impose une teinte en gardant saturation et valeur.

    Deplacer la teinte plutot que poser un aplat : une equipe se reconnait a sa
    couleur, mais le personnage doit garder ses ombres et ses contours.
    """
    r, g, b, a = pixel
    high = max(r, g, b)
    low = min(r, g, b)

    # Le noir et les gris n'ont pas de teinte a deplacer. Les contours et les yeux
    # doivent rester ce qu'ils sont, sinon le personnage devient une silhouette
    # coloree sans regard.
    if high < 40 or (high - low) < 12:
        return pixel

    v = high / 255.0
    s = 0.0 if high == 0 else (high - low) / high

    sector = (hue / 60.0) % 6
    i = int(sector)
    f = sector - i
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)

    table = [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]
    rr, gg, bb = table[i]

    # Arrondi et non troncature : un canal calcule a 32.9995 vaut 33, et tronquer
    # le rend a 32 sur des centaines de pixels par planche. C'est invisible a l'oeil
    # et fatal a une comparaison au pixel pres - donc a la seule verification qui
    # dise que le C# et ce prototype font bien la meme chose.
    return (round(rr * 255), round(gg * 255), round(bb * 255), a)


def tinted(sheet: Image.Image, hue: float) -> Image.Image:
    out = Image.new("RGBA", sheet.size)
    source = sheet.load()
    target = out.load()

    for y in range(sheet.height):
        for x in range(sheet.width):
            pixel = source[x, y]
            if pixel[3] == 0:
                continue
            target[x, y] = shift_hue(pixel, hue)

    return out


def silhouette(sheet: Image.Image) -> Image.Image:
    """Silhouette blanche, superposee par le jeu au moment du coup."""
    out = Image.new("RGBA", sheet.size)
    source = sheet.load()
    target = out.load()

    for y in range(sheet.height):
        for x in range(sheet.width):
            alpha = source[x, y][3]
            if alpha:
                target[x, y] = (255, 255, 255, alpha)

    return out


# ---------------------------------------------------------------------------
# Assemblage
# ---------------------------------------------------------------------------


@dataclass
class Design:
    """L'archer en cours d'assemblage."""

    name: str
    source: Path
    window: tuple[int, int] = WINDOW
    frame: int = FRAME
    cell: int = SOURCE_CELL
    hue: float = 30.0
    picks: dict[str, tuple[int, int]] = field(default_factory=dict)

    def fill_from_layout(self) -> None:
        self.picks = dict(LAYOUT)


def cut(sheet: Image.Image, design: Design, col: int, row: int) -> Image.Image:
    """Une pose, decoupee dans la fenetre constante."""
    x = col * design.cell + design.window[0]
    y = row * design.cell + design.window[1]
    return sheet.crop((x, y, x + design.frame, y + design.frame))


def assemble(sheet: Image.Image, design: Design, slots: list[Slot]) -> tuple[Image.Image, list[str]]:
    """Accole les poses d'une planche. Rend aussi les emplacements restes vides."""
    strip = Image.new("RGBA", (len(slots) * design.frame, design.frame), (0, 0, 0, 0))
    empty: list[str] = []

    for slot in slots:
        pick = design.picks.get(slot.key)
        if pick is None:
            empty.append(slot.key)
            continue

        tile = cut(sheet, design, pick[0], pick[1])
        if not tile.getbbox():
            empty.append(slot.key)
            continue

        strip.paste(tile, (slot.index * design.frame, 0))

    return strip, empty


def scaled_onto(tile: Image.Image, width: int, height: int, scale: float) -> Image.Image:
    """Pose une image agrandie, calee en bas et centree.

    Les portraits et la statue sont des agrandissements de la pose debout. Les
    archers d'origine ont des bustes dessines a la main, qu'on ne peut pas deduire
    d'un sprite de treize pixels - c'est le seul endroit ou l'agrandissement se
    voit, et il n'y a pas mieux a faire.
    """
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    w = max(1, int(tile.width * scale))
    h = max(1, int(tile.height * scale))
    big = tile.resize((w, h), Image.NEAREST)
    out.alpha_composite(big, ((width - w) // 2, height - h))
    return out


def statue(idle: Image.Image) -> Image.Image:
    """Statue de 20x20, recadree et non reduite.

    Le mod Brones y arrivait par une reduction a 0.83, qui est le seul endroit de
    tout l'assemblage ou des pixels se melangent : a cette echelle le plus proche
    voisin mange une colonne sur six, au hasard de l'arrondi. Un recadrage donne le
    meme cadre sans toucher un seul pixel - le personnage debout tient dans x 5..18
    et y 4..23 sur toutes les planches essayees, il reste donc entier.

    Si un jour il n'y tient pas, on rogne plutot que de deformer : une statue
    legerement coupee se remarque moins qu'une statue floue.
    """
    out = Image.new("RGBA", (20, 20), (0, 0, 0, 0))
    out.paste(idle.crop((2, idle.height - 20, 22, idle.height)), (0, 0))
    return out


def build(design: Design, out_root: Path) -> dict:
    sheet = Image.open(design.source).convert("RGBA")
    name = design.name
    report: dict = {"name": name, "source": design.source.name}

    body, body_empty = assemble(sheet, design, BODY_SLOTS)
    corpse, corpse_empty = assemble(sheet, design, CORPSE_SLOTS)
    report["empty"] = body_empty + corpse_empty

    def save(image: Image.Image, *parts: str) -> None:
        path = out_root.joinpath(name, *parts)
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path)

    save(body, "player", f"{name}.png")
    save(tinted(body, 0), "player", f"{name}_red.png")
    save(tinted(body, 220), "player", f"{name}_blue.png")

    save(corpse, "player", "corpses", f"{name}-normal.png")
    save(tinted(corpse, 0), "player", "corpses", f"{name}-redTeam.png")
    save(tinted(corpse, 220), "player", "corpses", f"{name}-blueTeam.png")
    save(silhouette(corpse), "player", "corpses", f"{name}-flash.png")

    # Les tetes sont vides : le personnage porte la sienne dans son corps. Le
    # decoupage tete/corps de TowerFall n'existe que pour la faire tourner quand on
    # vise, pose que la source n'a pas. Separer poserait une seconde tete sur la
    # premiere.
    for part in ("NoHat", "Crown"):
        save(Image.new("RGBA", (50, 10), (0, 0, 0, 0)), "player", "head", f"{name}{part}.png")

    idle = body.crop((0, 0, design.frame, design.frame))
    save(statue(idle), "player", "statues", f"{name}.png")
    save(scaled_onto(idle, 60, 120, 4), "portraits", f"notJoined{name}.png")
    save(scaled_onto(idle, 60, 120, 4), "portraits", f"joined{name}.png")
    save(scaled_onto(idle, 50, 50, 2), "portraits", f"win{name}.png")
    save(scaled_onto(idle, 50, 50, 2), "portraits", f"lose{name}.png")

    contact(design, body, corpse, out_root / name / "_contact.png")
    (out_root / name / "design.json").write_text(
        json.dumps(
            {
                "name": name,
                "source": str(design.source),
                "window": list(design.window),
                "frame": design.frame,
                "cell": design.cell,
                "hue": design.hue,
                "picks": {k: list(v) for k, v in design.picks.items()},
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    sheet.close()
    return report


def contact(design: Design, body: Image.Image, corpse: Image.Image, path: Path) -> None:
    """Image de controle : les seize poses agrandies, sur fond de damier.

    Le damier plutot qu'un aplat : un pixel oublie au bord d'une pose ne se voit
    pas sur du noir, et c'est exactement ce qu'on cherche a reperer ici.
    """
    scale = 4
    strip = Image.new("RGBA", (body.width + corpse.width, body.height))
    strip.paste(body, (0, 0))
    strip.paste(corpse, (body.width, 0))

    big = strip.resize((strip.width * scale, strip.height * scale), Image.NEAREST)
    board = Image.new("RGBA", big.size, (48, 48, 62, 255))
    pixels = board.load()

    for y in range(0, board.height, 8):
        for x in range(0, board.width, 8):
            if (x // 8 + y // 8) % 2:
                for dy in range(8):
                    for dx in range(8):
                        if x + dx < board.width and y + dy < board.height:
                            pixels[x + dx, y + dy] = (62, 62, 78, 255)

    board.alpha_composite(big)

    # Une ligne par image : c'est la seule facon de voir qu'une pose deborde sur sa
    # voisine, et le debordement est l'erreur classique d'une fenetre mal calee.
    marks = board.load()
    step = design.frame * scale
    for x in range(0, board.width + 1, step):
        for y in range(board.height):
            if x < board.width:
                marks[x, y] = (255, 80, 80, 255)

    path.parent.mkdir(parents=True, exist_ok=True)
    board.convert("RGB").save(path)


# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description="Prototype d'assemblage de la forge.")
    parser.add_argument("--sheet", help="nom de la planche source, sans extension")
    parser.add_argument("--src", type=Path, default=Path(r"D:\__dev\code\archive\tf-archer\Texture2D"))
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--name", help="nom de l'archer (defaut : celui de la planche)")
    parser.add_argument("--window", default=",".join(str(v) for v in WINDOW))
    parser.add_argument("--frame", type=int, default=FRAME)
    parser.add_argument("--cell", type=int, default=SOURCE_CELL)
    parser.add_argument("--hue", type=float, default=30.0)
    parser.add_argument("--list", action="store_true", help="liste les planches disponibles")
    args = parser.parse_args()

    if args.list:
        for path in sorted(args.src.glob("*.png")):
            print(path.stem)
        return 0

    if not args.sheet:
        parser.error("--sheet est requis (ou --list)")

    source = args.src / f"{args.sheet}.png"
    if not source.exists():
        print(f"Planche introuvable : {source}", file=sys.stderr)
        return 1

    wx, wy = (int(v) for v in args.window.split(","))
    design = Design(
        name=args.name or args.sheet.replace("_anim", "").replace(" ", ""),
        source=source,
        window=(wx, wy),
        frame=args.frame,
        cell=args.cell,
        hue=args.hue,
    )
    design.fill_from_layout()

    report = build(design, args.out)

    print(f"{report['name']} <- {report['source']}")
    if report["empty"]:
        print("  poses vides, a remplir a la main : " + ", ".join(report["empty"]))
    print(f"  ecrit dans {args.out / report['name']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
