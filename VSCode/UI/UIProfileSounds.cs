using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Liste des evenements sonores d'un profil, avec le nombre de sons attaches a
  /// chacun. Ouvre l'ecran de choix des fichiers.
  ///
  /// Aux evenements fixes s'ajoute un "tue par untel" par autre profil : la liste
  /// est donc reconstruite a chaque affichage plutot que figee.
  /// </summary>
  public class UIProfileSounds : CustomMenuState
  {
    /// <summary>Evenement que l'ecran de choix des fichiers doit ouvrir.</summary>
    internal static string EditingEvent;

    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 60f;

    private ProfileData profile;

    public UIProfileSounds(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState editState = ModRegisters.MenuState<UIProfileEdit>();

      profile = UIProfilesMenu.Editing;
      if (profile == null)
      {
        Main.State = ModRegisters.MenuState<UIProfilesMenu>();
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileSounds>());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      // Le vivier est relu ici : le joueur a pu deposer des WAV pendant que le jeu
      // tournait, et c'est le moment naturel pour les decouvrir.
      ProfileSfx.RefreshPool();

      var rows = new List<UIMenuRow>();

      foreach (string soundEvent in SoundEvents.Fixed)
      {
        rows.Add(MakeEventRow(rows.Count, soundEvent));
      }

      foreach (ProfileData other in ProfileStorage.Profiles)
      {
        if (ReferenceEquals(other, profile) || string.IsNullOrEmpty(other.Name))
        {
          continue;
        }

        rows.Add(MakeEventRow(rows.Count, SoundEvents.KilledBy(other.Name)));
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
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      Main.ToStartSelected = rows[0];

      Main.Add(new UIPoolHint(new Vector2(160f, 224f)));
    }

    public override void Destroy()
    {
    }

    private UIMenuRow MakeEventRow(int index, string soundEvent)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -240f : 560f, y);

      string captured = soundEvent;
      ProfileData owner = profile;

      var row = new UIMenuRow(new Vector2(RowX, y), from, SoundEvents.Label(soundEvent))
      {
        ContentWidth = 200f,
        RightText = () =>
        {
          int count = ProfileSfx.CountAssigned(owner.Name, captured);
          return count == 0 ? "-" : count.ToString();
        },
        OnConfirmed = () =>
        {
          EditingEvent = captured;
          Main.State = ModRegisters.MenuState<UIProfileSoundPicker>();
        }
      };

      return row;
    }
  }

  /// <summary>
  /// Rappel du chemin ou deposer les WAV. Sans lui, rien a l'ecran n'indique ou le
  /// vivier se remplit, et le menu paraitrait vide et inerte a la premiere ouverture.
  /// </summary>
  public class UIPoolHint : MenuItem
  {
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    public UIPoolHint(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position + Vector2.UnitY * 40f;
    }

    public override void Render()
    {
      base.Render();

      int count = ProfileSfx.Pool.Count;
      string text = count == 0
          ? "DROP .WAV FILES IN SAVES/PROFILES/WAV"
          : $"{count} WAV IN POOL - SAVES/PROFILES/WAV";

      Draw.TextCentered(TFGame.Font, text, Position, Color.Gray * 0.9f);
    }

    public override void Update()
    {
      base.Update();

      // La liste peut defiler sous ce panneau : on le replace a chaque image plutot
      // que de le laisser suivre la camera du calque.
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
