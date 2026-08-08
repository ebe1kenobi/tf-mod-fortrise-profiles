using System;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Traduit dans les deux sens entre un <see cref="ProfileData"/> et les donnees
  /// d'archer du jeu.
  ///
  /// Toutes les methodes tolerent un catalogue absent : ArcherData.Archers n'est
  /// rempli que pendant TFGame.LoadContent, et un profil peut etre lu ou ecrit
  /// avant, notamment au premier chargement du fichier de sauvegarde.
  /// </summary>
  public static class ArcherCatalog
  {
    public static bool Ready => ArcherData.Archers != null && ArcherData.Archers.Length > 0;

    public static int Count => Ready ? ArcherData.Archers.Length : 0;

    /// <summary>
    /// Nom complet d'un archer, sur une seule ligne. Le jeu stocke son titre en deux
    /// moities (Name0 / Name1) parce qu'il l'affiche empile sous le portrait.
    /// </summary>
    public static string NameOf(int index)
    {
      if (!Ready || index < 0 || index >= ArcherData.Archers.Length)
      {
        return "";
      }

      ArcherData archer = ArcherData.Archers[index];
      if (archer == null)
      {
        return "";
      }

      string name0 = archer.Name0 ?? "";
      string name1 = archer.Name1 ?? "";

      if (name0.Length == 0)
      {
        return name1;
      }

      return name1.Length == 0 ? name0 : name0 + " " + name1;
    }

    /// <summary>
    /// Index de l'archer d'un profil : par nom d'abord, par index ensuite, et a
    /// defaut le premier archer. Ne rend jamais un index hors bornes tant que le
    /// catalogue est charge.
    /// </summary>
    public static int IndexOf(ProfileData profile)
    {
      if (!Ready || profile == null)
      {
        return 0;
      }

      if (!string.IsNullOrEmpty(profile.Archer))
      {
        for (int i = 0; i < ArcherData.Archers.Length; i++)
        {
          if (string.Equals(NameOf(i), profile.Archer, StringComparison.OrdinalIgnoreCase))
          {
            return i;
          }
        }
      }

      if (profile.ArcherIndex >= 0 && profile.ArcherIndex < ArcherData.Archers.Length)
      {
        return profile.ArcherIndex;
      }

      return 0;
    }

    public static ArcherData.ArcherTypes TypeOf(ProfileData profile)
    {
      return profile != null && profile.IsAlt
          ? ArcherData.ArcherTypes.Alt
          : ArcherData.ArcherTypes.Normal;
    }

    /// <summary>
    /// Donnees d'archer a afficher pour un profil. ArcherData.Get retombe deja sur
    /// la version normale quand un archer n'a pas de tenue alternative, il n'y a
    /// donc rien a verifier de ce cote.
    /// </summary>
    public static ArcherData DataOf(ProfileData profile)
    {
      if (!Ready)
      {
        return null;
      }

      return ArcherData.Get(IndexOf(profile), TypeOf(profile));
    }

    /// <summary>
    /// Ecrit dans le profil l'archer designe par un index. Le nom et l'index sont
    /// enregistres ensemble pour que la relecture puisse se rabattre de l'un sur
    /// l'autre.
    /// </summary>
    public static void SetArcher(ProfileData profile, int index)
    {
      if (profile == null)
      {
        return;
      }

      if (!Ready)
      {
        profile.ArcherIndex = Math.Max(0, index);
        return;
      }

      index = ((index % ArcherData.Archers.Length) + ArcherData.Archers.Length) % ArcherData.Archers.Length;
      profile.ArcherIndex = index;
      profile.Archer = NameOf(index);
    }

    /// <summary>
    /// Indique si l'archer possede une tenue alternative distincte. Sert a griser le
    /// choix du costume plutot qu'a proposer une bascule sans effet visible.
    /// </summary>
    public static bool HasAlt(int index)
    {
      return Ready
          && ArcherData.AltArchers != null
          && index >= 0
          && index < ArcherData.AltArchers.Length
          && ArcherData.AltArchers[index] != null;
    }
  }
}
