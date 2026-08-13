using System.Collections.Generic;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Regroupe les planches du sprite en parties telles qu'on les designe quand on
  /// parle du personnage : le corps, la tete, l'arc, le chapeau, le cadavre.
  ///
  /// Le jeu, lui, decoupe plus finement : la tete a une planche par coiffe, et la
  /// fleche encochee est une image separee de l'arc. Les proposer separement ferait
  /// neuf cases a cocher pour une distinction dont on n'a pas l'usage - on veut
  /// recolorer "la tete", pas "la tete avec couronne".
  /// </summary>
  public static class SpritePartGroups
  {
    public const string Body = "BODY";
    public const string Head = "HEAD";
    public const string Bow = "BOW";
    public const string Hat = "HAT";
    public const string Corpse = "CORPSE";

    public static readonly string[] All = { Body, Head, Bow, Hat, Corpse };

    /// <summary>Les planches que recouvre une partie.</summary>
    public static IEnumerable<string> PartsOf(string group)
    {
      switch (group)
      {
        case Head:
          yield return SpriteRecolor.Head;
          yield return SpriteRecolor.HeadNoHat;
          yield return SpriteRecolor.HeadCrown;
          yield return SpriteRecolor.HeadBack;
          break;
        case Body:
          yield return SpriteRecolor.Body;
          break;
        case Bow:
          yield return SpriteRecolor.Bow;

          // La fleche encochee suit l'arc plutot que d'avoir sa propre case : elle
          // n'apparait qu'avec lui, et recolorer l'arc sans elle laisserait une
          // fleche aux couleurs d'origine posee dessus.
          yield return SpriteRecolor.Aim;
          break;
        case Hat:
          yield return SpriteRecolor.Hat;
          break;
        case Corpse:
          yield return SpriteRecolor.Corpse;
          break;
      }
    }
  }

  /// <summary>
  /// Les parties sur lesquelles portent les deux ecrans de couleur.
  ///
  /// C'est un outil d'edition et non une donnee du profil : la selection vit le temps
  /// de la session et n'est pas enregistree. Elle est partagee par les deux ecrans,
  /// pour qu'on puisse passer du reglage par famille au reglage couleur par couleur
  /// sans refaire son choix.
  /// </summary>
  public static class ColorSelection
  {
    private static readonly HashSet<string> selected =
        new HashSet<string>(SpritePartGroups.All);

    public static bool IsSelected(string group)
    {
      return selected.Contains(group);
    }

    public static void Toggle(string group)
    {
      if (!selected.Remove(group))
      {
        selected.Add(group);
      }
    }

    /// <summary>
    /// Les planches concernees par la selection courante, chez ce sujet.
    ///
    /// C'est le sujet qui dit quelles familles existent et ce qu'elles recouvrent :
    /// une famille cochee mais absente de son vocabulaire est ignoree. Sans cela, une
    /// selection heritee de l'ecran des profils ferait chercher a la forge des
    /// planches qu'elle n'a pas - la selection est volontairement partagee entre les
    /// ecrans, elle traverse donc aussi les sujets.
    /// </summary>
    public static List<string> Parts(IColorSubject subject)
    {
      var parts = new List<string>();

      if (subject == null)
      {
        return parts;
      }

      foreach (string group in subject.Groups)
      {
        if (!selected.Contains(group))
        {
          continue;
        }

        foreach (string part in subject.PartsOf(group))
        {
          parts.Add(part);
        }
      }

      return parts;
    }

    public static bool Any => selected.Count > 0;
  }
}
