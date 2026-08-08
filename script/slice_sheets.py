#!/usr/bin/env python3
"""Decoupe des planches de sprites en images individuelles.

Chaque PNG source donne un repertoire portant son nom, contenant une image par
case non vide, plus un index.json decrivant la grille et le contenu de chaque case.

Le point delicat est de trouver la taille des cases : une planche ne la declare
nulle part. On la deduit, et la regle tient en une phrase :

    la bonne grille est la plus fine dont aucune ligne ne coupe un sprite.

Un sprite est un ilot de pixels opaques. Si la grille est trop fine, une de ses
lignes traverse un ilot - c'est detectable exactement, sans approximation, en
regardant les pixels voisins de part et d'autre de chaque ligne. Si elle est trop
large, plusieurs sprites se retrouvent dans la meme case. Entre les deux, on prend
la plus fine qui ne coupe rien, jamais plus petite que le plus grand ilot : sans
cette borne, un personnage jambes ecartees pourrait etre coupe en deux par une
ligne passant pile dans son entrejambe.

Cette regle a une limite, et elle est franche : sur certaines planches les dessins
se touchent d'une case a l'autre. Aucune grille fine ne passe alors sans couper, et
la deduction remonte jusqu'a une case absurde couvrant un quart de la planche. Le
script traite donc le lot en deux temps : il deduit ce qu'il peut, puis reprend les
planches restees sans reponse en leur imposant la taille de case la plus repandue
parmi les planches de MEME DIMENSION - dans un meme jeu, deux planches de meme
format decoupent presque toujours pareil. C'est le lot qui se corrige lui-meme,
sans rien avoir a saisir.

Les cases sont enregistrees entieres, pas rognees au contenu. C'est ce qui permet
de les reassembler ensuite : la place du dessin dans sa case porte l'alignement de
l'animation, et la rogner ferait sautiller le personnage. Le cadre exact du dessin
est note dans l'index pour qui veut l'afficher serre.

Usage :
    python slice_sheets.py
    python slice_sheets.py --only "*bro*" --cell 32
    python slice_sheets.py --src ... --out ... --keep-duplicates
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import sys
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image

try:
    from scipy import ndimage

    HAVE_SCIPY = True
except ImportError:  # pragma: no cover - depend de l'installation
    HAVE_SCIPY = False


DEFAULT_SRC = Path(r"D:\__dev\code\archive\tf-archer\Texture2D")
DEFAULT_OUT = Path(r"D:\__dev\code\archive\tf-archer\sprites")

# En dessous, une "case" ne contient plus un sprite mais un morceau de sprite.
MIN_CELL = 8

# Un pixel est opaque a partir de ce seuil. Les bords adoucis du pixel art
# descendent bas, mais pas jusqu'a 1 : compter les quasi-transparents ferait
# grossir les ilots et fausserait la grille.
ALPHA_THRESHOLD = 8


# ---------------------------------------------------------------------------
# Detection de la grille
# ---------------------------------------------------------------------------


def divisors(total: int, minimum: int) -> list[int]:
    """Diviseurs de `total` au moins egaux a `minimum`, du plus petit au plus grand."""
    return [d for d in range(minimum, total + 1) if total % d == 0]


def cuts_columns(mask: np.ndarray, step: int) -> bool:
    """Vrai si une ligne verticale de la grille traverse un ilot de pixels.

    Un ilot traverse la ligne x=k s'il possede deux pixels voisins - au sens des
    huit directions - de part et d'autre. Il suffit donc de comparer la colonne
    k-1 a la colonne k, decalee d'un cran vers le haut, vers le bas, et pas du
    tout. Aucune detection d'ilot n'est necessaire pour ce test.
    """
    height, width = mask.shape
    for x in range(step, width, step):
        left = mask[:, x - 1]
        right = mask[:, x]
        if np.any(left & right):
            return True
        if np.any(left[:-1] & right[1:]):
            return True
        if np.any(left[1:] & right[:-1]):
            return True
    return False


def cuts_rows(mask: np.ndarray, step: int) -> bool:
    """Meme test pour les lignes horizontales."""
    return cuts_columns(mask.T, step)


def largest_island(mask: np.ndarray) -> tuple[int, int]:
    """Largeur et hauteur du plus grand ilot de pixels opaques.

    Sert de plancher a la taille des cases : une case ne peut pas etre plus
    petite que le plus grand dessin de la planche.
    """
    if not mask.any():
        return (0, 0)

    if HAVE_SCIPY:
        structure = np.ones((3, 3), dtype=bool)  # huit directions
        labels, count = ndimage.label(mask, structure=structure)
        if count == 0:
            return (0, 0)

        widest = tallest = 0
        for y_slice, x_slice in ndimage.find_objects(labels):
            widest = max(widest, x_slice.stop - x_slice.start)
            tallest = max(tallest, y_slice.stop - y_slice.start)
        return (widest, tallest)

    return largest_island_fallback(mask)


def largest_island_fallback(mask: np.ndarray) -> tuple[int, int]:
    """Meme mesure sans scipy, par parcours en largeur des pixels opaques."""
    height, width = mask.shape
    seen = np.zeros_like(mask)
    widest = tallest = 0

    for start_y, start_x in zip(*np.nonzero(mask)):
        if seen[start_y, start_x]:
            continue

        stack = [(int(start_y), int(start_x))]
        seen[start_y, start_x] = True
        min_x = max_x = int(start_x)
        min_y = max_y = int(start_y)

        while stack:
            y, x = stack.pop()
            min_x, max_x = min(min_x, x), max(max_x, x)
            min_y, max_y = min(min_y, y), max(max_y, y)

            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width:
                        if mask[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))

        widest = max(widest, max_x - min_x + 1)
        tallest = max(tallest, max_y - min_y + 1)

    return (widest, tallest)


def detect_grid(mask: np.ndarray) -> tuple[int, int] | None:
    """Taille des cases (largeur, hauteur), ou None si la planche n'a pas de grille.

    On cherche d'abord une case carree, de loin le cas le plus courant. A defaut
    on laisse les deux axes diverger - certaines planches sont plus larges que
    hautes par case.
    """
    height, width = mask.shape
    island_w, island_h = largest_island(mask)
    floor_w = max(MIN_CELL, island_w)
    floor_h = max(MIN_CELL, island_h)

    square = [
        d
        for d in divisors(width, max(floor_w, floor_h))
        if height % d == 0 and d >= floor_h
    ]
    for size in square:
        if not cuts_columns(mask, size) and not cuts_rows(mask, size):
            return (size, size)

    cell_w = next(
        (d for d in divisors(width, floor_w) if not cuts_columns(mask, d)), None
    )
    cell_h = next(
        (d for d in divisors(height, floor_h) if not cuts_rows(mask, d)), None
    )

    if cell_w is None or cell_h is None:
        return None

    return (cell_w, cell_h)


# ---------------------------------------------------------------------------
# Decoupe
# ---------------------------------------------------------------------------


@dataclass
class Frame:
    file: str
    row: int
    col: int
    bbox: tuple[int, int, int, int]
    pixels: int


def content_bbox(cell: np.ndarray) -> tuple[int, int, int, int]:
    """Cadre du dessin dans sa case, en (x, y, largeur, hauteur)."""
    rows = np.any(cell, axis=1)
    cols = np.any(cell, axis=0)
    y0, y1 = np.where(rows)[0][[0, -1]]
    x0, x1 = np.where(cols)[0][[0, -1]]
    return (int(x0), int(y0), int(x1 - x0 + 1), int(y1 - y0 + 1))


@dataclass
class Sheet:
    """Une planche analysee, avant decoupe."""

    path: Path
    size: tuple[int, int]  # dimensions de la planche
    island: tuple[int, int]  # plus grand ilot, plancher de la taille des cases
    cell: tuple[int, int] | None
    origin: str  # "deduite", "imposee", "empruntee" ou "minorante"


def analyse(path: Path, forced: tuple[int, int] | None, max_cell: int) -> Sheet | None:
    """Determine la taille des cases d'une planche, sans rien ecrire.

    Rend `cell = None` quand la deduction n'aboutit pas ou aboutit a une case trop
    grande pour etre un sprite : la seconde passe reprendra ces planches-la.
    """
    image = Image.open(path).convert("RGBA")
    mask = np.array(image)[:, :, 3] >= ALPHA_THRESHOLD
    size = (image.width, image.height)
    image.close()

    if not mask.any():
        return None

    island = largest_island(mask)

    if forced:
        return Sheet(path, size, island, forced, "imposee")

    cell = detect_grid(mask)

    # Une case plus grande que max_cell ne contient plus un sprite mais un groupe :
    # c'est la signature de dessins qui se touchent. On la refuse plutot que de la
    # prendre pour une reponse.
    if cell and (cell[0] > max_cell or cell[1] > max_cell):
        cell = None

    return Sheet(path, size, island, cell, "deduite")


def borrow_cell(sheet: Sheet, by_size: dict) -> tuple[tuple[int, int], str] | None:
    """Taille de case pour une planche dont la grille n'a pas pu etre deduite.

    Deux reponses, dans cet ordre, et l'ordre est ce qui compte :

    1. Celle des planches de MEME DIMENSION qui, elles, ont abouti. Dans un meme
       jeu, deux planches de meme format se decoupent presque toujours pareil.

    2. A defaut, la plus petite case pouvant contenir le plus grand dessin. Une
       case ne peut pas etre plus petite que ce qu'elle doit contenir - c'est une
       borne, pas une devinette.

    Il n'y a volontairement pas de troisieme reponse par la taille la plus
    repandue du lot : une planche de gros ennemis n'a rien a emprunter a des
    planches de personnages ordinaires, et 32 pixels pour un dessin qui en fait 79
    le decoupe en seize morceaux.
    """
    peers = by_size.get(sheet.size)
    if peers:
        return (max(set(peers), key=peers.count), "empruntee")

    width, height = sheet.size
    floor = max(MIN_CELL, sheet.island[0], sheet.island[1])

    square = next(
        (d for d in divisors(width, floor) if height % d == 0), None
    )
    if square:
        return ((square, square), "minorante")

    cell_w = next((d for d in divisors(width, max(MIN_CELL, sheet.island[0]))), None)
    cell_h = next((d for d in divisors(height, max(MIN_CELL, sheet.island[1]))), None)
    if cell_w and cell_h:
        return ((cell_w, cell_h), "minorante")

    return None


def slice_sheet(
    path: Path,
    out_root: Path,
    cell: tuple[int, int],
    origin: str,
    min_pixels: int,
    dedupe: bool,
    contact: bool,
) -> str:
    """Decoupe une planche. Rend une ligne de compte-rendu."""
    image = Image.open(path).convert("RGBA")
    mask = np.array(image)[:, :, 3] >= ALPHA_THRESHOLD

    cell_w, cell_h = cell
    forced_cell = origin != "deduite"
    cols = image.width // cell_w
    rows = image.height // cell_h

    target = out_root / path.stem
    target.mkdir(parents=True, exist_ok=True)

    frames: list[Frame] = []
    seen: dict[bytes, str] = {}
    duplicates = 0

    for row in range(rows):
        for col in range(cols):
            y0, x0 = row * cell_h, col * cell_w
            cell_mask = mask[y0 : y0 + cell_h, x0 : x0 + cell_w]

            count = int(cell_mask.sum())
            if count < min_pixels:
                continue

            crop = image.crop((x0, y0, x0 + cell_w, y0 + cell_h))

            if dedupe:
                signature = crop.tobytes()
                if signature in seen:
                    duplicates += 1
                    continue
                seen[signature] = ""

            name = f"r{row:02d}c{col:02d}.png"
            crop.save(target / name)
            frames.append(Frame(name, row, col, content_bbox(cell_mask), count))

    index = {
        "source": path.name,
        "cell": {"width": cell_w, "height": cell_h},
        "grid": {"cols": cols, "rows": rows},
        "cell_origin": origin,
        "duplicates_dropped": duplicates,
        "frames": [
            {
                "file": f.file,
                "row": f.row,
                "col": f.col,
                "bbox": list(f.bbox),
                "pixels": f.pixels,
            }
            for f in frames
        ],
    }
    (target / "index.json").write_text(
        json.dumps(index, indent=2), encoding="utf-8"
    )

    if contact and frames:
        write_contact_sheet(target, frames, cell_w, cell_h)

    extra = f", {duplicates} doublons ecartes" if duplicates else ""
    return f"{path.name}: {len(frames)} images, case {cell_w}x{cell_h} ({origin}){extra}"


def write_contact_sheet(
    target: Path, frames: list[Frame], cell_w: int, cell_h: int
) -> None:
    """Planche de contact : toutes les images retenues, cote a cote.

    Sert a choisir une pose d'un coup d'oeil plutot qu'a ouvrir cent fichiers.
    Prefixee d'un souligne pour rester en tete de liste, et exclue de l'index.
    """
    columns = 16
    rows = (len(frames) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (0, 0, 0, 0))

    for i, frame in enumerate(frames):
        tile = Image.open(target / frame.file)
        sheet.paste(tile, ((i % columns) * cell_w, (i // columns) * cell_h))

    sheet.save(target / "_contact.png")


# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Decoupe des planches de sprites en images individuelles."
    )
    parser.add_argument("--src", type=Path, default=DEFAULT_SRC)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument(
        "--only", default="*", help="filtre sur le nom de fichier (ex: *bro*)"
    )
    parser.add_argument(
        "--cell",
        type=int,
        help="impose la taille des cases au lieu de la deduire",
    )
    parser.add_argument(
        "--min-pixels",
        type=int,
        default=4,
        help="en dessous, une case est tenue pour vide (defaut 4)",
    )
    parser.add_argument(
        "--keep-duplicates",
        action="store_true",
        help="garde les images identiques au lieu de n'en ecrire qu'une",
    )
    parser.add_argument(
        "--no-contact",
        action="store_true",
        help="n'ecrit pas la planche de contact _contact.png",
    )
    parser.add_argument(
        "--max-cell",
        type=int,
        default=64,
        help="au dela, une case deduite est tenue pour aberrante (defaut 64)",
    )
    args = parser.parse_args()

    if not args.src.is_dir():
        print(f"Repertoire introuvable : {args.src}", file=sys.stderr)
        return 1

    sources = sorted(
        p
        for p in args.src.glob("*.png")
        if fnmatch.fnmatch(p.name.lower(), args.only.lower())
    )

    if not sources:
        print(f"Aucun PNG ne correspond a {args.only!r} dans {args.src}")
        return 1

    if not HAVE_SCIPY:
        print("scipy absent : detection plus lente, resultat identique.\n")

    forced = (args.cell, args.cell) if args.cell else None
    args.out.mkdir(parents=True, exist_ok=True)

    # Premiere passe : deduire ce qui se deduit. C'est la partie couteuse, elle
    # n'ecrit rien - de quoi corriger les planches recalcitrantes avant d'ecrire
    # quoi que ce soit.
    print(f"Analyse de {len(sources)} planches...")
    sheets: list[Sheet] = []
    empty = 0

    for path in sources:
        try:
            sheet = analyse(path, forced, args.max_cell)
        except Exception as error:
            print(f"  {path.name}: ECHEC - {error}")
            continue

        if sheet is None:
            empty += 1
            continue

        sheets.append(sheet)

    # Seconde passe : les planches sans reponse empruntent la taille de case la
    # plus repandue chez celles de meme dimension. Deux planches de meme format
    # dans un meme jeu se decoupent presque toujours pareil.
    by_size: dict[tuple[int, int], list[tuple[int, int]]] = {}
    for sheet in sheets:
        if sheet.cell:
            by_size.setdefault(sheet.size, []).append(sheet.cell)

    resolved = 0
    unresolved = []

    for sheet in sheets:
        if sheet.cell:
            continue

        answer = borrow_cell(sheet, by_size)
        if answer:
            sheet.cell, sheet.origin = answer
            resolved += 1
        else:
            unresolved.append(sheet.path.name)

    if resolved:
        print(f"{resolved} planches sans grille nette : taille deduite du lot.\n")

    total = 0
    for sheet in sheets:
        if not sheet.cell:
            continue

        try:
            line = slice_sheet(
                sheet.path,
                args.out,
                sheet.cell,
                sheet.origin,
                args.min_pixels,
                not args.keep_duplicates,
                not args.no_contact,
            )
            total += int(line.split(": ")[1].split(" ")[0])
        except Exception as error:  # une planche cassee ne doit pas tout arreter
            line = f"{sheet.path.name}: ECHEC - {error}"

        print(line)

    print(f"\n{len(sheets)} planches, {total} images ecrites dans {args.out}")

    if empty:
        print(f"{empty} planches entierement transparentes, ignorees.")

    if unresolved:
        print(f"{len(unresolved)} sans taille : " + ", ".join(unresolved[:8]))
        print("Relancer celles-la avec --only et --cell.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
