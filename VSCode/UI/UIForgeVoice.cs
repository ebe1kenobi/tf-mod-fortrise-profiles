using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// La voix d'un archer forge : une voix de repli, puis les sons qui la remplacent.
  ///
  /// La premiere ligne choisit l'archer du jeu dont la voix comble tout ce qui n'est
  /// pas fourni. Les vingt-et-une suivantes sont les actions, et chacune peut
  /// recevoir un WAV de la banque - celle-la meme ou les profils prennent leurs sons.
  ///
  /// Le repli agit action par action et non en bloc : poser un seul fichier sur MORT
  /// ne rend pas l'archer muet pour les vingt autres. C'est ce qui permet de
  /// commencer par un son et de s'arreter la.
  ///
  /// Un archer qui n'a rien ici a deja une voix complete. C'est aussi ce qui explique
  /// que tous les archers forges se ressemblaient a l'oreille : la valeur de repli
  /// par defaut est le vert.
  /// </summary>
  public class UIForgeVoice : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 14f;
    private const float RowX = 30f;

    private ForgeDesign design;

    /// <summary>Action que le selecteur de fichier doit remplir.</summary>
    internal static string EditingAction;

    public UIForgeVoice(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState editState = ModRegisters.MenuState<UIForgeEdit>();

      design = UIForgeList.Editing;

      if (design == null)
      {
        Main.State = editState;
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIForgeVoice>());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      var rows = new List<UIMenuRow>();

      UIMenuRow fallbackRow = MakeRow(rows.Count, "VOIX DE REPLI");
      fallbackRow.RightText = () => ForgeVoice.FallbackLabel(design.VoiceFallback);
      fallbackRow.OnLeft = () => CycleFallback(-1);
      fallbackRow.OnRight = () => CycleFallback(1);
      rows.Add(fallbackRow);

      foreach (ForgeVoiceAction action in ForgeVoice.Actions)
      {
        ForgeVoiceAction captured = action;

        UIMenuRow row = MakeRow(rows.Count, captured.Label);
        row.RightText = () => Label(captured);
        row.OnConfirmed = () => Open(captured);

        // Alt retire le son sans passer par le selecteur : c'est le geste inverse,
        // il doit couter le meme prix.
        row.OnAlt = () => Clear(captured);

        rows.Add(row);
      }

      for (int i = 0; i < rows.Count; i++)
      {
        if (i > 0)
        {
          rows[i].UpItem = rows[i - 1];
        }

        if (i + 1 < rows.Count)
        {
          rows[i].DownItem = rows[i + 1];
        }
      }

      Main.Add(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      ForgeStorage.Save();
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);
      return new UIMenuRow(new Vector2(RowX, y), from, label);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Ce que porte une action : le nom du fichier, ou la voix qui la comble.
    ///
    /// Un fichier assigne mais disparu de la banque doit se voir : il ne sera pas
    /// joue, et laisser le nom laisserait croire le contraire.
    /// </summary>
    private string Label(ForgeVoiceAction action)
    {
      string file = ForgeVoice.FileOf(design, action.Key);

      if (file == null)
      {
        return ForgeVoice.FallbackLabel(design.VoiceFallback);
      }

      return ForgeVoice.PathOf(design, action.Key) == null
          ? "INTROUVABLE"
          : UIForgeEdit.Shorten(Path.GetFileNameWithoutExtension(file).ToUpperInvariant());
    }

    private void CycleFallback(int direction)
    {
      design.VoiceFallback = ForgeVoice.NextFallback(design.VoiceFallback, direction);
    }

    private void Open(ForgeVoiceAction action)
    {
      EditingAction = action.Key;
      Main.State = ModRegisters.MenuState<UIForgeVoicePicker>();
    }

    private void Clear(ForgeVoiceAction action)
    {
      if (ForgeVoice.FileOf(design, action.Key) == null)
      {
        return;
      }

      ForgeVoice.Assign(design, action.Key, null);
      Sounds.ui_move2.Play(160f, 1f);
    }
  }
}
