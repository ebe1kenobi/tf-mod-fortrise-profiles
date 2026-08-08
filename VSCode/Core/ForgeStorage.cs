using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Lecture et ecriture des archers forges.
  ///
  /// Fichier separe de celui des profils, et non une rubrique de plus dedans : un
  /// archer forge ne se rattache a personne. Plusieurs profils peuvent choisir le
  /// meme, et supprimer un profil ne doit pas emporter un archer avec lui.
  ///
  /// Meme convention de nom et meme emplacement que les profils, a cote d'eux dans
  /// l'espace de sauvegarde du mod.
  /// </summary>
  public static class ForgeStorage
  {
    private static List<ForgeDesign> designs;

    public static List<ForgeDesign> Designs
    {
      get
      {
        if (designs == null)
        {
          designs = Load();
        }

        return designs;
      }
    }

    public static string FilePath
    {
      get
      {
        var module = TFModFortRiseProfilesModule.Instance;
        return Path.Combine(module.Context.Storage.StoragePath, $"{module.Meta.Name}.forge.json");
      }
    }

    public static ForgeDesign Find(string id)
    {
      if (string.IsNullOrEmpty(id))
      {
        return null;
      }

      foreach (ForgeDesign design in Designs)
      {
        if (design.Id == id)
        {
          return design;
        }
      }

      return null;
    }

    /// <summary>
    /// Vrai si ce nom court est libre. Il sert d'identifiant de sprite et de nom de
    /// repertoire a l'export : deux archers homonymes ecraseraient leurs planches.
    /// </summary>
    /// <summary>Le dessin dont celui-ci est le costume ALT, ou null.</summary>
    public static ForgeDesign ParentOf(ForgeDesign design)
    {
      return design == null || !design.IsAlt ? null : Find(design.AltOf);
    }

    /// <summary>
    /// Le costume ALT de ce dessin, ou null.
    ///
    /// Un seul est rendu : le jeu n'a qu'une bascule ALT par archer, et deux dessins
    /// se declarant ALT du meme parent ne pourraient pas coexister.
    /// </summary>
    public static ForgeDesign AltOf(ForgeDesign design)
    {
      if (design == null || design.IsAlt)
      {
        return null;
      }

      foreach (ForgeDesign other in Designs)
      {
        if (other != design && other.AltOf == design.Id)
        {
          return other;
        }
      }

      return null;
    }

    /// <summary>
    /// Les dessins qui peuvent servir de parent a un costume ALT : tous sauf
    /// celui-ci, ceux qui sont deja un ALT, et ceux qui en ont deja un.
    /// </summary>
    public static List<ForgeDesign> PossibleParents(ForgeDesign design)
    {
      var list = new List<ForgeDesign>();

      foreach (ForgeDesign other in Designs)
      {
        if (other == design || other.IsAlt)
        {
          continue;
        }

        ForgeDesign taken = AltOf(other);
        if (taken == null || taken == design)
        {
          list.Add(other);
        }
      }

      return list;
    }

    public static bool NameAvailable(string name, ForgeDesign except = null)
    {
      if (string.IsNullOrWhiteSpace(name))
      {
        return false;
      }

      foreach (ForgeDesign design in Designs)
      {
        if (design != except && string.Equals(design.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      return true;
    }

    public static void Save()
    {
      try
      {
        string json = JsonSerializer.Serialize(Designs, new JsonSerializerOptions
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
        Log.Error($"[Forge] enregistrement impossible : {e.Message}");
      }
    }

    private static List<ForgeDesign> Load()
    {
      try
      {
        if (!File.Exists(FilePath))
        {
          return new List<ForgeDesign>();
        }

        string json = File.ReadAllText(FilePath);
        var loaded = JsonSerializer.Deserialize<List<ForgeDesign>>(json)
                     ?? new List<ForgeDesign>();

        // Les dessins ecrits avant les calques n'ont qu'une image par pose : on les
        // transporte dans la nouvelle forme des la lecture, pour que rien plus bas
        // n'ait a connaitre les deux.
        foreach (ForgeDesign design in loaded)
        {
          design.Migrate();
        }

        return loaded;
      }
      catch (Exception e)
      {
        // Un fichier illisible ne doit pas empecher le menu de s'ouvrir : on repart
        // d'une liste vide et on le dit, plutot que de tomber au chargement du mod.
        Log.Error($"[Forge] lecture impossible : {e.Message}");
        return new List<ForgeDesign>();
      }
    }
  }
}
