using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Ce que les ecrans de couleur ne peuvent pas partager : l'apercu, l'enregistrement
  /// et le releve de palette.
  ///
  /// Le reste des ecrans est identique pour un profil et pour un archer forge - on y
  /// coche des parties, on lit une palette, on remplace une teinte. Ces trois points
  /// la, non : un profil anime un archer du jeu et exporte ses PNG a la sortie, un
  /// archer forge montre ses planches assemblees et vit dans le fichier de la forge.
  ///
  /// Les regrouper ici plutot que de semer des tests dans les trois ecrans : c'est le
  /// seul endroit qui connait les deux sujets, et l'ajout d'un troisieme ne toucherait
  /// que ce fichier.
  /// </summary>
  internal sealed class ColorPreview
  {
    private readonly UISpritePreview sprite;
    private readonly UIForgePreview forge;
    private readonly IColorSubject subject;

    private ColorPreview(IColorSubject subject, UISpritePreview sprite, UIForgePreview forge)
    {
      this.subject = subject;
      this.sprite = sprite;
      this.forge = forge;
    }

    /// <summary>Le panneau a poser dans le menu.</summary>
    public MenuItem Item => (MenuItem)sprite ?? forge;

    public static ColorPreview For(IColorSubject subject, Vector2 position)
    {
      if (subject is ForgeColorSubject)
      {
        return new ColorPreview(subject, null, new UIForgePreview(position));
      }

      return new ColorPreview(subject, new UISpritePreview(position), null);
    }

    public void Rebuild()
    {
      if (subject is ProfileColorSubject profile)
      {
        sprite?.Rebuild(profile.Profile);
        return;
      }

      if (subject is ForgeColorSubject design)
      {
        forge?.Show(design.Design);
      }
    }

    /// <summary>
    /// Enregistre ce que l'ecran vient de modifier, a sa fermeture.
    ///
    /// Le profil exporte en plus ses PNG recolores : ils font foi en jeu, et les
    /// laisser en arriere afficherait les couleurs du passage precedent.
    /// </summary>
    public static void Persist(IColorSubject subject)
    {
      if (subject is ProfileColorSubject profile)
      {
        SpriteRecolor.Export(profile.Profile);
        ProfileStorage.Save();
        return;
      }

      if (subject is ForgeColorSubject)
      {
        ForgeStorage.Save();
      }
    }

    /// <summary>
    /// Les teintes dominantes des planches choisies.
    ///
    /// Le profil garde son chemin, qui met la palette en cache par archer : la relever
    /// coute la lecture de tout l'atlas du personnage. La forge passe par le releve
    /// generique, ses planches etant deja en memoire.
    /// </summary>
    public static List<PaletteColor> Palette(IColorSubject subject, IReadOnlyList<string> parts)
    {
      if (subject is ProfileColorSubject profile)
      {
        return SpriteRecolor.Palette(profile.Profile, parts);
      }

      var sources = new List<Color[]>();

      foreach (string part in parts)
      {
        sources.Add(subject.SourcePixels(part));
      }

      return ColorPalette.Of(sources);
    }
  }
}
