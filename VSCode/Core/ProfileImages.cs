using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseProfiles
{
  /// <summary>Un PNG du vivier, quelle que soit sa provenance.</summary>
  public sealed class ImageFile
  {
    public string Name;
    /// <summary>"MOD" pour une image livree avec le mod, "USER" pour une image deposee.</summary>
    public string Source;
    private readonly Func<Stream> open;

    internal ImageFile(string name, string source, Func<Stream> open)
    {
      Name = name;
      Source = source;
      this.open = open;
    }

    public Stream Open()
    {
      return open();
    }
  }

  /// <summary>
  /// Images personnelles d'un profil : le portrait de l'ecran de selection et ceux de
  /// l'ecran de resultats.
  ///
  /// Meme organisation que les sons : un vivier commun ou l'on depose ses fichiers, et
  /// une copie dans le dossier du profil au moment de l'affectation. La difference est
  /// qu'un emplacement ne recoit qu'une image, la ou un evenement sonore en accepte
  /// plusieurs - il n'y a rien a tirer au sort ici.
  ///
  /// Les six emplacements suivent le costume : un profil en tenue alternative prend
  /// les variantes ALT. C'est la meme distinction que le jeu fait dans ses propres
  /// portraits.
  /// </summary>
  public static class ProfileImages
  {
    public const string Archer = "ARCHER";
    public const string ArcherAlt = "ARCHER_ALT";
    public const string Win = "WIN";
    public const string WinAlt = "WIN_ALT";
    public const string Lose = "LOSE";
    public const string LoseAlt = "LOSE_ALT";

    public static readonly string[] Slots =
    {
      Archer, ArcherAlt, Win, WinAlt, Lose, LoseAlt
    };

    private const string PoolDirName = "images";
    private const string ProfileDirName = "images";
    private const string ModPoolPath = "Content/images";
    private const string Extension = ".png";

    // <id de profil> -> <emplacement> -> image chargee
    private static readonly Dictionary<string, Dictionary<string, Subtexture>> loaded =
        new Dictionary<string, Dictionary<string, Subtexture>>();

    private static List<ImageFile> pool;

    private static string StorageRoot => TFModFortRiseProfilesModule.Instance.Context.Storage.StoragePath;

    public static string PoolDir => Path.Combine(StorageRoot, PoolDirName);

    public static string DirOf(ProfileData profile)
    {
      return Path.Combine(ProfileSfx.DirOf(profile.Name), ProfileDirName);
    }

    private static string PathOf(ProfileData profile, string slot)
    {
      return Path.Combine(DirOf(profile), slot + Extension);
    }

    // ------------------------------------------------------------------
    // Vivier
    // ------------------------------------------------------------------

    public static IReadOnlyList<ImageFile> Pool
    {
      get
      {
        if (pool == null)
        {
          RefreshPool();
        }

        return pool;
      }
    }

    public static void RefreshPool()
    {
      var found = new List<ImageFile>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      try
      {
        if (Directory.Exists(PoolDir))
        {
          foreach (string path in Directory.GetFiles(PoolDir, "*" + Extension))
          {
            string name = Path.GetFileName(path);
            if (seen.Add(name))
            {
              string captured = path;
              found.Add(new ImageFile(name, "USER", () => File.OpenRead(captured)));
            }
          }
        }
        else
        {
          // Cree le dossier au premier passage : le joueur doit pouvoir y deposer ses
          // images sans avoir a deviner le chemin.
          Directory.CreateDirectory(PoolDir);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Images] vivier utilisateur illisible : {e.Message}");
      }

      try
      {
        IModContent content = TFModFortRiseProfilesModule.Instance.ModContent;
        if (content != null && content.TryGetResource(ModPoolPath, out IResourceInfo root) && root?.Childrens != null)
        {
          foreach (IResourceInfo child in root.Childrens)
          {
            if (child.Name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && seen.Add(child.Name))
            {
              IResourceInfo captured = child;
              found.Add(new ImageFile(child.Name, "MOD", () => captured.Stream));
            }
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Images] vivier du mod illisible : {e.Message}");
      }

      found.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
      pool = found;
    }

    // ------------------------------------------------------------------
    // Affectation
    // ------------------------------------------------------------------

    public static bool HasImage(ProfileData profile, string slot)
    {
      try
      {
        return profile != null && File.Exists(PathOf(profile, slot));
      }
      catch
      {
        return false;
      }
    }

    /// <summary>Copie l'image du vivier dans le dossier du profil, en remplacant l'ancienne.</summary>
    public static bool Assign(ProfileData profile, string slot, ImageFile file)
    {
      try
      {
        string dir = DirOf(profile);
        Directory.CreateDirectory(dir);

        using (Stream source = file.Open())
        using (FileStream target = File.Create(PathOf(profile, slot)))
        {
          source.CopyTo(target);
        }

        Invalidate(profile);
        return true;
      }
      catch (Exception e)
      {
        Log.Error($"[Images] copie de {file.Name} impossible : {e.Message}");
        return false;
      }
    }

    public static void Unassign(ProfileData profile, string slot)
    {
      try
      {
        string path = PathOf(profile, slot);
        if (File.Exists(path))
        {
          File.Delete(path);
        }

        Invalidate(profile);
      }
      catch (Exception e)
      {
        Log.Error($"[Images] suppression impossible : {e.Message}");
      }
    }

    /// <summary>Suit un renommage de profil : le dossier d'images vit sous celui du profil.</summary>
    public static void OnProfileRenamed(ProfileData profile)
    {
      Invalidate(profile);
    }

    // ------------------------------------------------------------------
    // Lecture
    // ------------------------------------------------------------------

    /// <summary>Image d'un emplacement, ou null si le profil n'en a pas.</summary>
    public static Subtexture Get(ProfileData profile, string slot)
    {
      if (profile == null || string.IsNullOrEmpty(slot))
      {
        return null;
      }

      if (!loaded.TryGetValue(profile.Id, out var slots))
      {
        slots = new Dictionary<string, Subtexture>();
        loaded[profile.Id] = slots;
      }

      if (slots.TryGetValue(slot, out Subtexture cached))
      {
        return cached;
      }

      Subtexture built = null;

      try
      {
        string path = PathOf(profile, slot);
        if (File.Exists(path))
        {
          using FileStream stream = File.OpenRead(path);
          Texture2D texture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);
          if (texture != null)
          {
            built = new Subtexture(new Monocle.Texture(texture));
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Images] {slot} illisible : {e.Message}");
      }

      slots[slot] = built;
      return built;
    }

    /// <summary>Portrait de l'ecran de selection, selon le costume du profil.</summary>
    public static Subtexture ForArcher(ProfileData profile)
    {
      return Get(profile, profile != null && profile.IsAlt ? ArcherAlt : Archer);
    }

    /// <summary>Portrait de l'ecran de resultats, selon l'issue et le costume.</summary>
    public static Subtexture ForResult(ProfileData profile, bool won)
    {
      if (profile == null)
      {
        return null;
      }

      if (won)
      {
        return Get(profile, profile.IsAlt ? WinAlt : Win);
      }

      return Get(profile, profile.IsAlt ? LoseAlt : Lose);
    }

    public static void Invalidate(ProfileData profile)
    {
      if (profile == null)
      {
        return;
      }

      if (!loaded.TryGetValue(profile.Id, out var slots))
      {
        return;
      }

      foreach (Subtexture subtexture in slots.Values)
      {
        try { subtexture?.Texture2D?.Dispose(); } catch { }
      }

      loaded.Remove(profile.Id);
    }

    /// <summary>Libelle lisible d'un emplacement : "ARCHER ALT" plutot que "ARCHER_ALT".</summary>
    public static string Label(string slot)
    {
      return slot.Replace('_', ' ');
    }
  }
}
