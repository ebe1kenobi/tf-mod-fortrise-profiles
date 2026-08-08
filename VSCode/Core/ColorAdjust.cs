using System;
using Microsoft.Xna.Framework;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Reglages d'ensemble appliques a un pixel deja recolore : saturation, teinte,
  /// luminosite, contraste.
  ///
  /// Ils viennent apres le remplacement des couleurs et portent sur tout le sprite.
  /// C'est voulu : ces reglages se jugent sur la silhouette entiere, les limiter a une
  /// partie donnerait un personnage dont la tete et le corps n'auraient pas la meme
  /// lumiere.
  ///
  /// Saturation, teinte et luminosite passent par HSV, ou elles ont un sens direct. Le
  /// contraste, lui, s'applique canal par canal autour du gris moyen : le faire sur la
  /// valeur HSV ecraserait la teinte des zones sombres.
  /// </summary>
  public static class ColorAdjust
  {
    public static Color Apply(Color color, ColorTrial trial)
    {
      if (trial == null || !trial.HasAdjustments)
      {
        return color;
      }

      byte alpha = color.A;

      ColorFamilies.ToHsv(color, out float h, out float s, out float v);

      h += trial.Hue;
      s *= trial.Saturation;
      v *= trial.Brightness;

      Color result = ColorFamilies.FromHsv(h, s, v);

      if (Math.Abs(trial.Contrast - 1f) > 0.001f)
      {
        result = new Color(
            Contrast(result.R, trial.Contrast),
            Contrast(result.G, trial.Contrast),
            Contrast(result.B, trial.Contrast));
      }

      // L'alpha n'est jamais touche : ce sont les bords adoucis du pixel art, les
      // modifier ferait apparaitre un lisere ou trouerait la silhouette.
      result.A = alpha;
      return result;
    }

    /// <summary>
    /// Operation inverse : quelle couleur de depart donne, une fois les reglages
    /// appliques, celle qu'on demande.
    ///
    /// Sert quand on choisit une couleur dans un ecran qui affiche le rendu final :
    /// c'est la couleur de depart qu'il faut ranger dans la table, sinon les reglages
    /// s'appliqueraient une seconde fois par-dessus.
    ///
    /// L'inversion n'est pas exacte partout. Le contraste et les bornes de teinte
    /// ecretent, et une saturation ou une luminosite proches de zero effacent
    /// l'information : dans ces cas on rend au mieux, sans chercher a retrouver ce qui
    /// n'existe plus.
    /// </summary>
    public static Color Invert(Color color, ColorTrial trial)
    {
      if (trial == null || !trial.HasAdjustments)
      {
        return color;
      }

      byte alpha = color.A;
      Color working = color;

      if (Math.Abs(trial.Contrast - 1f) > 0.001f && trial.Contrast > 0.001f)
      {
        working = new Color(
            Contrast(working.R, 1f / trial.Contrast),
            Contrast(working.G, 1f / trial.Contrast),
            Contrast(working.B, 1f / trial.Contrast));
      }

      ColorFamilies.ToHsv(working, out float h, out float s, out float v);

      h -= trial.Hue;

      if (trial.Saturation > 0.01f)
      {
        s /= trial.Saturation;
      }

      if (trial.Brightness > 0.01f)
      {
        v /= trial.Brightness;
      }

      Color result = ColorFamilies.FromHsv(h, s, v);
      result.A = alpha;
      return result;
    }

    private static int Contrast(byte channel, float amount)
    {
      float value = (channel / 255f - 0.5f) * amount + 0.5f;
      return (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
  }
}
