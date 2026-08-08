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
  /// Les essais de couleurs du profil, pour l'archer et le costume courants.
  ///
  /// C'est desormais la porte d'entree de COLORS : on choisit d'abord sur quel essai
  /// on travaille, ou on en cree un. L'apercu suit la ligne survolee et montre l'essai
  /// tel qu'il rendrait, ce qui evite d'avoir a l'activer pour le juger.
  ///
  /// La liste est filtree sur l'archer courant : un essai n'a de sens que pour le
  /// sprite sur lequel il a ete fait, ses cles etant les teintes d'origine de
  /// celui-ci. Changer d'archer ne perd donc rien, cela change seulement ce qu'on voit.
  /// </summary>
  public class UIProfileTrials : CustomMenuState
  {
    private const float FirstRowY = 56f;
    private const float RowStep = 16f;
    private const float RowX = 30f;
    private const float ContentWidth = 150f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private ProfileData profile;
    private UISpritePreview preview;
    private readonly List<MenuItem> rows = new List<MenuItem>();

    public UIProfileTrials(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      profile = UIProfilesMenu.Editing;
      if (profile == null)
      {
        Main.State = ModRegisters.MenuState<UIProfilesMenu>();
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileTrials>());
      Main.BackState = ModRegisters.MenuState<UIProfileEdit>();
      Main.TweenBGCameraToY(2);

      // Les profils d'avant les essais ont une palette unique : elle devient un essai
      // nomme DEFAULT, sans quoi cette liste paraitrait vide alors que le sprite est
      // bel et bien recolore.
      ProfileTrials.Migrate(profile);

      preview = new UISpritePreview(PreviewPosition);
      Main.Add(preview);
      preview.Rebuild(profile);

      Build(0);
    }

    public override void Destroy()
    {
      ProfileStorage.Save();
      rows.Clear();
    }

    private void Build(int selectIndex)
    {
      var items = new List<UIMenuRow>();

      UIMenuRow create = MakeRow(items.Count, "+ NEW TRIAL");
      create.OnConfirmed = AskNewTrial;
      items.Add(create);

      UIMenuRow import = MakeRow(items.Count, "+ IMPORT");
      import.RightText = () => ProfileTrials.Exported().Count.ToString();
      import.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileTrialImport>();
      items.Add(import);

      foreach (ColorTrial trial in ProfileTrials.For(profile))
      {
        items.Add(TrialRow(items.Count, trial));
      }

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

    private UIMenuRow TrialRow(int index, ColorTrial trial)
    {
      ColorTrial captured = trial;

      UIMenuRow row = MakeRow(index, trial.Name);
      row.RightText = () => ProfileTrials.IsActive(profile, captured) ? "ACTIVE" : "";
      row.OnConfirmed = () => Open(captured);
      row.OnAlt = () => ShowActions(row, captured);
      row.AltGuide = "MORE";

      // L'apercu montre l'essai survole sans l'activer : on juge avant de choisir.
      row.OnSelected = () => Show(captured);

      return row;
    }

    /// <summary>
    /// Montre un essai dans l'apercu. Il faut l'activer le temps de fabriquer les
    /// textures, la recoloration se faisant toujours a partir de l'essai actif ; on
    /// remet ensuite celui qui l'etait.
    /// </summary>
    private void Show(ColorTrial trial)
    {
      ColorTrial previous = ProfileTrials.Active(profile);
      ProfileTrials.SetActive(profile, trial);
      preview?.Rebuild(profile);
      ProfileTrials.SetActive(profile, previous);
    }

    private void Open(ColorTrial trial)
    {
      ProfileTrials.SetActive(profile, trial);
      Main.State = ModRegisters.MenuState<UIProfileColorGroups>();
    }

    private void AskNewTrial()
    {
      Main.Add(new VirtualKeyboard(
          "TRIAL NAME",
          "",
          name =>
          {
            if (string.IsNullOrEmpty(name)) return "EMPTY NAME";
            return ProfileTrials.NameTaken(profile, name) ? "ALREADY IN THE LIST" : null;
          },
          name =>
          {
            ColorTrial trial = ProfileTrials.Create(profile, name);
            ProfileStorage.Save();
            Open(trial);
          }));
    }

    private void ShowActions(MenuItem back, ColorTrial trial)
    {
      MenuItem selected = back;
      Main.CanAct = false;
      selected.Selected = false;

      void Restore()
      {
        Main.CanAct = true;
        if (selected.Scene != null)
        {
          selected.Selected = true;
        }
      }

      var modal = new UIModal(0);
      modal.SetTitle(trial.Name);

      modal.AddItem("EXPORT", () =>
      {
        Restore();
        string path = ProfileTrials.Export(profile, trial);
        Sounds.ui_click.Play(160f, 1f);

        if (path != null)
        {
          Log.Info($"[Trials] {trial.Name} exporte vers {path}");
        }
      });

      modal.AddItem("DELETE", () =>
      {
        Restore();
        ProfileTrials.Delete(profile, trial);
        ProfileStorage.Save();
        preview?.Rebuild(profile);
        Rebuild(0);
      });

      modal.AddItem("CANCEL", Restore);
      modal.SetOnBackCallBack(Restore);

      Main.Add(modal);
    }
  }

  /// <summary>
  /// Import d'un essai depuis le vivier partage.
  ///
  /// Un essai fait pour un autre archer reste importable : il ne figurera simplement
  /// dans la liste des essais que lorsque le profil sera sur cet archer-la. C'est
  /// preferable a un refus, qui obligerait a changer d'archer avant d'importer.
  /// </summary>
  public class UIProfileTrialImport : CustomMenuState
  {
    private const float FirstRowY = 56f;
    private const float RowStep = 15f;
    private const float RowX = 20f;

    private ProfileData profile;

    public UIProfileTrialImport(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState listState = ModRegisters.MenuState<UIProfileTrials>();

      profile = UIProfilesMenu.Editing;
      if (profile == null)
      {
        Main.State = ModRegisters.MenuState<UIProfilesMenu>();
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileTrialImport>());
      Main.BackState = listState;
      Main.TweenBGCameraToY(2);

      List<string> files = ProfileTrials.Exported();

      if (files.Count == 0)
      {
        Main.Add(new UITrialPoolHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      for (int i = 0; i < files.Count; i++)
      {
        string path = files[i];
        ColorTrial peek = ProfileTrials.Read(path);

        float y = FirstRowY + i * RowStep;
        var from = new Vector2(i % 2 == 0 ? -260f : 580f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, Label(path))
        {
          ContentWidth = 280f,
          RightText = () => peek == null ? "BAD FILE" : peek.Archer,
          OnConfirmed = () =>
          {
            if (peek == null)
            {
              Sounds.ui_invalid.Play(160f, 1f);
              return;
            }

            ProfileTrials.Import(profile, peek);
            ProfileStorage.Save();
            Main.State = listState;
          }
        };

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
      Main.Add(new UITrialPoolHint(new Vector2(160f, 224f)));

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
    }

    private static string Label(string path)
    {
      string name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
      return name.Length > 24 ? name.Substring(0, 23) + "." : name;
    }
  }

  /// <summary>Rappel du dossier ou les essais s'exportent et se deposent.</summary>
  public class UITrialPoolHint : MenuItem
  {
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    public UITrialPoolHint(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position + Vector2.UnitY * 40f;
    }

    public override void Render()
    {
      base.Render();
      Draw.TextCentered(TFGame.Font, "SAVES/EBE1.PROFILES/TRIALS", Position, Color.Gray * 0.9f);
    }

    public override void Update()
    {
      base.Update();
      Position = MenuCamera.Fixed(MainMenu, anchor);
    }

    public override void TweenIn()
    {
      anchor = tweenFrom;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 20, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(tweenFrom, tweenTo, t.Eased);
      Add(tween);
    }

    public override void TweenOut()
    {
      Vector2 start = anchor;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 12, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(start, tweenFrom, t.Eased);
      Add(tween);
    }

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
