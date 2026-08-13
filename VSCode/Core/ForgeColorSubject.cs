using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// L'archer forge vu par les ecrans de couleur.
  ///
  /// Les teintes sont relevees sur les planches assemblees - corps, cadavre, chapeau -
  /// et non sur les cases du vivier prises une a une : une pose faite de trois calques
  /// n'a de couleurs completes qu'une fois empilee, et l'on veut la palette du
  /// personnage, pas celle de la premiere image.
  ///
  /// Trois familles seulement. Le corps porte deja la tete, il n'y a donc pas de HEAD
  /// a proposer ; l'arc, le viseur et les gemmes sont empruntes au jeu et gardent leur
  /// propre reglage, BORROWED HUE, qui fait tourner leur teinte d'un bloc. Les faire
  /// entrer ici remplirait la palette des couleurs de l'archer vert - qui n'ont rien a
  /// voir avec le personnage - et deux reglages agiraient sur les memes pixels.
  /// </summary>
  public sealed class ForgeColorSubject : IColorSubject
  {
    /// <summary>Les familles proposees, dans l'ordre des ecrans.</summary>
    public static readonly string[] Families =
    {
      SpritePartGroups.Body,
      SpritePartGroups.Head,
      SpritePartGroups.Corpse,
      SpritePartGroups.Hat
    };

    private readonly ForgeDesign design;

    /// <summary>
    /// Pixels d'origine par famille, releves une fois.
    ///
    /// Les relire a chaque image couterait dix-neuf decoupes par passage dans la
    /// palette, et surtout ils ne changent pas : ce sont les planches AVANT
    /// recoloration, et retoucher une couleur ne les touche pas.
    /// </summary>
    private readonly Dictionary<string, Color[]> pixels = new Dictionary<string, Color[]>();

    public ForgeColorSubject(ForgeDesign design)
    {
      this.design = design;
    }

    public ForgeDesign Design => design;

    public ColorTrial Trial => design.Colors;

    public IReadOnlyList<string> Groups => Families;

    /// <summary>
    /// Une famille est ici sa propre planche : la forge n'a pas le decoupage fin du
    /// jeu, ou la tete a une image par coiffe.
    /// </summary>
    public IEnumerable<string> PartsOf(string group)
    {
      yield return group;
    }

    public Color[] SourcePixels(string part)
    {
      if (pixels.TryGetValue(part, out Color[] cached))
      {
        return cached;
      }

      Color[] read = Read(part);
      pixels[part] = read;
      return read;
    }

    public void Invalidate()
    {
      design.Touch();
    }

    /// <summary>Efface le releve : a appeler si les poses ont change.</summary>
    public void Forget()
    {
      pixels.Clear();
    }

    /// <summary>
    /// Assemble une planche entiere et rend ses pixels, ou null si elle est vide.
    ///
    /// La meme decoupe que la fabrication, par <see cref="ForgeCompose"/> : une
    /// palette relevee autrement contiendrait des teintes que l'archer n'aura pas.
    /// </summary>
    private Color[] Read(string group)
    {
      ForgeSheet sheet = SheetOf(group);
      var all = new List<Color>();

      foreach (ForgeSlot slot in ForgeSlots.Of(sheet))
      {
        Color[] pose = ForgeCompose.Cut(ForgeCompose.Pose(design, slot.Key), ForgeCompose.FrameOf(design));

        if (pose != null)
        {
          all.AddRange(pose);
        }
      }

      return all.Count == 0 ? null : all.ToArray();
    }

    private static ForgeSheet SheetOf(string group)
    {
      if (group == SpritePartGroups.Corpse)
      {
        return ForgeSheet.Corpse;
      }

      if (group == SpritePartGroups.Head)
      {
        return ForgeSheet.Head;
      }

      return group == SpritePartGroups.Hat ? ForgeSheet.Hat : ForgeSheet.Body;
    }

    /// <summary>
    /// Nom de famille d'une planche, tel que la table de couleurs le range.
    ///
    /// La fabrication en a besoin pour retrouver les remplacements d'une planche, et
    /// il doit etre le meme que celui employe par les ecrans - d'ou ce point unique
    /// plutot que deux chaines ecrites a deux endroits.
    /// </summary>
    public static string GroupOf(ForgeSheet sheet)
    {
      switch (sheet)
      {
        case ForgeSheet.Corpse:
          return SpritePartGroups.Corpse;
        case ForgeSheet.Hat:
          return SpritePartGroups.Hat;
        case ForgeSheet.Head:
        case ForgeSheet.HeadNormal:
        case ForgeSheet.HeadCrown:
          return SpritePartGroups.Head;

        // La couronne se recolore avec le chapeau : ce sont les deux couvre-chefs, et
        // les separer donnerait un archer dont la couronne jure avec le sien.
        case ForgeSheet.Crown:
          return SpritePartGroups.Hat;
        default:
          return SpritePartGroups.Body;
      }
    }
  }
}
