using System.Collections.Generic;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Le nom affiche pour un emplacement joueur, ou qu'on l'affiche.
  ///
  /// Reprend le role que tenait le mod CustomName. Trois sources, dans l'ordre :
  /// un nom impose par un autre mod via l'API, le profil rattache a ce joueur, et a
  /// defaut le "P1".."P8" du jeu.
  ///
  /// Ce dernier repli compte autant que les autres : sur l'ecran de selection, le
  /// cran "aucun profil" du cycle doit se voir. Sans nom affiche, le joueur ne
  /// distingue pas "je suis revenu au debut" de "l'ecran ne repond plus".
  /// </summary>
  public static class PlayerNames
  {
    private static readonly Dictionary<int, string> overrides = new Dictionary<int, string>();

    public static string Of(int playerIndex)
    {
      if (overrides.TryGetValue(playerIndex, out string forced) && !string.IsNullOrEmpty(forced))
      {
        return forced;
      }

      ProfileData profile = ProfileAssignment.Get(playerIndex);
      if (profile != null && !string.IsNullOrEmpty(profile.Name))
      {
        return profile.Name;
      }

      return Default(playerIndex);
    }

    public static string Default(int playerIndex)
    {
      return "P" + (playerIndex + 1);
    }

    /// <summary>
    /// Nom impose de l'exterieur (API). Prime sur le profil : c'est un appel
    /// deliberat d'un autre mod, il ne doit pas etre silencieusement ignore.
    /// </summary>
    public static void SetOverride(int playerIndex, string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        overrides.Remove(playerIndex);
        return;
      }

      overrides[playerIndex] = name;
    }

    /// <summary>
    /// Appele quand le joueur change de profil sur l'ecran de selection : ce geste
    /// est le plus recent, il reprend la main sur un nom impose plus tot.
    /// </summary>
    public static void ClearOverride(int playerIndex)
    {
      overrides.Remove(playerIndex);
    }
  }
}
