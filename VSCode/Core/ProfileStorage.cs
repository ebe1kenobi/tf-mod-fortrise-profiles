using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Lecture et ecriture de la liste des profils.
  ///
  /// Le fichier vit dans l'espace de sauvegarde du mod fourni par FortRise
  /// (FortRise/Saves/Profiles/Profiles.profiles.json), au meme endroit et sous la
  /// meme convention de nom que le playerName.json du mod CustomName. Les deux mods
  /// ne partagent rien d'autre : chacun garde son fichier.
  ///
  /// La liste est chargee a la demande et non au demarrage du mod : le constructeur
  /// du module tourne avant que le disque du joueur soit forcement pret, et rien
  /// n'a besoin des profils avant l'ouverture du menu.
  /// </summary>
  public static class ProfileStorage
  {
    private static List<ProfileData> profiles;

    public static List<ProfileData> Profiles
    {
      get
      {
        if (profiles == null)
        {
          profiles = Load();
        }

        return profiles;
      }
    }

    public static string FilePath
    {
      get
      {
        var module = TFModFortRiseProfilesModule.Instance;
        return Path.Combine(module.Context.Storage.StoragePath, $"{module.Meta.Name}.profiles.json");
      }
    }

    public static void Save()
    {
      try
      {
        string json = JsonSerializer.Serialize(Profiles, new JsonSerializerOptions
        {
          WriteIndented = true
        });

        string folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
        {
          Directory.CreateDirectory(folder);
        }

        File.WriteAllText(FilePath, json);
      }
      catch (Exception e)
      {
        Log.Error($"[ProfileStorage] sauvegarde impossible : {e.Message}");
      }
    }

    private static List<ProfileData> Load()
    {
      try
      {
        if (!File.Exists(FilePath))
        {
          return new List<ProfileData>();
        }

        var loaded = JsonSerializer.Deserialize<List<ProfileData>>(File.ReadAllText(FilePath));
        if (loaded == null)
        {
          return new List<ProfileData>();
        }

        // Un fichier edite a la main peut arriver sans identifiant : on en attribue
        // un plutot que de laisser deux profils partager la chaine vide, ce qui les
        // ferait pointer sur le meme dossier d'assets par la suite.
        foreach (var profile in loaded)
        {
          if (string.IsNullOrEmpty(profile.Id))
          {
            profile.Id = Guid.NewGuid().ToString("N");
          }
        }

        return loaded;
      }
      catch (Exception e)
      {
        // Rendre une liste vide plutot que de propager : un JSON casse ne doit pas
        // empecher d'ouvrir le menu. Le fichier n'est pas ecrase tant que le joueur
        // n'enregistre rien, il reste donc reparable a la main.
        Log.Error($"[ProfileStorage] lecture impossible : {e.Message}");
        return new List<ProfileData>();
      }
    }

    public static ProfileData Add(string name)
    {
      var profile = new ProfileData
      {
        Name = Normalize(name)
      };

      ArcherCatalog.SetArcher(profile, FirstFreeArcher());
      Profiles.Add(profile);
      Save();
      return profile;
    }

    public static void Remove(ProfileData profile)
    {
      if (profile == null)
      {
        return;
      }

      Profiles.Remove(profile);

      // Les sons du profil partent avec lui : les laisser sur le disque ferait
      // resurgir d'anciens fichiers si un profil du meme nom etait recree plus tard.
      ProfileSfx.DeleteProfileFolder(profile.Name);

      Save();
    }

    /// <summary>
    /// Normalise un nom saisi : majuscules, espaces de bord retires, longueur bornee.
    /// La police du jeu ne dessine que des majuscules.
    /// </summary>
    public static string Normalize(string name)
    {
      name = (name ?? "").Trim().ToUpperInvariant();
      return name.Length > ProfileData.MaxNameLength
          ? name.Substring(0, ProfileData.MaxNameLength)
          : name;
    }

    /// <param name="except">
    /// Profil a ignorer dans la comparaison, pour qu'un renommage qui ne change pas
    /// le nom ne soit pas refuse comme doublon.
    /// </param>
    public static bool NameTaken(string name, ProfileData except = null)
    {
      name = Normalize(name);
      foreach (var profile in Profiles)
      {
        if (!ReferenceEquals(profile, except)
            && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Premier archer que personne n'a encore choisi, pour qu'un nouveau profil ne
    /// tombe pas systematiquement sur le meme. Une fois tous les archers pris, on
    /// repart du premier : rien n'interdit a deux profils de partager un archer.
    /// </summary>
    private static int FirstFreeArcher()
    {
      int count = ArcherCatalog.Count;
      if (count == 0)
      {
        return 0;
      }

      var taken = new HashSet<int>();
      foreach (var profile in Profiles)
      {
        taken.Add(ArcherCatalog.IndexOf(profile));
      }

      for (int i = 0; i < count; i++)
      {
        if (!taken.Contains(i))
        {
          return i;
        }
      }

      return 0;
    }
  }
}
