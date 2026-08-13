using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseArcher
{
  /// <summary>Une planche du vivier : un repertoire, et les images qu'il contient.</summary>
  public sealed class ForgeSource
  {
    public string Name;
    public string Dir;

    /// <summary>
    /// Les images du repertoire, triees par nom. Relevees sur le disque et nulle
    /// part ailleurs : il n'y a plus d'index a tenir a jour, donc plus rien qui
    /// puisse se desynchroniser du contenu reel.
    /// </summary>
    public List<ForgeCell> Cells = new List<ForgeCell>();

    public int FrameCount => Cells.Count;

    /// <summary>Vrai si la forge sait tirer quelque chose de cette planche.</summary>
    public bool CanPick => Cells.Count > 0;

    public override string ToString()
    {
      return Name;
    }
  }

  /// <summary>
  /// Le vivier : les images individuelles dans lesquelles la forge puise.
  ///
  /// Un repertoire par planche, un fichier PNG par pose. Rien d'autre : pas
  /// d'index, pas de grille, aucun format propre a la forge. Ce qui se regarde dans
  /// un explorateur de fichiers se debogue sans le jeu, une image ajoutee ou
  /// remplacee a la main est vue telle quelle, et la taille d'une pose est celle de
  /// son fichier - il n'existe aucune seconde source de verite avec laquelle elle
  /// pourrait diverger.
  ///
  /// Les fichiers dont le nom commence par un souligne sont ignores : ce sont les
  /// annexes du decoupage, la planche de contact notamment.
  /// </summary>
  public static class ForgeBank
  {
    private const string DirName = "sprites";
    private const string PathFile = "sprites.path";

    /// <summary>Les extensions reconnues comme images.</summary>
    private static readonly string[] Extensions = [".png"];

    private static List<ForgeSource> sources;
    private static string rootOverride;

    private static string StorageRoot => TFModFortRiseArcherModule.Instance.Context.Storage.StoragePath;

    /// <summary>
    /// Ou vivent les images.
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

    /// <summary>
    /// Relit le vivier. Appele a chaque entree dans la forge : un decoupage relance
    /// a cote pendant que le jeu tourne doit etre vu sans redemarrer.
    /// </summary>
    public static void Refresh()
    {
      rootOverride = null;
      var found = new List<ForgeSource>();

      try
      {
        if (!Directory.Exists(Root))
        {
          // Cree le repertoire au premier passage : le joueur doit pouvoir y deposer
          // ses images sans avoir a deviner le chemin.
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
    /// Une planche, ou null si le repertoire ne contient aucune image.
    ///
    /// Aucun fichier de description n'est requis ni lu : la planche EST son
    /// repertoire.
    /// </summary>
    private static ForgeSource Read(string dir)
    {
      try
      {
        var source = new ForgeSource
        {
          Name = Path.GetFileName(dir),
          Dir = dir
        };

        foreach (string file in Directory.GetFiles(dir))
        {
          if (Array.IndexOf(Extensions, Path.GetExtension(file).ToLowerInvariant()) < 0)
          {
            continue;
          }

          string name = Path.GetFileNameWithoutExtension(file);

          if (name.StartsWith("_", StringComparison.Ordinal))
          {
            continue;
          }

          source.Cells.Add(new ForgeCell(name));
        }

        if (source.Cells.Count == 0)
        {
          return null;
        }

        source.Cells.Sort((a, b) => string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase));
        return source;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] {Path.GetFileName(dir)} illisible : {e.Message}");
        return null;
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
    public static int PickableCount => PickableSources().Count;

    /// <summary>
    /// Combien de planches du vivier la forge ne sait pas exploiter.
    ///
    /// Vaut zero par construction depuis qu'une planche est simplement un
    /// repertoire d'images : un repertoire sans image n'est plus liste du tout. Le
    /// compte est garde parce que l'ecran l'affiche encore.
    /// </summary>
    public static int UnusableCount()
    {
      return Sources.Count - PickableCount;
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

    /// <summary>Les images d'une planche, dans l'ordre des noms de fichiers.</summary>
    public static List<ForgeCell> CellsOf(ForgeSource source)
    {
      return source == null ? new List<ForgeCell>() : new List<ForgeCell>(source.Cells);
    }

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
    /// Les pixels d'une image, et sa taille reelle.
    ///
    /// Aucune taille attendue n'est comparee : le fichier EST la reference. C'est
    /// ce qui permet de remplacer une image ou de relancer un decoupage pendant que
    /// le jeu tourne sans que rien ne devienne illisible.
    /// </summary>
    public static Color[] ReadCell(ForgeSource source, ForgeCell cell, out Point size)
    {
      size = Point.Zero;
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

        size = new Point(texture.Width, texture.Height);

        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return pixels;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] {source?.Name}/{cell} illisible : {e.Message}");
        return null;
      }
      finally
      {
        // La texture n'a servi qu'a decoder le PNG. La garder mettrait une image
        // sur la carte graphique par case survolee.
        try { texture?.Dispose(); } catch { }
      }
    }

    /// <summary>
    /// Les pixels d'une image, ramenee dans la fenetre de decoupe si elle deborde.
    ///
    /// Le rapport largeur/hauteur est conserve : etirer une pose de 71x136 sur un
    /// carre de 24 l'ecraserait. La reduction se fait au plus proche voisin, sans
    /// interpolation - un sprite a bords francs doit le rester.
    ///
    /// Et on REDUIT seulement. Agrandir au plus proche voisin doublerait ou
    /// triplerait les pixels, et une pose de 6x7 portee a 21x24 jurerait au milieu
    /// d'un archer dessine au pixel. Mieux vaut une petite pose fidele qu'une
    /// grande en gros carres.
    /// </summary>
    public static Color[] ReadCellFitted(ForgeSource source, ForgeCell cell, out Point size)
    {
      Color[] pixels = ReadCell(source, cell, out size);

      if (pixels == null || size.X <= 0 || size.Y <= 0)
      {
        return pixels;
      }

      int frame = ForgeSlots.Frame;
      float scale = Math.Min(frame / (float)size.X, frame / (float)size.Y);

      if (scale >= 1f)
      {
        return pixels;
      }

      int width = Math.Max(1, (int)Math.Round(size.X * scale));
      int height = Math.Max(1, (int)Math.Round(size.Y * scale));
      var scaled = new Color[width * height];

      for (int y = 0; y < height; y++)
      {
        int sourceY = Math.Min(size.Y - 1, (int)(y / scale));

        for (int x = 0; x < width; x++)
        {
          int sourceX = Math.Min(size.X - 1, (int)(x / scale));
          scaled[y * width + x] = pixels[sourceY * size.X + sourceX];
        }
      }

      size = new Point(width, height);
      return scaled;
    }
  }
}
