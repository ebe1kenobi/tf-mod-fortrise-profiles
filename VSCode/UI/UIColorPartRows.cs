using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Les cases a cocher qui ouvrent les deux ecrans de couleur : sur quelles parties
  /// du sprite le reglage porte-t-il.
  ///
  /// Partage par l'ecran par familles et l'ecran couleur par couleur, qui les
  /// affichent a l'identique et lisent la meme selection.
  /// </summary>
  internal static class UIColorPartRows
  {
    /// <summary>
    /// Construit une ligne par partie, cochable a la validation.
    /// </summary>
    /// <param name="onChanged">
    /// Appele apres chaque bascule : l'ecran doit se reconstruire, la palette ne
    /// portant plus sur les memes planches.
    /// </param>
    /// <param name="groups">
    /// Les familles a proposer. Elles viennent du sujet et non de la liste complete :
    /// un archer forge n'a ni tete separee ni arc a lui, et ces cases y resteraient
    /// sans effet - une case qui ne fait rien est pire qu'une case absente.
    /// </param>
    public static List<UIMenuRow> Build(
        float firstY, float step, float x, float contentWidth, Action onChanged,
        IReadOnlyList<string> groups)
    {
      var rows = new List<UIMenuRow>();

      foreach (string group in groups)
      {
        string captured = group;
        float y = firstY + rows.Count * step;
        var from = new Vector2(rows.Count % 2 == 0 ? -240f : 560f, y);

        rows.Add(new UIMenuRow(new Vector2(x, y), from, captured)
        {
          ContentWidth = contentWidth,
          RightText = () => ColorSelection.IsSelected(captured) ? "[X]" : "[ ]",
          OnConfirmed = () =>
          {
            ColorSelection.Toggle(captured);
            onChanged();
          }
        });
      }

      return rows;
    }
  }
}
