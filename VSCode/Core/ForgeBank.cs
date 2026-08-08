using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseProfiles
{
  /// <summary>Une planche decoupee du vivier, telle que son index.json la decrit.</summary>
  public sealed class ForgeSource
  {
    public string Name;
    public string Dir;
    public int CellWidth;
    public int CellHeight;
    public int Cols;
    public int Rows;
    public int FrameCount;

    /// <summary>
    /// Cases que le decoupage a ecartees parce qu'elles etaient identiques a une
    /// autre. Voir <see cref="ForgeBank.Duplicates"/> : ce n'est pas anodin ici.
    /// </summary>
    public int DuplicatesDropped;

    /// <summary>
    /// Vrai si la forge sait tirer quelque chose de cette planche.
    ///
    /// La condition est la taille des cases, et rien d'autre. Toute la geometrie de
    /// la forge - fenetre de decoupe en (3,7), image de sortie de 24 - a ete relevee
    /// sur une case Broforce de 32. Appliquee a une case de 64, la meme fenetre
    /// prelevle un coin de la creature au lieu de la creature : pas une erreur, une
    /// image silencieusement fausse, ce qui est pire.
    ///
    /// On ne filtre volontairement PAS sur la taille du dessin, qui semblerait
    /// pourtant plus direct : la planche d'Indianna, celle qui a produit l'archer de
    /// reference, contient des images allant jusqu'a 26x26 pour une sortie de 24.
    /// Ce sont des poses a fouet tendu, dont aucune ne figure parmi les seize a
    /// choisir. Ecarter la planche pour ces quelques images ecarterait la meilleure
    /// source du vivier.
    /// </summary>
    public bool CanPick;

    /// <summary>
    /// Vrai si la table des coordonnees canoniques s'applique a cette planche :
    /// exploitable, et grille assez grande pour que les seize poses existent. Les
    /// autres restent choisissables case par case, mais ne peuvent pas pre-remplir un
    /// archer d'un coup.
    /// </summary>
    public bool CanPrefill;

    public override string ToString()
    {
      return Name;
    }
  }

  /// <summary>
  /// Le vivier : les images individuelles decoupees par script/slice_sheets.py.
  ///
  /// Un repertoire par planche source, une image par case non vide, et un index.json
  /// portant la taille des cases et la grille. La forge lit ces repertoires tels
  /// quels - elle n'a besoin de rien d'autre, et surtout d'aucun format qui lui soit
  /// propre : ce qui se regarde dans un explorateur de fichiers se debogue sans le
  /// jeu.
  ///
  /// Une case vide n'a pas de fichier. C'est le signal dont la forge se sert pour
  /// dire qu'un emplacement reste a remplir, plutot que de livrer une pose
  /// transparente sans rien signaler.
  /// </summary>
  public static class ForgeBank
  {
    private const string DirName = "sprites";
    private const string PathFile = "sprites.path";

    private static List<ForgeSource> sources;
    private static string rootOverride;

    private static string StorageRoot => TFModFortRiseProfilesModule.Instance.Context.Storage.StoragePath;

    /// <summary>
    /// Ou vivent les images decoupees.
    ///
    /// Par defaut a cote des profils, ou un `slice_sheets.py --out` les depose. Un
    /// fichier `sprites.path` contenant un chemin le remplace : trente mille fichiers
    /// ne se recopient pas a chaque essai, et celui qui travaille depuis son depot
    /// doit pouvoir y pointer.
    /// </summary>
    public static string Root
    {
      get
      {
        if (rootOverride != null)
        {
          return rootOverride;
        }

        try
        {
          string pointer = Path.Combine(StorageRoot, PathFile);
          if (File.Exists(pointer))
          {
            string target = File.ReadAllText(pointer).Trim();
            if (target.Length > 0 && Directory.Exists(target))
            {
              rootOverride = target;
              return rootOverride;
            }

            Log.Error($"[Forge] {PathFile} pointe sur un repertoire absent : {target}");
          }
        }
        catch (Exception e)
        {
          Log.Error($"[Forge] {PathFile} illisible : {e.Message}");
        }

        rootOverride = Path.Combine(StorageRoot, DirName);
        return rootOverride;
      }
    }

    public static IReadOnlyList<ForgeSource> Sources
    {
      get
      {
        if (sources == null)
        {
          Refresh();
        }

        return sources;
      }
    }

    public static void Refresh()
    {
      rootOverride = null;
      var found = new List<ForgeSource>();

      try
      {
        if (!Directory.Exists(Root))
        {
          // Cree le repertoire au premier passage : le joueur doit pouvoir y deposer
          // son vivier sans avoir a deviner le chemin.
          Directory.CreateDirectory(Root);
          sources = found;
          return;
        }

        foreach (string dir in Directory.GetDirectories(Root))
        {
          ForgeSource source = Read(dir);
          if (source != null)
          {
            found.Add(source);
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] vivier illisible : {e.Message}");
      }

      found.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
      sources = found;
    }

    /// <summary>
    /// Nombre de cases que le decoupage a ecartees comme doublons, sur tout le vivier.
    ///
    /// Cela demande une explication, parce que c'est un piege discret.
    /// slice_sheets.py n'ecrit qu'une fois deux cases rigoureusement identiques, ce
    /// qui est raisonnable pour ranger des images et faux pour la forge : la case
    /// ecartee n'a pas de fichier, la forge la croit vide, et l'emplacement
    /// correspondant se retrouve comble par la pose debout. Un personnage dont la
    /// deuxieme image de course est identique a la premiere se met alors a boiter,
    /// sans que rien n'ait signale d'erreur.
    ///
    /// Le vivier de la forge doit donc etre decoupe avec --keep-duplicates. Ce
    /// compte permet a l'ecran de le dire au lieu de laisser chercher.
    /// </summary>
    public static int Duplicates
    {
      get
      {
        int total = 0;

        foreach (ForgeSource source in Sources)
        {
          total += source.DuplicatesDropped;
        }

        return total;
      }
    }

    /// <summary>Les planches dans lesquelles on peut choisir une pose.</summary>
    public static List<ForgeSource> PickableSources()
    {
      var list = new List<ForgeSource>();

      foreach (ForgeSource source in Sources)
      {
        if (source.CanPick)
        {
          list.Add(source);
        }
      }

      return list;
    }

    /// <summary>Combien de planches la forge sait exploiter.</summary>
    public static int PickableCount
    {
      get
      {
        int count = 0;

        foreach (ForgeSource source in Sources)
        {
          if (source.CanPick)
          {
            count++;
          }
        }

        return count;
      }
    }

    /// <summary>
    /// Combien de planches du vivier la forge ne sait pas exploiter.
    ///
    /// Affiche a cote de la liste filtree : une planche deposee dans le vivier et
    /// absente de l'ecran ferait chercher une faute de nom ou un decoupage rate,
    /// alors qu'elle est simplement au mauvais format.
    /// </summary>
    public static int UnusableCount()
    {
      int count = 0;

      foreach (ForgeSource source in Sources)
      {
        if (!source.CanPick)
        {
          count++;
        }
      }

      return count;
    }

    /// <summary>Les planches qui peuvent pre-remplir un archer d'un coup.</summary>
    public static List<ForgeSource> PrefillableSources()
    {
      var list = new List<ForgeSource>();

      foreach (ForgeSource source in Sources)
      {
        if (source.CanPrefill)
        {
          list.Add(source);
        }
      }

      return list;
    }

    public static ForgeSource Find(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        return null;
      }

      foreach (ForgeSource source in Sources)
      {
        if (string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return source;
        }
      }

      return null;
    }

    private static ForgeSource Read(string dir)
    {
      try
      {
        string indexPath = Path.Combine(dir, "index.json");
        if (!File.Exists(indexPath))
        {
          return null;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(indexPath));
        JsonElement root = doc.RootElement;

        JsonElement cell = root.GetProperty("cell");
        JsonElement grid = root.GetProperty("grid");

        var source = new ForgeSource
        {
          Name = Path.GetFileName(dir),
          Dir = dir,
          CellWidth = cell.GetProperty("width").GetInt32(),
          CellHeight = cell.GetProperty("height").GetInt32(),
          Cols = grid.GetProperty("cols").GetInt32(),
          Rows = grid.GetProperty("rows").GetInt32(),
          FrameCount = root.TryGetProperty("frames", out JsonElement frames) ? frames.GetArrayLength() : 0,
          DuplicatesDropped = root.TryGetProperty("duplicates_dropped", out JsonElement dropped)
              ? dropped.GetInt32()
              : 0
        };

        // La fenetre de decoupe n'a de sens que sur la case pour laquelle elle a ete
        // relevee. Et la table des coordonnees ne vaut que pour la case Broforce :
        // appliquee ailleurs elle designerait des poses au hasard.
        source.CanPick =
            source.CellWidth == ForgeSlots.SourceCell
            && source.CellHeight == ForgeSlots.SourceCell;

        source.CanPrefill = source.CanPick && ForgeLayout.Fits(source.Cols, source.Rows);

        return source;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] {Path.GetFileName(dir)} illisible : {e.Message}");
        return null;
      }
    }

    // ------------------------------------------------------------------
    // Lecture des images
    // ------------------------------------------------------------------

    public static string PathOf(ForgeSource source, ForgeCell cell)
    {
      return source == null ? null : Path.Combine(source.Dir, cell + ".png");
    }

    public static bool Has(ForgeSource source, ForgeCell cell)
    {
      try
      {
        string path = PathOf(source, cell);
        return path != null && File.Exists(path);
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Les cases non vides d'une planche, dans l'ordre de lecture.
    ///
    /// Lues sur le disque et non dans l'index : c'est le fichier qui fait foi, et un
    /// index et un repertoire qui divergent doivent se resoudre en faveur de ce
    /// qu'on peut reellement afficher.
    /// </summary>
    public static List<ForgeCell> CellsOf(ForgeSource source)
    {
      var list = new List<ForgeCell>();

      if (source == null)
      {
        return list;
      }

      for (int row = 0; row < source.Rows; row++)
      {
        for (int col = 0; col < source.Cols; col++)
        {
          var cell = new ForgeCell(col, row);
          if (Has(source, cell))
          {
            list.Add(cell);
          }
        }
      }

      return list;
    }

    /// <summary>
    /// Pixels d'une case, ou null si elle est vide. Le tableau fait toujours la
    /// taille de la case declaree : la forge decoupe dedans a coordonnees fixes, une
    /// image plus petite lui ferait lire hors des clous.
    /// </summary>
    public static Color[] ReadCell(ForgeSource source, ForgeCell cell)
    {
      string path = PathOf(source, cell);

      if (path == null || !File.Exists(path))
      {
        return null;
      }

      Texture2D texture = null;

      try
      {
        using FileStream stream = File.OpenRead(path);
        texture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);

        if (texture == null)
        {
          return null;
        }

        if (texture.Width != source.CellWidth || texture.Height != source.CellHeight)
        {
          Log.Error($"[Forge] {source.Name}/{cell} fait {texture.Width}x{texture.Height}, "
                    + $"attendu {source.CellWidth}x{source.CellHeight}");
          return null;
        }

        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return pixels;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] {source.Name}/{cell} illisible : {e.Message}");
        return null;
      }
      finally
      {
        // La texture n'a servi qu'a decoder le PNG. La garder mettrait une image de
        // trente-deux pixels sur la carte graphique par case survolee.
        try { texture?.Dispose(); } catch { }
      }
    }
  }
}
