using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Recoloration par familles de teinte : une couleur choisie pour tous les violets,
  /// et chaque nuance de violet la suit en gardant son propre eclairage.
  ///
  /// Les cases du haut disent sur quelles parties du sprite le reglage porte. La
  /// palette ne montre alors que les teintes de ces parties, et le remplacement n'est
  /// ecrit que pour elles : une couleur reglee sur la tete ne bouge plus quand on
  /// revient retoucher le corps, meme si les deux partagent la teinte.
  ///
  /// Les deux ecrans de couleur ecrivent dans la meme table : la famille y produit
  /// simplement plusieurs entrees d'un coup.
  /// </summary>
  public class UIProfileColorGroups : CustomMenuState
  {
    private const int MaxFamilies = 10;
    private const float FirstRowY = 44f;
    private const float RowStep = 14f;
    private const float RowX = 30f;
    private const float ContentWidth = 108f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private IColorSubject subject;
    private ColorPreview preview;

    private ColorTrial trial;
    private readonly List<MenuItem> rows = new List<MenuItem>();

    public UIProfileColorGroups(MainMenu main) : base(main)
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

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileColorGroups>());
      Main.BackState = ColorEditing.BackState;
      Main.TweenBGCameraToY(2);

      // Les profils d'avant la recoloration par partie portent des remplacements
      // globaux : on les rend explicites avant d'editer, sinon la premiere retouche
      // deplacerait des teintes sur des parties non selectionnees. Sans objet pour un
      // archer forge, dont la table n'a jamais eu de forme globale.
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

      UIMenuRow switchRow = MakeRow(items.Count, "SWITCH TO ONE BY ONE");

      switchRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileColors>();

      items.Add(switchRow);

      UIMenuRow adjustRow = MakeRow(items.Count, "ADJUST");

      adjustRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileAdjust>();

      items.Add(adjustRow);

      items.AddRange(UIColorPartRows.Build(
          FirstRowY + 2f * RowStep, RowStep, RowX, ContentWidth, () => Rebuild(0), subject.Groups));

      int firstFamilyRow = items.Count;
      List<string> parts = ColorSelection.Parts(subject);

      if (parts.Count > 0)
      {
        List<ColorFamily> families = ColorFamilies.Group(ColorPreview.Palette(subject, parts));
        int shown = Math.Min(families.Count, MaxFamilies);

        for (int i = 0; i < shown; i++)
        {
          items.Add(FamilyRow(items.Count, families[i], parts));
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

    /// <summary>
    /// Reconstruit les lignes : la palette depend des parties cochees, elle change
    /// donc a chaque bascule.
    /// </summary>
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

    private UIMenuRow FamilyRow(int index, ColorFamily family, List<string> parts)
    {
      ColorFamily captured = family;
      List<string> capturedParts = parts;

      UIMenuRow row = MakeRow(index, family.Name);
      row.RightText = () => captured.Members.Count + " SHADES";
      row.OnConfirmed = () => PickFromWheel(row, captured, capturedParts);
      row.OnAlt = () => PickFromHex(captured, capturedParts);
      row.AltGuide = "HEX CODE";

      row.OnRendered = () =>
      {
        Vector2 at = row.Position + new Vector2(row.ContentWidth + 16f, -4f);
        int shown = Math.Min(captured.Members.Count, 8);

        for (int i = 0; i < shown; i++)
        {
          Draw.Rect(at.X + i * 8f, at.Y, 7f, 9f, Current(captured.Members[i], capturedParts));
        }

        Draw.HollowRect(new Rectangle((int)at.X, (int)at.Y, shown * 8, 9), Color.Black);
      };

      return row;
    }

    /// <summary>
    /// Couleur effective d'une teinte : celle que porte la premiere partie
    /// selectionnee. Les parties selectionnees recoivent toutes le meme reglage, il
    /// suffit donc d'en consulter une.
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

    private void PickFromWheel(MenuItem back, ColorFamily family, List<string> parts)
    {
      Main.Add(new UIInputColor(back, chosen => SetFamily(family, chosen, parts), new Vector2(160f, 120f)));
    }

    private void PickFromHex(ColorFamily family, List<string> parts)
    {
      Main.Add(new VirtualKeyboard(
          "HEX COLOR - " + family.Name,
          SpriteRecolor.ToHex(Current(family.Reference, parts)),
          value => SpriteRecolor.TryParse(value, out _) ? null : "NEED 6 HEX DIGITS",
          value =>
          {
            if (SpriteRecolor.TryParse(value, out Color chosen))
            {
              SetFamily(family, chosen, parts);
            }
          }));
    }

    private void SetFamily(ColorFamily family, Color target, List<string> parts)
    {
      // La couleur choisie est un rendu final : on repasse en couleur de depart avant
      // de calculer le decalage, qui travaille sur les teintes brutes du sprite.
      target = ColorAdjust.Invert(target, trial);
      trial.Palette ??= new List<ColorSwap>();

      foreach (Color member in family.Members)
      {
        string from = SpriteRecolor.ToHex(member);
        string to = SpriteRecolor.ToHex(ColorFamilies.Shift(member, family.Reference, target));

        foreach (string part in parts)
        {
          trial.Palette.RemoveAll(s =>
              string.Equals(s.Part, part, StringComparison.OrdinalIgnoreCase)
              && string.Equals(s.From, from, StringComparison.OrdinalIgnoreCase));

          if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
          {
            trial.Palette.Add(new ColorSwap { Part = part, From = from, To = to });
          }
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
