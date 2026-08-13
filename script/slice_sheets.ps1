<#
.SYNOPSIS
    Decoupe des planches de sprites en images individuelles. Version Windows de
    slice_sheets.py, sans Python ni aucune installation.

.DESCRIPTION
    Meme travail, memes reglages, memes resultats que le script Python : chaque
    planche source donne un repertoire portant son nom, contenant une image par
    sprite, et aucun fichier de description - la forge liste le repertoire.

    Deux problemes a resoudre, et ils sont independants.

    1. QU'EST-CE QUI EST DU CONTENU ?
       Une planche porte sa transparence de deux facons. Soit un canal alpha -
       c'est le cas des planches Broforce. Soit une COULEUR DE FOND, comme les
       planches arcade ripees, opaques a 100 % et posees sur du magenta. Sans
       distinguer les deux, le masque d'une planche a fond colore vaut "tout",
       elle n'est qu'un seul ilot, et plus rien ne se deduit. La couleur est
       prise aux QUATRE COINS, ou a defaut sur la teinte dominante si elle
       occupe la moitie de la planche.

    2. OU S'ARRETE CHAQUE SPRITE ?
       GRILLE - les sprites sont ranges en cases de taille egale. La bonne
       grille est la plus fine dont aucune ligne ne coupe un sprite. Une planche
       restee sans reponse emprunte la taille la plus repandue parmi les
       planches de MEME DIMENSION.

       ILOTS - aucune grille. Chaque ilot de contenu est un sprite, avec SA
       taille. Les ilots proches sont fusionnes ; l'ecart tolere se regle par
       -Gap.

       En mode auto - le defaut - la grille est tentee d'abord, et toute planche
       dont la grille ne se deduit pas franchement bascule en ilots.

    POURQUOI DU C# AU MILIEU D'UN SCRIPT POWERSHELL
    Le travail est d'un bout a l'autre du parcours de pixels : une planche de
    2000x2000 en fait quatre millions, et chaque test de grille les relit. En
    PowerShell pur, une seule planche demanderait des minutes. Le pixel est donc
    confie a une classe compilee a la volee par Add-Type, et PowerShell garde ce
    qu'il fait bien : les arguments, les fichiers, l'enchainement des passes et
    le compte-rendu. Aucune dependance a installer : System.Drawing est livre
    avec Windows.

.EXAMPLE
    .\slice_sheets.ps1
.EXAMPLE
    .\slice_sheets.ps1 -Only "*goku*" -Mode islands
.EXAMPLE
    .\slice_sheets.ps1 -Only "*bro*" -Cell 32
.EXAMPLE
    .\slice_sheets.ps1 -Bg FF00FF -Gap 5
#>

[CmdletBinding()]
param(
    [string] $Src = ".",
    [string] $Out = ".\sprites",

    # Filtre sur le nom de fichier (ex: *bro*).
    [string] $Only = "*",

    # Impose la taille des cases au lieu de la deduire.
    [int] $Cell = 0,

    # En dessous, une case est tenue pour vide.
    [int] $MinPixels = 4,

    # Garde les images identiques au lieu de n'en ecrire qu'une.
    [switch] $KeepDuplicates,

    # N'ecrit pas la planche de contact _contact.png.
    [switch] $NoContact,

    # Au dela, une case deduite est tenue pour aberrante.
    [int] $MaxCell = 64,

    [ValidateSet("auto", "grid", "islands")]
    [string] $Mode = "auto",

    # Couleur de fond en RRGGBB, ou 'none'. Deduite des coins par defaut.
    [string] $Bg,

    # En islands : ecart en pixels sous lequel deux morceaux sont tenus pour un
    # meme sprite.
    [int] $Gap = 3,

    # En islands : sous ce cote, un ilot est tenu pour une poussiere.
    [int] $MinSide = 6
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Le travail sur les pixels
# ---------------------------------------------------------------------------

if (-not ('SpriteSheets.Sheet' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SpriteSheets
{
  /// <summary>
  /// Une planche chargee en memoire : ses pixels, et ce qui dans ces pixels est
  /// du contenu.
  ///
  /// Les pixels sont gardes a plat en BGRA - l'ordre de System.Drawing - plutot
  /// que dans un Bitmap : chaque test de grille relit la planche entiere, et
  /// passer par GetPixel serait mille fois plus lent qu'un acces a un tableau.
  /// </summary>
  public sealed class Sheet : IDisposable
  {
    /// <summary>En dessous, une case ne contient plus un sprite mais un morceau.</summary>
    public const int MinCell = 8;

    /// <summary>
    /// Un pixel est opaque a partir de ce seuil. Les bords adoucis du pixel art
    /// descendent bas, mais pas jusqu'a 1 : compter les quasi-transparents
    /// ferait grossir les ilots et fausserait la grille.
    /// </summary>
    const int AlphaThreshold = 8;

    /// <summary>
    /// Ecart tolere autour de la couleur de fond. Les GIF trames et les bords
    /// adoucis font baver la teinte de quelques unites ; au-dela, on mordrait
    /// dans le dessin.
    /// </summary>
    const int BackgroundTolerance = 12;

    public int Width;
    public int Height;

    /// <summary>La couleur de fond retenue, ou null quand la planche a un vrai alpha.</summary>
    public int[] Background;

    byte[] pixels;
    bool[] mask;

    Sheet() { }

    public void Dispose() { pixels = null; mask = null; }

    public bool IsEmpty
    {
      get
      {
        for (int i = 0; i < mask.Length; i++) { if (mask[i]) return false; }
        return true;
      }
    }

    /// <summary>
    /// Charge une planche et separe le contenu du fond.
    /// </summary>
    /// <param name="key">
    /// Couleur de fond imposee, ou null pour la deduire. Sans effet sur une
    /// planche qui porte un vrai canal alpha.
    /// </param>
    public static Sheet Load(string path, int[] key)
    {
      var sheet = new Sheet();

      using (var source = new Bitmap(path))
      using (Bitmap frame = AsArgb(source))
      {
        sheet.Width = frame.Width;
        sheet.Height = frame.Height;
        sheet.pixels = new byte[frame.Width * frame.Height * 4];

        var area = new Rectangle(0, 0, frame.Width, frame.Height);
        BitmapData data = frame.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
          for (int y = 0; y < frame.Height; y++)
          {
            IntPtr row = IntPtr.Add(data.Scan0, y * data.Stride);
            System.Runtime.InteropServices.Marshal.Copy(
                row, sheet.pixels, y * frame.Width * 4, frame.Width * 4);
          }
        }
        finally
        {
          frame.UnlockBits(data);
        }
      }

      sheet.BuildMask(key);
      return sheet;
    }

    /// <summary>
    /// La planche en 32 bits ARGB, prete a etre lue octet par octet.
    ///
    /// Une planche qui EST deja dans ce format est prise telle quelle. La
    /// redessiner par Graphics la ferait composer sur du transparent : les
    /// pixels a demi opaques passeraient par une premultiplication et
    /// changeraient de teinte de quelques unites. Sur du pixel art ca ne se
    /// verrait pas, mais on ne recopie pas une planche pour la modifier.
    ///
    /// Les autres formats - GIF et BMP indexes, JPEG 24 bits - n'ont pas ce
    /// probleme et n'ont pas d'autre chemin : leur transparence ne se lit pas
    /// sans conversion.
    /// </summary>
    static Bitmap AsArgb(Bitmap source)
    {
      if (source.PixelFormat == PixelFormat.Format32bppArgb)
      {
        return (Bitmap)source.Clone();
      }

      var frame = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

      using (var canvas = Graphics.FromImage(frame))
      {
        canvas.DrawImage(source, 0, 0, source.Width, source.Height);
      }

      return frame;
    }

    /// <summary>
    /// Le point qui bloquait tout : une planche peut porter sa transparence de
    /// deux facons. Soit un canal alpha, soit une couleur de fond. Sans
    /// distinguer les deux, le masque d'une planche a fond colore vaut "tout",
    /// la planche entiere n'est qu'un seul ilot, et plus aucune taille ne se
    /// deduit.
    /// </summary>
    void BuildMask(int[] key)
    {
      int count = Width * Height;
      mask = new bool[count];

      bool transparent = false;
      for (int i = 0; i < count && !transparent; i++)
      {
        if (pixels[i * 4 + 3] < 255) { transparent = true; }
      }

      if (transparent)
      {
        for (int i = 0; i < count; i++) { mask[i] = pixels[i * 4 + 3] >= AlphaThreshold; }
        return;
      }

      Background = key != null ? key : FindBackground();

      if (Background == null)
      {
        // Ni alpha ni fond identifiable : toute la planche est du contenu.
        for (int i = 0; i < count; i++) { mask[i] = true; }
        return;
      }

      for (int i = 0; i < count; i++)
      {
        int b = pixels[i * 4], g = pixels[i * 4 + 1], r = pixels[i * 4 + 2];

        int distance = Math.Max(Math.Abs(r - Background[0]),
                       Math.Max(Math.Abs(g - Background[1]), Math.Abs(b - Background[2])));

        mask[i] = distance > BackgroundTolerance;

        // Le fond devient reellement transparent dans les images ecrites : sans
        // ca, chaque sprite sortirait avec son rectangle magenta.
        pixels[i * 4 + 3] = mask[i] ? (byte)255 : (byte)0;
      }
    }

    /// <summary>
    /// Couleur de fond d'une planche sans transparence, ou null.
    ///
    /// Les QUATRE COINS d'abord : une planche decoupee proprement a du fond a
    /// chaque angle, et c'est le seul endroit ou l'on soit sur de ne pas tomber
    /// sur un dessin. Si les coins ne s'accordent pas, la couleur DOMINANTE,
    /// mais seulement si elle occupe la moitie de la planche - en dessous, ce
    /// n'est plus un fond mais un aplat du dessin.
    /// </summary>
    int[] FindBackground()
    {
      int[] corners =
      {
        Packed(0, 0), Packed(Width - 1, 0),
        Packed(0, Height - 1), Packed(Width - 1, Height - 1)
      };

      if (corners[0] == corners[1] && corners[1] == corners[2] && corners[2] == corners[3])
      {
        return Unpack(corners[0]);
      }

      var tally = new Dictionary<int, int>();
      int best = 0, most = 0;

      for (int i = 0; i < Width * Height; i++)
      {
        int packed = (pixels[i * 4 + 2] << 16) | (pixels[i * 4 + 1] << 8) | pixels[i * 4];

        int seen;
        tally.TryGetValue(packed, out seen);
        seen++;
        tally[packed] = seen;

        if (seen > most) { most = seen; best = packed; }
      }

      return most < Width * Height * 0.5 ? null : Unpack(best);
    }

    int Packed(int x, int y)
    {
      int i = (y * Width + x) * 4;
      return (pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i];
    }

    static int[] Unpack(int packed)
    {
      return new int[] { (packed >> 16) & 255, (packed >> 8) & 255, packed & 255 };
    }

    // ----------------------------------------------------------------------
    // Detection de la grille
    // ----------------------------------------------------------------------

    /// <summary>Diviseurs de total au moins egaux a minimum, du plus petit au plus grand.</summary>
    public static int[] Divisors(int total, int minimum)
    {
      var found = new List<int>();
      for (int d = Math.Max(1, minimum); d <= total; d++)
      {
        if (total % d == 0) { found.Add(d); }
      }
      return found.ToArray();
    }

    /// <summary>
    /// Vrai si une ligne verticale de la grille traverse un ilot de pixels.
    ///
    /// Un ilot traverse la ligne x=k s'il possede deux pixels voisins - au sens
    /// des huit directions - de part et d'autre. Il suffit donc de comparer la
    /// colonne k-1 a la colonne k, decalee d'un cran vers le haut, vers le bas,
    /// et pas du tout. Aucune detection d'ilot n'est necessaire pour ce test.
    /// </summary>
    public bool CutsColumns(int step)
    {
      for (int x = step; x < Width; x += step)
      {
        for (int y = 0; y < Height; y++)
        {
          if (!mask[y * Width + x - 1]) { continue; }

          if (mask[y * Width + x]) { return true; }
          if (y + 1 < Height && mask[(y + 1) * Width + x]) { return true; }
          if (y > 0 && mask[(y - 1) * Width + x]) { return true; }
        }
      }
      return false;
    }

    /// <summary>Meme test pour les lignes horizontales.</summary>
    public bool CutsRows(int step)
    {
      for (int y = step; y < Height; y += step)
      {
        for (int x = 0; x < Width; x++)
        {
          if (!mask[(y - 1) * Width + x]) { continue; }

          if (mask[y * Width + x]) { return true; }
          if (x + 1 < Width && mask[y * Width + x + 1]) { return true; }
          if (x > 0 && mask[y * Width + x - 1]) { return true; }
        }
      }
      return false;
    }

    /// <summary>
    /// Largeur et hauteur du plus grand ilot de pixels opaques. Sert de plancher
    /// a la taille des cases : une case ne peut pas etre plus petite que le plus
    /// grand dessin de la planche.
    /// </summary>
    public int[] LargestIsland()
    {
      int widest = 0, tallest = 0;

      foreach (int[] box in Islands(true))
      {
        widest = Math.Max(widest, box[2] - box[0] + 1);
        tallest = Math.Max(tallest, box[3] - box[1] + 1);
      }

      return new int[] { widest, tallest };
    }

    /// <summary>
    /// Taille des cases, ou null si la planche n'a pas de grille.
    ///
    /// On cherche d'abord une case carree, de loin le cas le plus courant. A
    /// defaut on laisse les deux axes diverger - certaines planches sont plus
    /// larges que hautes par case.
    /// </summary>
    public int[] DetectGrid()
    {
      int[] island = LargestIsland();
      int floorW = Math.Max(MinCell, island[0]);
      int floorH = Math.Max(MinCell, island[1]);

      foreach (int size in Divisors(Width, Math.Max(floorW, floorH)))
      {
        if (Height % size != 0 || size < floorH) { continue; }
        if (!CutsColumns(size) && !CutsRows(size)) { return new int[] { size, size }; }
      }

      int cellW = -1, cellH = -1;

      foreach (int d in Divisors(Width, floorW))
      {
        if (!CutsColumns(d)) { cellW = d; break; }
      }

      foreach (int d in Divisors(Height, floorH))
      {
        if (!CutsRows(d)) { cellH = d; break; }
      }

      return (cellW < 0 || cellH < 0) ? null : new int[] { cellW, cellH };
    }

    // ----------------------------------------------------------------------
    // Ilots
    // ----------------------------------------------------------------------

    /// <summary>
    /// Cadres des ilots de contenu, en x0,y0,x1,y1 inclus.
    /// </summary>
    /// <param name="diagonal">
    /// Vrai pour relier les pixels en diagonale. La mesure du plus grand dessin
    /// les relie - un trait fin passe souvent en biais - la decoupe non, pour ne
    /// pas coller deux sprites qui se frolent d'un coin.
    /// </param>
    List<int[]> Islands(bool diagonal)
    {
      var boxes = new List<int[]>();
      var seen = new bool[mask.Length];
      var stack = new Stack<int>();

      for (int start = 0; start < mask.Length; start++)
      {
        if (!mask[start] || seen[start]) { continue; }

        stack.Push(start);
        seen[start] = true;

        int x0 = start % Width, x1 = x0;
        int y0 = start / Width, y1 = y0;

        while (stack.Count > 0)
        {
          int at = stack.Pop();
          int x = at % Width, y = at / Width;

          if (x < x0) { x0 = x; }
          if (x > x1) { x1 = x; }
          if (y < y0) { y0 = y; }
          if (y > y1) { y1 = y; }

          for (int dy = -1; dy <= 1; dy++)
          {
            for (int dx = -1; dx <= 1; dx++)
            {
              if (dx == 0 && dy == 0) { continue; }
              if (!diagonal && dx != 0 && dy != 0) { continue; }

              int nx = x + dx, ny = y + dy;
              if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) { continue; }

              int next = ny * Width + nx;
              if (mask[next] && !seen[next]) { seen[next] = true; stack.Push(next); }
            }
          }
        }

        boxes.Add(new int[] { x0, y0, x1, y1 });
      }

      return boxes;
    }

    /// <summary>
    /// Fusionne les cadres qui se touchent a gap pixels pres.
    ///
    /// Un personnage n'est pas toujours d'un seul tenant : une main detachee,
    /// une arme, un oeil separe par un lisere forment autant d'ilots. Sans
    /// fusion, un sprite sortirait en morceaux. On repasse jusqu'a stabilite -
    /// fusionner A et B peut les rapprocher de C.
    /// </summary>
    static List<int[]> Merge(List<int[]> boxes, int gap)
    {
      bool changed = true;

      while (changed)
      {
        changed = false;
        var merged = new List<int[]>();

        foreach (int[] box in boxes)
        {
          bool joined = false;

          foreach (int[] other in merged)
          {
            bool touches = box[0] <= other[2] + gap && other[0] <= box[2] + gap
                        && box[1] <= other[3] + gap && other[1] <= box[3] + gap;

            if (!touches) { continue; }

            other[0] = Math.Min(other[0], box[0]);
            other[1] = Math.Min(other[1], box[1]);
            other[2] = Math.Max(other[2], box[2]);
            other[3] = Math.Max(other[3], box[3]);
            joined = true;
            changed = true;
            break;
          }

          if (!joined) { merged.Add(box); }
        }

        boxes = merged;
      }

      return boxes;
    }

    // ----------------------------------------------------------------------
    // Ecriture
    // ----------------------------------------------------------------------

    Bitmap Crop(int x0, int y0, int width, int height)
    {
      var crop = new Bitmap(width, height, PixelFormat.Format32bppArgb);
      var area = new Rectangle(0, 0, width, height);
      BitmapData data = crop.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

      try
      {
        for (int y = 0; y < height; y++)
        {
          IntPtr row = IntPtr.Add(data.Scan0, y * data.Stride);
          System.Runtime.InteropServices.Marshal.Copy(
              pixels, ((y0 + y) * Width + x0) * 4, row, width * 4);
        }
      }
      finally
      {
        crop.UnlockBits(data);
      }

      return crop;
    }

    byte[] Bytes(int x0, int y0, int width, int height)
    {
      var raw = new byte[width * height * 4];
      for (int y = 0; y < height; y++)
      {
        Array.Copy(pixels, ((y0 + y) * Width + x0) * 4, raw, y * width * 4, width * 4);
      }
      return raw;
    }

    int Filled(int x0, int y0, int width, int height)
    {
      int count = 0;
      for (int y = y0; y < y0 + height; y++)
      {
        for (int x = x0; x < x0 + width; x++)
        {
          if (mask[y * Width + x]) { count++; }
        }
      }
      return count;
    }

    /// <summary>
    /// Decoupe en grille. Les cases sont enregistrees ENTIERES, pas rognees : la
    /// place du dessin dans sa case porte l'alignement de l'animation, et la
    /// rogner ferait sautiller le personnage.
    /// </summary>
    /// <returns>{ images ecrites, doublons ecartes }</returns>
    public int[] SliceGrid(string target, int cellW, int cellH, int minPixels,
                           bool dedupe, bool contact)
    {
      Directory.CreateDirectory(target);

      int cols = Width / cellW, rows = Height / cellH;
      var written = new List<string>();
      var seen = new HashSet<string>();
      int duplicates = 0;

      for (int row = 0; row < rows; row++)
      {
        for (int col = 0; col < cols; col++)
        {
          int x0 = col * cellW, y0 = row * cellH;

          if (Filled(x0, y0, cellW, cellH) < minPixels) { continue; }

          if (dedupe)
          {
            string signature = Convert.ToBase64String(Bytes(x0, y0, cellW, cellH));
            if (!seen.Add(signature)) { duplicates++; continue; }
          }

          string name = string.Format("r{0:00}c{1:00}.png", row, col);
          using (Bitmap crop = Crop(x0, y0, cellW, cellH))
          {
            crop.Save(Path.Combine(target, name), ImageFormat.Png);
          }
          written.Add(name);
        }
      }

      if (contact && written.Count > 0) { Contact(target, written, cellW, cellH); }

      return new int[] { written.Count, duplicates };
    }

    /// <summary>
    /// Decoupe sprite par sprite, chacun a sa taille reelle. Aucun rembourrage
    /// et aucun index : un fichier par sprite, nomme sNNN.png. La forge liste le
    /// repertoire et lit la taille de chaque PNG.
    /// </summary>
    /// <returns>{ ecrits, doublons, largeur mini, hauteur mini, largeur maxi, hauteur maxi }</returns>
    public int[] SliceIslands(string target, int gap, int minSide, int minPixels,
                              bool dedupe, bool contact)
    {
      List<int[]> boxes = Merge(Islands(false), gap);
      var kept = new List<int[]>();

      foreach (int[] box in boxes)
      {
        int width = box[2] - box[0] + 1, height = box[3] - box[1] + 1;

        if (width < minSide || height < minSide) { continue; }
        if (Filled(box[0], box[1], width, height) < minPixels) { continue; }

        kept.Add(box);
      }

      // De haut en bas puis de gauche a droite : l'ordre de lecture d'une
      // planche, donc celui des poses d'une animation.
      kept.Sort(delegate (int[] a, int[] b)
      {
        return a[1] != b[1] ? a[1].CompareTo(b[1]) : a[0].CompareTo(b[0]);
      });

      if (kept.Count == 0) { return new int[] { 0, 0, 0, 0, 0, 0 }; }

      Directory.CreateDirectory(target);

      var written = new List<string>();
      var seen = new HashSet<string>();
      int duplicates = 0;
      int minW = int.MaxValue, minH = int.MaxValue, maxW = 0, maxH = 0;

      for (int i = 0; i < kept.Count; i++)
      {
        int[] box = kept[i];
        int width = box[2] - box[0] + 1, height = box[3] - box[1] + 1;

        if (dedupe)
        {
          string signature = Convert.ToBase64String(Bytes(box[0], box[1], width, height));
          if (!seen.Add(signature)) { duplicates++; continue; }
        }

        string name = string.Format("s{0:000}.png", i);
        using (Bitmap crop = Crop(box[0], box[1], width, height))
        {
          crop.Save(Path.Combine(target, name), ImageFormat.Png);
        }
        written.Add(name);

        minW = Math.Min(minW, width); maxW = Math.Max(maxW, width);
        minH = Math.Min(minH, height); maxH = Math.Max(maxH, height);
      }

      if (written.Count == 0) { return new int[] { 0, duplicates, 0, 0, 0, 0 }; }

      if (contact) { Contact(target, written, maxW, maxH); }

      return new int[] { written.Count, duplicates, minW, minH, maxW, maxH };
    }

    /// <summary>
    /// Planche de contact : toutes les images retenues, cote a cote. Sert a
    /// choisir une pose d'un coup d'oeil plutot qu'a ouvrir cent fichiers.
    /// Prefixee d'un souligne pour rester en tete de liste, et c'est aussi ce
    /// qui la fait ignorer par la forge.
    ///
    /// Les sprites n'ayant pas forcement la meme taille, chacun est centre dans
    /// une case aux dimensions du plus grand.
    /// </summary>
    static void Contact(string target, List<string> names, int cellW, int cellH)
    {
      int columns = 12;
      int rows = (names.Count + columns - 1) / columns;

      using (var sheet = new Bitmap(columns * cellW, rows * cellH, PixelFormat.Format32bppArgb))
      {
        using (var canvas = Graphics.FromImage(sheet))
        {
          // Recopier et non composer : une tuile posee sur du transparent doit
          // arriver telle quelle. En composition normale, GDI+ melange, et les
          // pixels transparents ressortent avec une autre teinte sous leur
          // alpha - invisible, mais ce n'est plus la meme image.
          canvas.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

          for (int i = 0; i < names.Count; i++)
          {
            using (var tile = new Bitmap(Path.Combine(target, names[i])))
            {
              int x = (i % columns) * cellW + (cellW - tile.Width) / 2;
              int y = (i / columns) * cellH + (cellH - tile.Height) / 2;
              canvas.DrawImage(tile, x, y, tile.Width, tile.Height);
            }
          }
        }

        sheet.Save(Path.Combine(target, "_contact.png"), ImageFormat.Png);
      }
    }
  }
}
'@
}

# ---------------------------------------------------------------------------
# Reglages
# ---------------------------------------------------------------------------

$key = $null
if ($Bg -and $Bg.ToLower() -ne 'none') {
    $raw = $Bg.TrimStart('#')
    if ($raw.Length -ne 6) {
        Write-Error "-Bg attend six chiffres hexadecimaux, ex: FF00FF"
        exit 1
    }
    $key = [int[]] @(
        [Convert]::ToInt32($raw.Substring(0, 2), 16),
        [Convert]::ToInt32($raw.Substring(2, 2), 16),
        [Convert]::ToInt32($raw.Substring(4, 2), 16)
    )
}

if (-not (Test-Path -LiteralPath $Src -PathType Container)) {
    Write-Error "Repertoire introuvable : $Src"
    exit 1
}

$srcPath = (Resolve-Path -LiteralPath $Src).Path
$outPath = [IO.Path]::GetFullPath([IO.Path]::Combine($srcPath, $Out))

# Toutes les extensions que System.Drawing ouvre couramment : une planche ripee
# est aussi souvent un GIF ou un BMP qu'un PNG, et refuser le fichier pour son
# extension etait la moitie du probleme.
$extensions = @('.png', '.gif', '.bmp', '.jpg', '.jpeg', '.tif', '.tiff')

$sources = Get-ChildItem -LiteralPath $srcPath -File |
    Where-Object { $extensions -contains $_.Extension.ToLower() } |
    Where-Object { $_.Name -like $Only } |
    # Les images deja produites par une passe precedente ne sont pas des sources.
    Where-Object { -not $_.FullName.StartsWith($outPath, [StringComparison]::OrdinalIgnoreCase) } |
    Sort-Object Name

if (-not $sources) {
    Write-Host "Aucune planche ne correspond a '$Only' dans $srcPath"
    exit 1
}

$dedupe = -not $KeepDuplicates
$contact = -not $NoContact

New-Item -ItemType Directory -Force -Path $outPath | Out-Null

# ---------------------------------------------------------------------------

function Invoke-Islands {
    param([SpriteSheets.Sheet] $Sheet, [string] $Name)

    $target = Join-Path $outPath ([IO.Path]::GetFileNameWithoutExtension($Name))
    $result = $Sheet.SliceIslands($target, $Gap, $MinSide, $MinPixels, $dedupe, $contact)

    if ($result[0] -eq 0) {
        Write-Host "${Name}: aucun sprite retenu"
        return 0
    }

    $line = "${Name}: $($result[0]) sprites, de $($result[2])x$($result[3]) a $($result[4])x$($result[5])"
    if ($Sheet.Background) { $line += ", fond $($Sheet.Background -join ',')" }
    if ($result[1]) { $line += ", $($result[1]) doublons ecartes" }

    Write-Host $line
    return $result[0]
}

# Le mode islands ne cherche aucune grille : il n'a donc besoin ni de la passe
# d'analyse ni de l'emprunt entre planches.
if ($Mode -eq 'islands') {
    $total = 0

    foreach ($file in $sources) {
        try {
            $sheet = [SpriteSheets.Sheet]::Load($file.FullName, $key)
            try { $total += Invoke-Islands -Sheet $sheet -Name $file.Name }
            finally { $sheet.Dispose() }
        }
        catch {
            Write-Host "$($file.Name): ECHEC - $($_.Exception.Message)"
        }
    }

    Write-Host ""
    Write-Host "$($sources.Count) planches, $total sprites ecrits dans $outPath"
    exit 0
}

# Premiere passe : deduire ce qui se deduit. C'est la partie couteuse, elle
# n'ecrit rien - de quoi corriger les planches recalcitrantes avant d'ecrire quoi
# que ce soit.
Write-Host "Analyse de $($sources.Count) planches..."

$sheets = @()
$empty = 0

foreach ($file in $sources) {
    try {
        $sheet = [SpriteSheets.Sheet]::Load($file.FullName, $key)
    }
    catch {
        Write-Host "  $($file.Name): ECHEC - $($_.Exception.Message)"
        continue
    }

    if ($sheet.IsEmpty) {
        $sheet.Dispose()
        $empty++
        continue
    }

    $island = $sheet.LargestIsland()
    $origin = 'deduite'

    # Surtout pas $cell : PowerShell ne distingue pas la casse, et ce nom-la est
    # deja celui du parametre -Cell, type [int]. Lui affecter une paire de
    # dimensions echoue avec une erreur de conversion qui ne dit pas son nom.
    $cellSize = $null

    if ($Cell -gt 0) {
        $cellSize = [int[]] @($Cell, $Cell)
        $origin = 'imposee'
    }
    else {
        $cellSize = $sheet.DetectGrid()

        # Une case plus grande que MaxCell ne contient plus un sprite mais un
        # groupe : c'est la signature de dessins qui se touchent. On la refuse
        # plutot que de la prendre pour une reponse.
        if ($cellSize -and ($cellSize[0] -gt $MaxCell -or $cellSize[1] -gt $MaxCell)) {
            $cellSize = $null
        }
    }

    $sheets += [pscustomobject]@{
        Sheet  = $sheet
        Name   = $file.Name
        Size   = "$($sheet.Width)x$($sheet.Height)"
        Island = $island
        Cell   = $cellSize
        Origin = $origin
    }
}

# Seconde passe : les planches sans reponse empruntent la taille de case la plus
# repandue chez celles de MEME DIMENSION. Deux planches de meme format dans un
# meme jeu se decoupent presque toujours pareil.
#
# Il n'y a volontairement pas de troisieme reponse par la taille la plus repandue
# du lot : une planche de gros ennemis n'a rien a emprunter a des planches de
# personnages ordinaires, et 32 pixels pour un dessin qui en fait 79 le decoupe
# en seize morceaux.
$bySize = @{}
foreach ($entry in $sheets) {
    if (-not $entry.Cell) { continue }
    if (-not $bySize.ContainsKey($entry.Size)) { $bySize[$entry.Size] = @() }
    $bySize[$entry.Size] += , $entry.Cell
}

$resolved = 0
$unresolved = @()

foreach ($entry in $sheets) {
    if ($entry.Cell) { continue }

    $answer = $null
    $origin = $null

    $peers = $bySize[$entry.Size]
    if ($peers) {
        $answer = ($peers | Group-Object { "$($_[0])x$($_[1])" } |
            Sort-Object Count -Descending | Select-Object -First 1).Group[0]
        $origin = 'empruntee'
    }
    else {
        # A defaut, la plus petite case pouvant contenir le plus grand dessin.
        # Une case ne peut pas etre plus petite que ce qu'elle doit contenir -
        # c'est une borne, pas une devinette.
        $floor = [Math]::Max([SpriteSheets.Sheet]::MinCell,
                 [Math]::Max($entry.Island[0], $entry.Island[1]))

        $square = [SpriteSheets.Sheet]::Divisors($entry.Sheet.Width, $floor) |
            Where-Object { $entry.Sheet.Height % $_ -eq 0 } | Select-Object -First 1

        if ($square) {
            $answer = [int[]] @($square, $square)
            $origin = 'minorante'
        }
        else {
            $w = [SpriteSheets.Sheet]::Divisors($entry.Sheet.Width,
                 [Math]::Max([SpriteSheets.Sheet]::MinCell, $entry.Island[0])) | Select-Object -First 1
            $h = [SpriteSheets.Sheet]::Divisors($entry.Sheet.Height,
                 [Math]::Max([SpriteSheets.Sheet]::MinCell, $entry.Island[1])) | Select-Object -First 1

            if ($w -and $h) {
                $answer = [int[]] @($w, $h)
                $origin = 'minorante'
            }
        }
    }

    # "minorante" n'est pas une reponse, c'est un aveu : la grille n'a pas ete
    # deduite et l'on s'est rabattu sur la taille du plus grand ilot. En mode
    # auto, une planche dans ce cas est justement celle qu'il faut reprendre
    # sprite par sprite - l'accepter ici la condamnerait a un decoupage
    # arbitraire, sans jamais atteindre le repli sans grille.
    if ($answer -and -not ($Mode -eq 'auto' -and $origin -eq 'minorante')) {
        $entry.Cell = $answer
        $entry.Origin = $origin
        $resolved++
    }
    else {
        $unresolved += $entry
    }
}

if ($resolved) {
    Write-Host "$resolved planches sans grille nette : taille deduite du lot."
    Write-Host ""
}

$total = 0

foreach ($entry in $sheets) {
    if (-not $entry.Cell) { continue }

    try {
        $target = Join-Path $outPath ([IO.Path]::GetFileNameWithoutExtension($entry.Name))
        $result = $entry.Sheet.SliceGrid($target, $entry.Cell[0], $entry.Cell[1],
                                         $MinPixels, $dedupe, $contact)

        $line = "$($entry.Name): $($result[0]) images, case $($entry.Cell[0])x$($entry.Cell[1]) ($($entry.Origin))"
        if ($result[1]) { $line += ", $($result[1]) doublons ecartes" }

        $total += $result[0]
    }
    catch {
        # Une planche cassee ne doit pas tout arreter.
        $line = "$($entry.Name): ECHEC - $($_.Exception.Message)"
    }

    Write-Host $line
}

# Ce que la grille n'a pas su decouper n'est plus abandonne : on le reprend
# sprite par sprite. C'est exactement le cas des planches a fond colore, dont les
# dessins ne sont ni alignes ni de meme taille.
if ($unresolved) {
    Write-Host ""
    Write-Host "$($unresolved.Count) planches sans grille reguliere : reprise sprite par sprite."

    foreach ($entry in $unresolved) {
        try { $total += Invoke-Islands -Sheet $entry.Sheet -Name $entry.Name }
        catch { Write-Host "$($entry.Name): ECHEC - $($_.Exception.Message)" }
    }
}

foreach ($entry in $sheets) { $entry.Sheet.Dispose() }

Write-Host ""
Write-Host "$($sheets.Count) planches, $total images ecrites dans $outPath"

if ($empty) { Write-Host "$empty planches entierement vides, ignorees." }
