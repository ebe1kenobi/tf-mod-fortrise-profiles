using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Recoloration couleur par couleur : les teintes dominantes a gauche, le sprite
  /// anime a droite, remis a jour a chaque changement.
  ///
  /// Les cases du haut disent sur quelles parties du sprite le reglage porte, et la
  /// palette ne montre que les teintes de ces parties. Une couleur reglee sur la tete
  /// ne bouge plus quand on revient retoucher le corps.
  /// </summary>
  public class UIProfileColors : CustomMenuState
  {
    private const int MaxColors = 10;
    private const float FirstRowY = 44f;
    private const float RowStep = 14f;
    private const float RowX = 30f;
    private const float ContentWidth = 108f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private IColorSubject subject;
    private ColorPreview preview;

    private ColorTrial trial;
    private readonly List<MenuItem> rows = new List<MenuItem>();

    public UIProfileColors(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      subject = ColorEditing.Subject;

      if (subject == null || subject.Trial == null)
      {
        Main.State = ColorEditing.BackState;
        return;
      }

      trial = subject.Trial;

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileColors>());
      Main.BackState = ColorEditing.BackState;
      Main.TweenBGCameraToY(2);

      // Voir UIProfileColorGroups : les remplacements globaux des anciens profils sont
      // rendus explicites avant toute retouche. Sans objet pour un archer forge, dont
      // la table n'a jamais eu de forme globale.
      if (subject is ProfileColorSubject legacy)
      {
        SpriteRecolor.MakePartsExplicit(legacy.Profile);
      }

      preview = ColorPreview.For(subject, PreviewPosition);
      Main.Add(preview.Item);
      preview.Rebuild();

      Build(0);
    }

    public override void Destroy()
    {
      ColorPreview.Persist(subject);
      rows.Clear();
    }

    private void Build(int selectIndex)
    {
      var items = new List<UIMenuRow>();

      UIMenuRow switchRow = MakeRow(items.Count, "SWITCH TO COLOR GROUPS");

      switchRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileColorGroups>();

      items.Add(switchRow);

      UIMenuRow adjustRow = MakeRow(items.Count, "ADJUST");

      adjustRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileAdjust>();

      items.Add(adjustRow);

      items.AddRange(UIColorPartRows.Build(
          FirstRowY + 2f * RowStep, RowStep, RowX, ContentWidth, () => Rebuild(0), subject.Groups));

      List<string> parts = ColorSelection.Parts(subject);

      if (parts.Count > 0)
      {
        List<PaletteColor> palette = ColorPreview.Palette(subject, parts);
        int shown = Math.Min(palette.Count, MaxColors);

        for (int i = 0; i < shown; i++)
        {
          items.Add(ColorRow(items.Count, palette[i].Source, parts));
        }
      }

      UIMenuRow reset = MakeRow(items.Count, "RESET ALL COLORS");
      reset.OnConfirmed = () =>
      {
        trial.Palette = null;
        Apply();
        Rebuild(0);
      };
      items.Add(reset);

      for (int i = 0; i < items.Count; i++)
      {
        if (i > 0)
        {
          items[i].UpItem = items[i - 1];
        }

        if (i + 1 < items.Count)
        {
          items[i].DownItem = items[i + 1];
        }
      }

      Main.Add(items);
      rows.AddRange(items);

      float lastY = FirstRowY + (items.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);

      MenuItem toSelect = items[Math.Clamp(selectIndex, 0, items.Count - 1)];
      Main.ToStartSelected = toSelect;

      if (!Main.Transitioning)
      {
        toSelect.Selected = true;
      }
    }

    private void Rebuild(int selectIndex)
    {
      foreach (MenuItem item in rows)
      {
        Main.Remove(item);
      }

      rows.Clear();
      Build(selectIndex);
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -240f : 560f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = ContentWidth };
    }

    private UIMenuRow ColorRow(int index, Color source, List<string> parts)
    {
      Color captured = source;
      List<string> capturedParts = parts;

      UIMenuRow row = MakeRow(index, SpriteRecolor.ToHex(source));
      row.RightText = () => SpriteRecolor.ToHex(Current(captured, capturedParts));
      row.OnConfirmed = () => PickFromWheel(row, captured, capturedParts);
      row.OnAlt = () => PickFromHex(captured, capturedParts);
      row.AltGuide = "HEX CODE";

      row.OnRendered = () =>
      {
        Vector2 at = row.Position + new Vector2(row.ContentWidth + 16f, -4f);
        Draw.Rect(at.X, at.Y, 9f, 9f, Current(captured, capturedParts));
        Draw.HollowRect(new Rectangle((int)at.X, (int)at.Y, 9, 9), Color.Black);
      };

      return row;
    }

    /// <summary>
    /// Couleur effective d'une teinte sur la premiere partie selectionnee. Les parties
    /// cochees recoivent toutes le meme reglage, une seule suffit a le lire.
    /// </summary>
    private Color Current(Color source, List<string> parts)
    {
      // La couleur montree est celle qui finit sur le sprite : le remplacement, puis
      // les reglages d''ensemble. Sans ce second passage, toucher un curseur changeait
      // le personnage sans que la palette bouge.
      if (trial.Palette == null || parts.Count == 0)
      {
        return ColorAdjust.Apply(source, trial);
      }

      string from = SpriteRecolor.ToHex(source);

      foreach (ColorSwap swap in trial.Palette)
      {
        if (string.Equals(swap.Part, parts[0], StringComparison.OrdinalIgnoreCase)
            && string.Equals(swap.From, from, StringComparison.OrdinalIgnoreCase)
            && SpriteRecolor.TryParse(swap.To, out Color to))
        {
          return ColorAdjust.Apply(to, trial);
        }
      }

      return ColorAdjust.Apply(source, trial);
    }

    private void PickFromWheel(MenuItem back, Color source, List<string> parts)
    {
      Main.Add(new UIInputColor(back, chosen => Set(source, chosen, parts), new Vector2(160f, 120f)));
    }

    private void PickFromHex(Color source, List<string> parts)
    {
      Main.Add(new VirtualKeyboard(
          "HEX COLOR - " + SpriteRecolor.ToHex(source),
          SpriteRecolor.ToHex(Current(source, parts)),
          value => SpriteRecolor.TryParse(value, out _) ? null : "NEED 6 HEX DIGITS",
          value =>
          {
            if (SpriteRecolor.TryParse(value, out Color chosen))
            {
              Set(source, chosen, parts);
            }
          }));
    }

    private void Set(Color source, Color replacement, List<string> parts)
    {
      trial.Palette ??= new List<ColorSwap>();

      string from = SpriteRecolor.ToHex(source);
      // La couleur choisie est celle qu''on veut voir : c''est donc sa couleur de depart
      // qu''il faut ranger, sinon les reglages s''appliqueraient une seconde fois.
      string to = SpriteRecolor.ToHex(ColorAdjust.Invert(replacement, trial));

      foreach (string part in parts)
      {
        trial.Palette.RemoveAll(s =>
            string.Equals(s.Part, part, StringComparison.OrdinalIgnoreCase)
            && string.Equals(s.From, from, StringComparison.OrdinalIgnoreCase));

        // Remettre sa teinte d'origine, c'est retirer l'entree : une identite
        // encombrerait la table sans rien changer.
        if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
          trial.Palette.Add(new ColorSwap { Part = part, From = from, To = to });
        }
      }

      if (trial.Palette.Count == 0)
      {
        trial.Palette = null;
      }

      Apply();
    }

    private void Apply()
    {
      subject.Invalidate();
      preview?.Rebuild();
    }
  }
}
