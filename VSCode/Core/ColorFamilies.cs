using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseArcher
{
  /// <summary>Un groupe de teintes voisines et la nuance qui le represente.</summary>
  public sealed class ColorFamily
  {
    public string Name;
    /// <summary>Nuance la plus etendue du groupe : c'est elle qui sert de reference au decalage.</summary>
    public Color Reference;
    public List<Color> Members = new List<Color>();
    public int Count;
  }

  /// <summary>
  /// Regroupe les couleurs d'un sprite par famille de teinte, et sait deplacer une
  /// famille entiere vers une couleur choisie.
  ///
  /// Un sprite d'archer compte plusieurs dizaines de nuances : ombres, reflets,
  /// bords adoucis. Les traiter une par une est impraticable, mais les remplacer
  /// toutes par la meme couleur ecraserait le relief et rendrait le personnage plat.
  ///
  /// D'ou le decalage : la nuance la plus etendue de la famille devient exactement la
  /// couleur choisie, et toutes les autres subissent le meme ecart de teinte et le
  /// meme rapport de saturation et de luminosite. Les ombres restent des ombres, les
  /// reflets restent des reflets.
  /// </summary>
  public static class ColorFamilies
  {
    public const string Neutral = "GREY";
    public const string Dark = "BLACK";

    /// <summary>
    /// Familles presentes dans la palette, la plus etendue d'abord.
    /// </summary>
    public static List<ColorFamily> Group(List<PaletteColor> palette)
    {
      var families = new Dictionary<string, ColorFamily>();

      foreach (PaletteColor entry in palette)
      {
        string name = NameOf(entry.Source);

        if (!families.TryGetValue(name, out ColorFamily family))
        {
          family = new ColorFamily { Name = name, Reference = entry.Source };
          families[name] = family;
        }

        family.Members.Add(entry.Source);
        family.Count += entry.Count;
      }

      var result = new List<ColorFamily>(families.Values);

      // La palette arrive deja triee par surface : le premier membre rencontre est
      // donc la nuance dominante de sa famille, et fait une reference naturelle.
      result.Sort((a, b) => b.Count.CompareTo(a.Count));
      return result;
    }

    /// <summary>
    /// Famille d'une couleur. Les teintes trop sombres ou trop peu saturees n'ont pas
    /// de teinte exploitable : les ranger par nuance les eparpillerait au hasard,
    /// alors qu'elles forment en pratique les ombres et les contours du sprite.
    /// </summary>
    public static string NameOf(Color color)
    {
      ToHsv(color, out float h, out float s, out float v);

      if (v < 0.12f)
      {
        return Dark;
      }

      if (s < 0.15f)
      {
        return Neutral;
      }

      if (h < 15f || h >= 345f) return "RED";
      if (h < 45f) return "ORANGE";
      if (h < 70f) return "YELLOW";
      if (h < 160f) return "GREEN";
      if (h < 200f) return "CYAN";
      if (h < 255f) return "BLUE";
      if (h < 290f) return "PURPLE";
      return "PINK";
    }

    /// <summary>
    /// Deplace une nuance comme la reference a ete deplacee vers la cible.
    /// </summary>
    public static Color Shift(Color source, Color reference, Color target)
    {
      ToHsv(source, out float sh, out float ss, out float sv);
      ToHsv(reference, out float rh, out float rs, out float rv);
      ToHsv(target, out float th, out float ts, out float tv);

      // Une reference grise ou noire n'a pas de teinte : lui appliquer un ecart
      // n'aurait pas de sens, on impose alors celle de la cible a tout le groupe.
      float hue = rs < 0.01f ? th : Wrap(sh + (th - rh));

      float saturation = rs > 0.01f ? Clamp01(ss * (ts / rs)) : ts;
      float value = rv > 0.01f ? Clamp01(sv * (tv / rv)) : tv;

      return FromHsv(hue, saturation, value);
    }

    // ------------------------------------------------------------------
    // Conversions
    // ------------------------------------------------------------------

    public static void ToHsv(Color color, out float h, out float s, out float v)
    {
      float r = color.R / 255f;
      float g = color.G / 255f;
      float b = color.B / 255f;

      float max = Math.Max(r, Math.Max(g, b));
      float min = Math.Min(r, Math.Min(g, b));
      float delta = max - min;

      v = max;
      s = max <= 0f ? 0f : delta / max;

      if (delta <= 0f)
      {
        h = 0f;
        return;
      }

      if (max == r)
      {
        h = 60f * (((g - b) / delta) % 6f);
      }
      else if (max == g)
      {
        h = 60f * (((b - r) / delta) + 2f);
      }
      else
      {
        h = 60f * (((r - g) / delta) + 4f);
      }

      h = Wrap(h);
    }

    public static Color FromHsv(float h, float s, float v)
    {
      h = Wrap(h);
      s = Clamp01(s);
      v = Clamp01(v);

      float c = v * s;
      float x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
      float m = v - c;

      float r, g, b;
      if (h < 60f) { r = c; g = x; b = 0f; }
      else if (h < 120f) { r = x; g = c; b = 0f; }
      else if (h < 180f) { r = 0f; g = c; b = x; }
      else if (h < 240f) { r = 0f; g = x; b = c; }
      else if (h < 300f) { r = x; g = 0f; b = c; }
      else { r = c; g = 0f; b = x; }

      return new Color(
          (int)Math.Round((r + m) * 255f),
          (int)Math.Round((g + m) * 255f),
          (int)Math.Round((b + m) * 255f));
    }

    private static float Wrap(float hue)
    {
      hue %= 360f;
      return hue < 0f ? hue + 360f : hue;
    }

    private static float Clamp01(float value)
    {
      return value < 0f ? 0f : (value > 1f ? 1f : value);
    }
  }
}
