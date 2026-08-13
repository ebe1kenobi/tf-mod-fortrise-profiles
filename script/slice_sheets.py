#!/usr/bin/env python3
"""Decoupe des planches de sprites en images individuelles.

Chaque planche source donne un repertoire portant son nom, contenant une image par
sprite. Aucun fichier de description : la forge liste le repertoire.

Deux problemes a resoudre, et ils sont independants.

1. QU'EST-CE QUI EST DU CONTENU ?
   Une planche porte sa transparence de deux facons. Soit un canal alpha - c'est
   le cas des planches Broforce. Soit une COULEUR DE FOND, comme les planches
   arcade ripees, opaques a 100 % et posees sur du magenta. Sans distinguer les
   deux, le masque d'une planche a fond colore vaut "tout", elle n'est qu'un seul
   ilot, et plus rien ne se deduit. La couleur est prise aux QUATRE COINS, ou a
   defaut sur la teinte dominante si elle occupe la moitie de la planche.

2. OU S'ARRETE CHAQUE SPRITE ?
   Deux reponses, selon la planche.

   GRILLE - les sprites sont ranges en cases de taille egale. La regle tient en
   une phrase : la bonne grille est la plus fine dont aucune ligne ne coupe un
   sprite. Une planche restee sans reponse emprunte la taille la plus repandue
   parmi les planches de MEME DIMENSION : dans un meme jeu, deux planches de meme
   format se decoupent presque toujours pareil.

   ILOTS - aucune grille. Chaque ilot de contenu est un sprite, avec SA taille.
   Les ilots proches sont fusionnes (une main detachee, une arme, un oeil separe
   par un liseré appartiennent au meme dessin), l'ecart tolere se regle par
   --gap. C'est le mode a employer des que les dessins n'ont pas tous la meme
   taille ou ne sont pas alignes.

   En mode auto - le defaut - la grille est tentee d'abord, et toute planche dont
   la grille ne se deduit pas franchement bascule en ilots.

Les cases d'une GRILLE sont enregistrees entieres, pas rognees : la place du
dessin dans sa case porte l'alignement de l'animation, et la rogner ferait
sautiller le personnage. En mode ILOTS il n'y a pas de case, chaque image est
donc au plus juste.

POUR LA FORGE : elle liste simplement les fichiers du repertoire et lit la taille
de chaque PNG. Rien a declarer, rien a tenir a jour. Les fichiers commencant par un
souligne - la planche de contact - sont ignores.

Usage :
    python slice_sheets.py
    python slice_sheets.py --only "*goku*" --mode islands
    python slice_sheets.py --only "*bro*" --cell 32
    python slice_sheets.py --bg FF00FF --gap 5
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


DEFAULT_SRC = Path(r".")
DEFAULT_OUT = Path(r".\sprites")

# En dessous, une "case" ne contient plus un sprite mais un morceau de sprite.
MIN_CELL = 8

# Un pixel est opaque a partir de ce seuil. Les bords adoucis du pixel art
# descendent bas, mais pas jusqu'a 1 : compter les quasi-transparents ferait
# grossir les ilots et fausserait la grille.
ALPHA_THRESHOLD = 8


# ---------------------------------------------------------------------------
# Ce qui est du contenu et ce qui est du fond
# ---------------------------------------------------------------------------


# Ecart tolere autour de la couleur de fond. Les GIF trames et les bords
# adoucis font baver la teinte de quelques unites ; au-dela, on mordrait dans
# le dessin.
BG_TOLERANCE = 12


def background_color(rgb: np.ndarray) -> tuple[int, int, int] | None:
    """Couleur de fond d'une planche sans transparence, ou None.

    Deux indices, dans cet ordre. Les QUATRE COINS d'abord : une planche
    decoupee proprement a du fond a chaque angle, et c'est le seul endroit ou
    l'on soit sur de ne pas tomber sur un dessin. Si les coins ne s'accordent
    pas, la couleur DOMINANTE, mais seulement si elle occupe la moitie de la
    planche - en dessous, ce n'est plus un fond mais un aplat du dessin.
    """
    height, width, _ = rgb.shape
    corners = [
        tuple(int(v) for v in rgb[0, 0]),
        tuple(int(v) for v in rgb[0, width - 1]),
        tuple(int(v) for v in rgb[height - 1, 0]),
        tuple(int(v) for v in rgb[height - 1, width - 1]),
    ]
    if len(set(corners)) == 1:
        return corners[0]

    flat = rgb.reshape(-1, 3).astype(np.int32)
    packed = (flat[:, 0] << 16) | (flat[:, 1] << 8) | flat[:, 2]
    values, counts = np.unique(packed, return_counts=True)
    best = int(values[counts.argmax()])

    if counts.max() < packed.size * 0.5:
        return None

    return ((best >> 16) & 255, (best >> 8) & 255, best & 255)


def content_mask(path: Path, key: tuple[int, int, int] | None = None):
    """Rend (image RGBA detouree, masque du contenu) pour n'importe quelle planche.

    Le point qui bloquait tout : une planche peut porter sa transparence de deux
    facons. Soit un canal alpha - c'est le cas des planches Broforce - soit une
    COULEUR DE FOND, comme les planches arcade ripees, qui sont opaques a 100 %
    et posees sur du magenta. Sans distinguer les deux, le masque d'une planche
    a fond colore vaut "tout", la planche entiere n'est qu'un seul ilot, et plus
    aucune taille ne se deduit.
    """
    image = Image.open(path)
    frames = getattr(image, "n_frames", 1)
    rgba = image.convert("RGBA")

    array = np.array(rgba)
    alpha = array[:, :, 3]

    # Un vrai canal alpha : au moins un pixel transparent. Sinon la planche est
    # opaque et sa transparence est portee par une couleur.
    if alpha.min() < 255:
        return rgba, alpha >= ALPHA_THRESHOLD, frames, None

    rgb = array[:, :, :3]
    background = key if key is not None else background_color(rgb)

    if background is None:
        # Ni alpha ni fond identifiable : on prend toute la planche pour du
        # contenu, ce qui rend au moins l'ancien comportement.
        return rgba, np.ones(alpha.shape, dtype=bool), frames, None

    distance = np.abs(rgb.astype(np.int16) - np.array(background, dtype=np.int16))
    mask = distance.max(axis=2) > BG_TOLERANCE

    # Le fond devient reellement transparent dans les images ecrites : sans ca,
    # chaque sprite sortirait avec son rectangle magenta.
    array[:, :, 3] = np.where(mask, 255, 0).astype(np.uint8)

    return Image.fromarray(array, "RGBA"), mask, frames, background


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


def analyse(path: Path, forced: tuple[int, int] | None, max_cell: int,
            key: tuple[int, int, int] | None = None) -> Sheet | None:
    """Determine la taille des cases d'une planche, sans rien ecrire.

    Rend `cell = None` quand la deduction n'aboutit pas ou aboutit a une case trop
    grande pour etre un sprite : la seconde passe reprendra ces planches-la.
    """
    image, mask, _, _ = content_mask(path, key)
    size = (image.width, image.height)

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
    key: tuple[int, int, int] | None = None,
) -> str:
    """Decoupe une planche. Rend une ligne de compte-rendu."""
    image, mask, _, _ = content_mask(path, key)

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

    # Aucun index n'est ecrit : la forge liste simplement les fichiers du
    # repertoire. Un descripteur a cote des images serait une seconde source de
    # verite, qui divergerait au premier decoupage relance.

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


# ---------------------------------------------------------------------------
# Decoupe sans grille : chaque sprite a sa propre taille
# ---------------------------------------------------------------------------


def merge_boxes(boxes: list[list[int]], gap: int) -> list[list[int]]:
    """Fusionne les cadres qui se touchent a `gap` pixres pres.

    Un personnage n'est pas toujours d'un seul tenant : une main detachee, une
    arme, un oeil separe par un liseré transparent forment autant d'ilots. Sans
    fusion, un sprite sortirait en morceaux. On fusionne donc les cadres proches,
    en repassant jusqu'a stabilite - fusionner A et B peut les rapprocher de C.
    """
    boxes = [list(b) for b in boxes]
    changed = True

    while changed:
        changed = False
        merged: list[list[int]] = []

        for box in boxes:
            for other in merged:
                touches = (
                    box[0] <= other[2] + gap
                    and other[0] <= box[2] + gap
                    and box[1] <= other[3] + gap
                    and other[1] <= box[3] + gap
                )
                if touches:
                    other[0] = min(other[0], box[0])
                    other[1] = min(other[1], box[1])
                    other[2] = max(other[2], box[2])
                    other[3] = max(other[3], box[3])
                    changed = True
                    break
            else:
                merged.append(box)

        boxes = merged

    return boxes


def find_sprites(mask: np.ndarray, gap: int, min_side: int, min_pixels: int) -> list[list[int]]:
    """Cadres des sprites, sans supposer aucune grille.

    C'est la reponse au cas general : on ne cherche plus une taille de case
    commune, on prend chaque ilot de contenu avec SA taille. Une planche dont
    les dessins ont des tailles differentes se decoupe donc aussi bien qu'une
    planche reguliere.
    """
    if HAVE_SCIPY:
        labels, count = ndimage.label(mask)
        if count == 0:
            return []
        slices = ndimage.find_objects(labels)
        boxes = [
            [int(sx.start), int(sy.start), int(sx.stop) - 1, int(sy.stop) - 1]
            for sy, sx in slices
        ]
    else:
        boxes = flood_boxes(mask)

    boxes = merge_boxes(boxes, gap)

    kept = []
    for x0, y0, x1, y1 in boxes:
        if x1 - x0 + 1 < min_side or y1 - y0 + 1 < min_side:
            continue
        if int(mask[y0 : y1 + 1, x0 : x1 + 1].sum()) < min_pixels:
            continue
        kept.append([x0, y0, x1, y1])

    # De haut en bas puis de gauche a droite : l'ordre de lecture d'une planche,
    # donc celui des poses d'une animation.
    kept.sort(key=lambda b: (b[1], b[0]))
    return kept


def flood_boxes(mask: np.ndarray) -> list[list[int]]:
    """Cadres des ilots sans scipy. Parcours en largeur, pile explicite."""
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    boxes: list[list[int]] = []

    for sy in range(height):
        for sx in range(width):
            if not mask[sy, sx] or seen[sy, sx]:
                continue

            stack = [(sy, sx)]
            seen[sy, sx] = True
            x0 = x1 = sx
            y0 = y1 = sy

            while stack:
                y, x = stack.pop()
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y

                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))

            boxes.append([x0, y0, x1, y1])

    return boxes


def slice_islands(
    path: Path,
    out_root: Path,
    gap: int,
    min_side: int,
    min_pixels: int,
    dedupe: bool,
    contact: bool,
    key: tuple[int, int, int] | None,
) -> str:
    """Decoupe une planche sprite par sprite, chacun a sa taille reelle.

    Aucun rembourrage et aucun index : un fichier par sprite, nomme sNNN.png, et
    c'est tout. La forge liste le repertoire et lit la taille de chaque PNG, donc
    il n'y a rien a declarer a cote - et rien qui puisse diverger des fichiers.
    """
    image, mask, _, background = content_mask(path, key)

    boxes = find_sprites(mask, gap, min_side, min_pixels)
    if not boxes:
        return f"{path.name}: aucun sprite trouve"

    target = out_root / path.stem
    target.mkdir(parents=True, exist_ok=True)

    sizes: list[tuple[int, int]] = []
    written: list[str] = []
    seen: dict[bytes, str] = {}
    duplicates = 0

    for index, (x0, y0, x1, y1) in enumerate(boxes):
        crop = image.crop((x0, y0, x1 + 1, y1 + 1))

        if dedupe:
            signature = crop.tobytes()
            if signature in seen:
                duplicates += 1
                continue
            seen[signature] = ""

        name = f"s{index:03d}.png"
        crop.save(target / name)

        # Les noms ecrits, et non leur rang : les doublons ecartes laissent des
        # trous dans la numerotation, et la planche de contact allait chercher
        # s043.png parce qu'elle avait compte quarante-quatre images.
        written.append(name)
        sizes.append((crop.width, crop.height))

    if not sizes:
        return f"{path.name}: aucun sprite retenu"

    widths = [w for w, _ in sizes]
    heights = [h for _, h in sizes]

    if contact:
        write_islands_contact(target, written, max(widths), max(heights))

    extra = f", {duplicates} doublons ecartes" if duplicates else ""
    fond = f", fond {background}" if background else ""
    return (
        f"{path.name}: {len(sizes)} sprites, "
        f"de {min(widths)}x{min(heights)} a {max(widths)}x{max(heights)}{fond}{extra}"
    )


def write_islands_contact(
    target: Path, names: list[str], cell_w: int, cell_h: int
) -> None:
    """Planche de contact, pour se reperer dans un explorateur de fichiers.

    Les sprites n'ayant pas la meme taille, chacun est centre dans une case aux
    dimensions du plus grand. Le nom commence par un souligne : c'est ce qui la
    fait ignorer par la forge, qui ne veut que des poses.
    """
    columns = 12
    rows = (len(names) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (0, 0, 0, 0))

    for i, name in enumerate(names):
        tile = Image.open(target / name)
        x = (i % columns) * cell_w + (cell_w - tile.width) // 2
        y = (i // columns) * cell_h + (cell_h - tile.height) // 2
        sheet.paste(tile, (x, y))

    sheet.save(target / "_contact.png")


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
    parser.add_argument(
        "--mode",
        choices=["auto", "grid", "islands"],
        default="auto",
        help="grid = grille reguliere, islands = chaque sprite a sa taille, "
             "auto = grille si elle se deduit, sinon islands (defaut)",
    )
    parser.add_argument(
        "--bg",
        help="couleur de fond en RRGGBB, ou 'none'. Deduite des coins par defaut.",
    )
    parser.add_argument(
        "--gap",
        type=int,
        default=3,
        help="en islands : ecart en pixels sous lequel deux morceaux sont tenus "
             "pour un meme sprite (defaut 3)",
    )
    parser.add_argument(
        "--min-side",
        type=int,
        default=6,
        help="en islands : sous ce cote, un ilot est tenu pour une poussiere (defaut 6)",
    )
    args = parser.parse_args()

    key = None
    if args.bg and args.bg.lower() != "none":
        raw = args.bg.lstrip("#")
        if len(raw) != 6:
            print("--bg attend six chiffres hexadecimaux, ex: FF00FF", file=sys.stderr)
            return 1
        key = (int(raw[0:2], 16), int(raw[2:4], 16), int(raw[4:6], 16))

    if not args.src.is_dir():
        print(f"Repertoire introuvable : {args.src}", file=sys.stderr)
        return 1

    # Toutes les extensions que Pillow ouvre couramment : une planche ripee est
    # aussi souvent un GIF ou un BMP qu'un PNG, et refuser le fichier pour son
    # extension etait la moitie du probleme.
    patterns = ("*.png", "*.gif", "*.bmp", "*.jpg", "*.jpeg", "*.webp", "*.tga")
    sources = sorted(
        {
            p
            for pattern in patterns
            for p in args.src.glob(pattern)
            if fnmatch.fnmatch(p.name.lower(), args.only.lower())
        }
    )

    # Les images deja produites par une passe precedente ne sont pas des sources.
    sources = [p for p in sources if args.out.resolve() not in p.resolve().parents]

    if not sources:
        print(f"Aucune planche ne correspond a {args.only!r} dans {args.src}")
        return 1

    if not HAVE_SCIPY:
        print("scipy absent : detection plus lente, resultat identique.\n")

    forced = (args.cell, args.cell) if args.cell else None
    args.out.mkdir(parents=True, exist_ok=True)

    # Premiere passe : deduire ce qui se deduit. C'est la partie couteuse, elle
    # n'ecrit rien - de quoi corriger les planches recalcitrantes avant d'ecrire
    # quoi que ce soit.
    # Le mode islands ne cherche aucune grille : il n'a donc pas besoin de la
    # passe d'analyse ni de l'emprunt entre planches.
    if args.mode == "islands":
        total = 0
        for path in sources:
            try:
                line = slice_islands(
                    path, args.out, args.gap, args.min_side, args.min_pixels,
                    not args.keep_duplicates, not args.no_contact, key,
                )
                total += int(line.split(": ")[1].split(" ")[0]) if "sprites" in line else 0
            except Exception as error:
                line = f"{path.name}: ECHEC - {error}"
            print(line)

        print(f"\n{len(sources)} planches, {total} sprites ecrits dans {args.out}")
        return 0

    print(f"Analyse de {len(sources)} planches...")
    sheets: list[Sheet] = []
    empty = 0

    for path in sources:
        try:
            sheet = analyse(path, forced, args.max_cell, key)
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

        # "minorante" n'est pas une reponse, c'est un aveu : la grille n'a pas ete
        # deduite et l'on s'est rabattu sur la taille du plus grand ilot. En mode
        # auto, une planche dans ce cas est justement celle qu'il faut reprendre
        # sprite par sprite - l'accepter ici la condamnerait a un decoupage
        # arbitraire, sans jamais atteindre le repli sans grille.
        if answer and not (args.mode == "auto" and answer[1] == "minorante"):
            sheet.cell, sheet.origin = answer
            resolved += 1
        else:
            unresolved.append(sheet)

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
                key,
            )
            total += int(line.split(": ")[1].split(" ")[0])
        except Exception as error:  # une planche cassee ne doit pas tout arreter
            line = f"{sheet.path.name}: ECHEC - {error}"

        print(line)

    # Ce que la grille n'a pas su decouper n'est plus abandonne : on le reprend
    # sprite par sprite. C'est exactement le cas des planches a fond colore, dont
    # les dessins ne sont ni alignes ni de meme taille.
    if unresolved:
        print(f"\n{len(unresolved)} planches sans grille reguliere : "
              "reprise sprite par sprite.")
        for sheet in unresolved:
            try:
                line = slice_islands(
                    sheet.path, args.out, args.gap, args.min_side, args.min_pixels,
                    not args.keep_duplicates, not args.no_contact, key,
                )
                total += int(line.split(": ")[1].split(" ")[0]) if "sprites" in line else 0
            except Exception as error:
                line = f"{sheet.path.name}: ECHEC - {error}"
            print(line)

    print(f"\n{len(sheets)} planches, {total} images ecrites dans {args.out}")

    if empty:
        print(f"{empty} planches entierement vides, ignorees.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
