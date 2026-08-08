using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Fusionne les calques d'une pose pour l'affichage, a la taille de la case.
  ///
  /// La fabrication fait la meme chose dans <see cref="ForgeBuild"/>, mais apres
  /// decoupe : elle n'assemble que ce que la fenetre garde. Les ecrans, eux,
  /// montrent la case entiere avec la fenetre posee par-dessus - c'est ce qui permet
  /// de voir qu'un bras sort du cadre. Les deux fusions ne travaillent donc pas sur
  /// la meme image et ne peuvent pas etre le meme code ; seule la regle de
  /// recouvrement leur est commune.
  /// </summary>
  public static class ForgeCompose
  {
    /// <summary>
    /// Les pixels d'une pose, calques fusionnes dans l'ordre du choix, ou null.
    ///
    /// Un calque introuvable est saute et non fatal, comme a la fabrication : mieux
    /// vaut un apercu ampute des bras qu'aucun apercu. <paramref name="drawn"/> dit
    /// combien ont ete reellement poses, ce qui permet a l'appelant de ne pas
    /// annoncer trois images quand une planche manque.
    /// </summary>
    public static Color[] Pose(ForgeDesign design, string slotKey,
        out int width, out int height, out int drawn)
    {
      width = 0;
      height = 0;
      drawn = 0;

      if (design == null)
      {
        return null;
      }

      List<ForgePick> stack = design.LayersOf(slotKey);
      Color[] merged = null;

      foreach (ForgePick pick in stack)
      {
        if (pick == null)
        {
          continue;
        }

        ForgeSource source = ForgeBank.Find(pick.Source);

        if (source == null)
        {
          continue;
        }

        Color[] layer = ForgeBank.ReadCell(source, pick.Cell);

        if (layer == null)
        {
          continue;
        }

        // La premiere image reellement lue donne la taille : c'est le fond, les
        // suivantes se posent dedans. Une planche aux cases plus grandes deborderait,
        // ce que Over rogne plutot que de refuser la pose entiere.
        if (merged == null)
        {
          merged = layer;
          width = source.CellWidth;
          height = source.CellHeight;
        }
        else
        {
          Over(merged, width, height, layer, source.CellWidth, source.CellHeight);
        }

        drawn++;
      }

      return merged;
    }

    /// <summary>Vrai si la pose a des calques, meme illisibles.</summary>
    public static int Count(ForgeDesign design, string slotKey)
    {
      return design == null ? 0 : design.LayersOf(slotKey).Count;
    }

    /// <summary>
    /// Superpose un calque sur le fond, cale en haut a gauche.
    ///
    /// Les sprites sont a bords francs : un pixel est opaque ou il ne l'est pas. On
    /// recouvre donc plutot que de melanger, sans quoi un pixel semi-opaque ferait un
    /// halo absent du dessin d'origine. Les tailles sont passees separement parce que
    /// deux planches n'ont pas forcement la meme case : une fusion a plat sur les
    /// index decalerait le calque d'une ligne par pixel d'ecart.
    /// </summary>
    private static void Over(Color[] under, int underWidth, int underHeight,
        Color[] above, int aboveWidth, int aboveHeight)
    {
      int columns = Math.Min(underWidth, aboveWidth);
      int rows = Math.Min(underHeight, aboveHeight);

      for (int y = 0; y < rows; y++)
      {
        for (int x = 0; x < columns; x++)
        {
          Color pixel = above[y * aboveWidth + x];

          if (pixel.A != 0)
          {
            under[y * underWidth + x] = pixel;
          }
        }
      }
    }
  }
}
