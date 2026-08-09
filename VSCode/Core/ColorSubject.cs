using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Ce sur quoi les ecrans de couleur travaillent : un essai a modifier, des parties
  /// a examiner, et de quoi se remettre a jour.
  ///
  /// Les profils recolorent un archer du jeu, dont les pixels sont dans l'atlas ; la
  /// forge recolore des planches assemblees depuis le vivier. Rien d'autre ne les
  /// distingue du point de vue des ecrans - on y choisit une teinte dominante et on la
  /// remplace - d'ou cette interface plutot que deux jeux d'ecrans jumeaux qui
  /// auraient diverge a la premiere correction faite d'un seul cote.
  /// </summary>
  public interface IColorSubject
  {
    /// <summary>L'essai que les ecrans modifient. Jamais null.</summary>
    ColorTrial Trial { get; }

    /// <summary>
    /// Les familles de parties proposees en cases a cocher.
    ///
    /// Toutes n'existent pas partout : un archer forge n'a pas de tete separee, son
    /// corps la porte deja, et proposer HEAD y donnerait une case sans effet.
    /// </summary>
    IReadOnlyList<string> Groups { get; }

    /// <summary>Les planches que recouvre une famille.</summary>
    IEnumerable<string> PartsOf(string group);

    /// <summary>
    /// Pixels d'origine d'une planche, avant tout remplacement, ou null.
    ///
    /// D'origine et non tels qu'ils sont a l'ecran : la table de couleurs est clee sur
    /// les teintes de depart, une palette prise sur le resultat se deplacerait a chaque
    /// retouche et l'on ne saurait plus quoi remplacer.
    /// </summary>
    Color[] SourcePixels(string part);

    /// <summary>A appeler apres toute modification de l'essai.</summary>
    void Invalidate();
  }

  /// <summary>
  /// Ce que les ecrans de couleur editent en ce moment.
  ///
  /// Un statique plutot qu'un parametre : les MenuState sont construits par le jeu, on
  /// ne leur transmet rien. Meme procede que UIForgeFramePicker.ReturnToLayers, et
  /// meme raison.
  /// </summary>
  public static class ColorEditing
  {
    public static IColorSubject Subject;

    /// <summary>Ecran ou le bouton retour ramene, pose par la porte d'entree.</summary>
    public static TowerFall.MainMenu.MenuState BackState;
  }

  /// <summary>
  /// Le travail sur les pixels qui ne depend d'aucune source : relever les teintes
  /// dominantes, et appliquer une table de remplacement.
  /// </summary>
  public static class ColorPalette
  {
    /// <summary>
    /// Les teintes presentes, de la plus repandue a la plus rare.
    ///
    /// Les pixels entierement transparents sont ecartes : ils ne sont pas une couleur
    /// du personnage, et les compter mettrait une entree vide en tete de palette.
    /// </summary>
    public static List<PaletteColor> Of(IEnumerable<Color[]> sources)
    {
      var counts = new Dictionary<uint, int>();

      foreach (Color[] pixels in sources)
      {
        if (pixels == null)
        {
          continue;
        }

        foreach (Color pixel in pixels)
        {
          if (pixel.A == 0)
          {
            continue;
          }

          uint packed = pixel.PackedValue;
          counts.TryGetValue(packed, out int count);
          counts[packed] = count + 1;
        }
      }

      var palette = new List<PaletteColor>(counts.Count);

      foreach (var pair in counts)
      {
        palette.Add(new PaletteColor
        {
          Source = new Color { PackedValue = pair.Key },
          Count = pair.Value
        });
      }

      palette.Sort((a, b) => b.Count.CompareTo(a.Count));
      return palette;
    }

    /// <summary>
    /// Table teinte d'origine vers teinte finale pour une planche, reglages compris.
    ///
    /// Rendue clee sur la valeur empaquetee : c'est ce qui permet de recolorer une
    /// image en un seul parcours, sans comparer chaque pixel a toute la table.
    /// </summary>
    public static Dictionary<uint, Color> SwapMap(ColorTrial trial, string part)
    {
      var map = new Dictionary<uint, Color>();

      if (trial == null)
      {
        return map;
      }

      if (trial.Palette != null)
      {
        foreach (ColorSwap swap in trial.Palette)
        {
          if (!string.Equals(swap.Part, part, StringComparison.OrdinalIgnoreCase)
              || !SpriteRecolor.TryParse(swap.From, out Color from)
              || !SpriteRecolor.TryParse(swap.To, out Color to))
          {
            continue;
          }

          map[from.PackedValue] = to;
        }
      }

      return map;
    }

    /// <summary>
    /// Recolore des pixels en place : remplacement d'abord, reglages d'ensemble
    /// ensuite. L'ordre compte - les reglages doivent porter sur la couleur choisie et
    /// non sur celle qu'on a remplacee.
    /// </summary>
    public static void Apply(Color[] pixels, Dictionary<uint, Color> map, ColorTrial trial)
    {
      if (pixels == null)
      {
        return;
      }

      bool adjusts = trial != null && trial.HasAdjustments;

      if (map.Count == 0 && !adjusts)
      {
        return;
      }

      for (int i = 0; i < pixels.Length; i++)
      {
        // Un pixel transparent le reste : lui appliquer une teinte ferait apparaitre
        // un halo la ou le dessin n'a rien.
        if (pixels[i].A == 0)
        {
          continue;
        }

        Color color = map.TryGetValue(pixels[i].PackedValue, out Color replacement)
            ? replacement
            : pixels[i];

        pixels[i] = adjusts ? ColorAdjust.Apply(color, trial) : color;
      }
    }
  }
}
