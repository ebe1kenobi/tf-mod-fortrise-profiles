using System.Collections.Generic;

namespace TFModFortRiseProfiles
{
  /// <summary>Une case de la planche source : colonne et ligne.</summary>
  public struct ForgeCell
  {
    public int Col;
    public int Row;

    public ForgeCell(int col, int row)
    {
      Col = col;
      Row = row;
    }

    public override string ToString()
    {
      return "r" + Row.ToString("00") + "c" + Col.ToString("00");
    }
  }

  /// <summary>
  /// La mise en page canonique des planches sources.
  ///
  /// Les planches Broforce ont toutes la meme : la case (0,0) donne la pose debout,
  /// (21,4) le saut, (16,7) le cadavre au sol, et ainsi de suite. Verifie sur huit
  /// personnages avant d'ecrire cette table - les coordonnees codees en dur dans le
  /// script de l'archer Brones ne valaient donc pas que pour lui, elles valent pour
  /// la planche entiere du jeu.
  ///
  /// C'est ce qui rend la forge supportable : choisir une planche source pre-remplit
  /// les seize emplacements d'un coup, et l'on n'ouvre le detail que pour ce qui
  /// cloche. Sans cette table il faudrait seize parcours dans trente mille images.
  ///
  /// Une case peut etre vide chez un personnage donne - celle de l'esquive l'est
  /// chez BROBOCOP. La forge doit alors le dire et laisser l'emplacement a remplir,
  /// plutot que de livrer une image transparente sans rien signaler.
  /// </summary>
  public static class ForgeLayout
  {
    private static readonly Dictionary<string, ForgeCell> cells = new Dictionary<string, ForgeCell>
    {
      { "stand", new ForgeCell(0, 0) },
      { "run1", new ForgeCell(1, 1) },
      { "run2", new ForgeCell(3, 1) },
      { "run3", new ForgeCell(5, 1) },
      { "ledge", new ForgeCell(16, 3) },
      { "jump", new ForgeCell(21, 4) },
      { "fall", new ForgeCell(22, 4) },
      { "dodge", new ForgeCell(21, 1) },
      { "slide", new ForgeCell(11, 5) },
      { "duck", new ForgeCell(12, 1) },
      { "corpse_ground", new ForgeCell(16, 7) },
      { "corpse_fall", new ForgeCell(16, 6) },
      { "corpse_pinned", new ForgeCell(17, 6) },
      { "corpse_slouched", new ForgeCell(17, 7) },
      { "corpse_flying", new ForgeCell(19, 6) },
      { "corpse_ledge", new ForgeCell(18, 7) }
    };

    /// <summary>Case canonique d'un emplacement, ou null si la table ne le connait pas.</summary>
    public static ForgeCell? Of(string slotKey)
    {
      if (slotKey != null && cells.TryGetValue(slotKey, out ForgeCell cell))
      {
        return cell;
      }

      return null;
    }

    /// <summary>
    /// Vrai si une planche a la forme que cette table suppose : assez grande pour
    /// que toutes les cases citees existent.
    ///
    /// Une planche trop petite n'est pas une planche de personnage - c'est un decor
    /// ou un effet. La proposer comme source ferait des archers vides.
    /// </summary>
    public static bool Fits(int cols, int rows)
    {
      foreach (ForgeCell cell in cells.Values)
      {
        if (cell.Col >= cols || cell.Row >= rows)
        {
          return false;
        }
      }

      return true;
    }
  }
}
