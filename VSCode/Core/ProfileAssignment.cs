using System.Collections.Generic;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Quel profil joue sur quel emplacement joueur.
  ///
  /// Rempli sur l'ecran de selection des archers et lu ensuite par tout ce qui a
  /// besoin de savoir "qui est P1" : les sons, et plus tard les portraits et les
  /// palettes. C'est le seul point ou la question se pose, d'ou une table statique
  /// plutot qu'un passage de parametre a travers des classes du jeu qu'on ne
  /// controle pas.
  ///
  /// La table est volontairement conservee d'une partie a l'autre : revenir au
  /// rollcall doit retrouver les profils du tour precedent.
  /// </summary>
  public static class ProfileAssignment
  {
    private static readonly Dictionary<int, ProfileData> assigned = new Dictionary<int, ProfileData>();

    public static ProfileData Get(int playerIndex)
    {
      assigned.TryGetValue(playerIndex, out ProfileData profile);

      // Un profil supprime depuis le menu ne doit pas continuer a valoir pour un
      // joueur : la table garde une reference que la liste ne connait plus.
      if (profile != null && !ProfileStorage.Profiles.Contains(profile))
      {
        assigned.Remove(playerIndex);
        return null;
      }

      return profile;
    }

    public static void Set(int playerIndex, ProfileData profile)
    {
      if (profile == null)
      {
        assigned.Remove(playerIndex);
        return;
      }

      assigned[playerIndex] = profile;
    }

    public static string NameOf(int playerIndex)
    {
      return Get(playerIndex)?.Name ?? "";
    }

    /// <summary>
    /// Indique si un profil est deja pris par un autre joueur, pour ne pas le
    /// proposer deux fois sur le meme ecran.
    /// </summary>
    public static bool TakenByOther(ProfileData profile, int playerIndex)
    {
      foreach (var pair in assigned)
      {
        if (pair.Key != playerIndex && ReferenceEquals(pair.Value, profile))
        {
          return true;
        }
      }

      return false;
    }
  }
}
