using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Gestion des essais de couleurs d'un profil : lesquels existent pour l'archer
  /// courant, lequel est actif, creation, suppression, export et import.
  ///
  /// Un essai n'a de sens que pour le couple archer / costume sur lequel il a ete
  /// fait : ses cles sont les teintes d'origine de ce sprite. Les listes rendues ici
  /// sont donc toujours filtrees sur ce couple, et changer d'archer ne perd rien - les
  /// essais de l'ancien restent en place, simplement hors du filtre.
  /// </summary>
  public static class ProfileTrials
  {
    private const string PoolDirName = "trials";
    private const string Extension = ".json";
    private const string DefaultName = "DEFAULT";

    public static string ArcherOf(ProfileData profile)
    {
      return ArcherCatalog.NameOf(ArcherCatalog.IndexOf(profile));
    }

    public static string CostumeOf(ProfileData profile)
    {
      return profile != null && profile.IsAlt ? ProfileCostumes.Alt : ProfileCostumes.Normal;
    }

    private static bool Matches(ColorTrial trial, string archer, string costume)
    {
      return string.Equals(trial.Archer, archer, StringComparison.OrdinalIgnoreCase)
          && string.Equals(trial.Costume, costume, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Les essais faits pour l'archer et le costume actuels du profil.</summary>
    public static List<ColorTrial> For(ProfileData profile)
    {
      var result = new List<ColorTrial>();
      if (profile?.Trials == null)
      {
        return result;
      }

      string archer = ArcherOf(profile);
      string costume = CostumeOf(profile);

      foreach (ColorTrial trial in profile.Trials)
      {
        if (Matches(trial, archer, costume))
        {
          result.Add(trial);
        }
      }

      return result;
    }

    /// <summary>L'essai qui s'applique, ou null si aucun n'est retenu.</summary>
    public static ColorTrial Active(ProfileData profile)
    {
      if (profile?.Trials == null || profile.ActiveTrials == null)
      {
        return null;
      }

      string archer = ArcherOf(profile);
      string costume = CostumeOf(profile);

      foreach (ActiveTrial active in profile.ActiveTrials)
      {
        if (!string.Equals(active.Archer, archer, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(active.Costume, costume, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        foreach (ColorTrial trial in profile.Trials)
        {
          if (Matches(trial, archer, costume)
              && string.Equals(trial.Name, active.Name, StringComparison.OrdinalIgnoreCase))
          {
            return trial;
          }
        }
      }

      return null;
    }

    public static bool IsActive(ProfileData profile, ColorTrial trial)
    {
      return ReferenceEquals(Active(profile), trial);
    }

    public static void SetActive(ProfileData profile, ColorTrial trial)
    {
      if (profile == null)
      {
        return;
      }

      string archer = ArcherOf(profile);
      string costume = CostumeOf(profile);

      profile.ActiveTrials ??= new List<ActiveTrial>();
      profile.ActiveTrials.RemoveAll(a =>
          string.Equals(a.Archer, archer, StringComparison.OrdinalIgnoreCase)
          && string.Equals(a.Costume, costume, StringComparison.OrdinalIgnoreCase));

      if (trial != null)
      {
        profile.ActiveTrials.Add(new ActiveTrial
        {
          Archer = archer,
          Costume = costume,
          Name = trial.Name
        });
      }

      // Aucune invalidation ici, volontairement.
      //
      // Les textures fabriquees sont indexees par essai : changer d'essai actif fait
      // simplement porter les prochaines lectures sur une autre entree, celles de
      // l'ancien restant valides. Les liberer serait non seulement inutile mais
      // dangereux : l'ecran de choix des essais active brievement chaque essai survole
      // pour en fabriquer l'apercu, puis remet le precedent. Invalider a ce
      // moment-la detruisait les textures que l'apercu venait tout juste de prendre,
      // et le cadre virait au noir.
    }

    public static bool NameTaken(ProfileData profile, string name)
    {
      foreach (ColorTrial trial in For(profile))
      {
        if (string.Equals(trial.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }

    public static ColorTrial Create(ProfileData profile, string name)
    {
      if (profile == null)
      {
        return null;
      }

      var trial = new ColorTrial
      {
        Name = ProfileStorage.Normalize(name),
        Archer = ArcherOf(profile),
        Costume = CostumeOf(profile)
      };

      profile.Trials ??= new List<ColorTrial>();
      profile.Trials.Add(trial);
      return trial;
    }

    public static void Delete(ProfileData profile, ColorTrial trial)
    {
      if (profile?.Trials == null || trial == null)
      {
        return;
      }

      bool wasActive = IsActive(profile, trial);
      profile.Trials.Remove(trial);

      // Les images fabriquees pour cet essai n'ont plus d'objet : les laisser
      // reapparaitrait si un essai du meme nom etait recree plus tard.
      try
      {
        string dir = SpriteRecolor.TrialDir(profile, trial);
        if (Directory.Exists(dir))
        {
          Directory.Delete(dir, true);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Trials] suppression des images de {trial.Name} impossible : {e.Message}");
      }

      if (wasActive)
      {
        SetActive(profile, null);
      }

      SpriteRecolor.Invalidate(profile);
    }

    /// <summary>
    /// Convertit la palette unique des profils d'avant les essais en un essai nomme
    /// DEFAULT, attache a l'archer et au costume courants, et le rend actif.
    ///
    /// Sans cela, ouvrir l'ecran des couleurs sur un ancien profil montrerait une
    /// liste vide alors que le sprite est bel et bien recolore.
    /// </summary>
    public static void Migrate(ProfileData profile)
    {
      if (profile?.Palette == null || profile.Palette.Count == 0)
      {
        return;
      }

      ColorTrial trial = Create(profile, DefaultName);
      trial.Palette = profile.Palette;
      profile.Palette = null;

      SetActive(profile, trial);
      Log.Info($"[Trials] palette de {profile.Name} convertie en essai {DefaultName}");
    }

    // ------------------------------------------------------------------
    // Export et import
    // ------------------------------------------------------------------

    public static string PoolDir =>
        Path.Combine(TFModFortRiseArcherModule.Instance.Context.Storage.StoragePath, PoolDirName);

    /// <summary>
    /// Ecrit l'essai dans le vivier partage, en JSON.
    ///
    /// Ce format plutot qu'un binaire : un essai est une petite structure de texte,
    /// qu'on peut relire, corriger a la main, comparer, et coller dans un message pour
    /// l'envoyer a quelqu'un.
    /// </summary>
    public static string Export(ProfileData profile, ColorTrial trial)
    {
      try
      {
        Directory.CreateDirectory(PoolDir);

        string path = Path.Combine(PoolDir, FileNameOf(profile, trial));
        string json = JsonSerializer.Serialize(trial, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
      }
      catch (Exception e)
      {
        Log.Error($"[Trials] export de {trial?.Name} impossible : {e.Message}");
        return null;
      }
    }

    private static string FileNameOf(ProfileData profile, ColorTrial trial)
    {
      string archer = SoundEvents.Sanitize(trial.Archer);
      string costume = SoundEvents.Sanitize(trial.Costume);
      string name = SoundEvents.Sanitize(trial.Name);
      return $"{archer}_{costume}_{name}{Extension}";
    }

    /// <summary>Les essais disponibles a l'import, tous archers confondus.</summary>
    public static List<string> Exported()
    {
      var files = new List<string>();

      try
      {
        if (!Directory.Exists(PoolDir))
        {
          Directory.CreateDirectory(PoolDir);
          return files;
        }

        files.AddRange(Directory.GetFiles(PoolDir, "*" + Extension));
        files.Sort(StringComparer.OrdinalIgnoreCase);
      }
      catch (Exception e)
      {
        Log.Error($"[Trials] vivier d'essais illisible : {e.Message}");
      }

      return files;
    }

    public static ColorTrial Read(string path)
    {
      try
      {
        return JsonSerializer.Deserialize<ColorTrial>(File.ReadAllText(path));
      }
      catch (Exception e)
      {
        Log.Error($"[Trials] {Path.GetFileName(path)} illisible : {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Ajoute au profil une copie de l'essai lu sur le disque.
    ///
    /// L'archer et le costume de l'essai sont conserves tels quels : un essai fait
    /// pour un autre personnage reste importable, il n'apparaitra simplement dans la
    /// liste que si l'on selectionne cet archer-la. Le nom est suffixe en cas de
    /// collision plutot que d'ecraser un essai existant.
    /// </summary>
    public static ColorTrial Import(ProfileData profile, ColorTrial source)
    {
      if (profile == null || source == null)
      {
        return null;
      }

      var copy = new ColorTrial
      {
        Name = UniqueName(profile, source),
        Archer = source.Archer,
        Costume = string.IsNullOrEmpty(source.Costume) ? ProfileCostumes.Normal : source.Costume,
        Palette = source.Palette,
        Saturation = source.Saturation,
        Hue = source.Hue,
        Brightness = source.Brightness,
        Contrast = source.Contrast
      };

      profile.Trials ??= new List<ColorTrial>();
      profile.Trials.Add(copy);
      return copy;
    }

    private static string UniqueName(ProfileData profile, ColorTrial source)
    {
      string baseName = ProfileStorage.Normalize(source.Name);
      if (string.IsNullOrEmpty(baseName))
      {
        baseName = "IMPORT";
      }

      if (profile.Trials == null)
      {
        return baseName;
      }

      string candidate = baseName;
      int suffix = 2;

      while (Exists(profile, source.Archer, source.Costume, candidate))
      {
        candidate = ProfileStorage.Normalize(baseName + suffix);
        suffix++;
      }

      return candidate;
    }

    private static bool Exists(ProfileData profile, string archer, string costume, string name)
    {
      foreach (ColorTrial trial in profile.Trials)
      {
        if (string.Equals(trial.Archer, archer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(trial.Costume, costume, StringComparison.OrdinalIgnoreCase)
            && string.Equals(trial.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }

      return false;
    }
  }
}
