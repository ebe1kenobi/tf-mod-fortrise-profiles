using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Les six emplacements d'image d'un profil, avec un apercu de celui qui est
  /// selectionne.
  ///
  /// Un emplacement ne recoit qu'une image, contrairement aux sons ou un evenement en
  /// accepte plusieurs : valider ouvre donc directement le choix du fichier, sans
  /// case a cocher.
  /// </summary>
  public class UIProfileImages : CustomMenuState
  {
    /// <summary>Emplacement que l'ecran de choix doit remplir.</summary>
    internal static string EditingSlot;

    private const float FirstRowY = 60f;
    private const float RowStep = 18f;
    private const float RowX = 30f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private ProfileData profile;
    private UIImagePreview preview;

    public UIProfileImages(MainMenu main) : base(main)
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

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileImages>());
      Main.BackState = ModRegisters.MenuState<UIProfileEdit>();
      Main.TweenBGCameraToY(2);

      ProfileImages.RefreshPool();

      preview = new UIImagePreview(PreviewPosition);
      Main.Add(preview);

      var rows = new List<UIMenuRow>();

      foreach (string slot in ProfileImages.Slots)
      {
        string captured = slot;
        float y = FirstRowY + rows.Count * RowStep;
        var from = new Vector2(rows.Count % 2 == 0 ? -240f : 560f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, ProfileImages.Label(slot))
        {
          ContentWidth = 150f,
          RightText = () => ProfileImages.HasImage(profile, captured) ? "SET" : "-",
          OnConfirmed = () =>
          {
            EditingSlot = captured;
            Main.State = ModRegisters.MenuState<UIProfileImagePicker>();
          },
          OnAlt = () =>
          {
            ProfileImages.Unassign(profile, captured);
            preview.Show(profile, captured);
          },
          AltGuide = "CLEAR",
          OnSelected = () => preview.Show(profile, captured)
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
      Main.Add(new UIImagePoolHint(new Vector2(160f, 224f)));

      Main.MaxUICameraY = 0f;
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
    }
  }

  /// <summary>
  /// Montre l'image d'un emplacement, ramenee dans un cadre fixe.
  ///
  /// Les portraits du jeu font 60x120 pour l'ecran de selection et 50x50 pour celui
  /// des resultats ; une image deposee peut avoir n'importe quelle taille. On la
  /// reduit pour qu'elle tienne, sans jamais l'agrandir : etirer du pixel art le rend
  /// flou et donnerait une fausse idee du rendu.
  /// </summary>
  public class UIImagePreview : MenuItem
  {
    private const float BoxWidth = 62f;
    private const float BoxHeight = 122f;

    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    private Subtexture image;
    private string slot;

    public UIImagePreview(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position + Vector2.UnitX * 120f;
    }

    public void Show(ProfileData profile, string slot)
    {
      this.slot = slot;
      image = ProfileImages.Get(profile, slot);
    }

    public override void Render()
    {
      base.Render();

      var corner = new Vector2(Position.X - BoxWidth * 0.5f, Position.Y - BoxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, BoxWidth, BoxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)BoxWidth, (int)BoxHeight), Color.Gray);

      if (image == null)
      {
        Draw.TextCentered(TFGame.Font, "NO IMAGE", Position, Color.Gray * 0.9f);
        return;
      }

      float scale = Math.Min(1f, Math.Min(BoxWidth / image.Width, BoxHeight / image.Height));
      var origin = new Vector2(image.Width / 2f, image.Height / 2f);
      Draw.Texture(image, Position, Color.White, origin, scale, 0f);

      if (!string.IsNullOrEmpty(slot))
      {
        Draw.TextCentered(TFGame.Font, image.Width + "X" + image.Height,
            new Vector2(Position.X, corner.Y + BoxHeight + 8f), Color.Gray * 0.9f);
      }
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

  /// <summary>Rappel du dossier ou deposer les PNG.</summary>
  public class UIImagePoolHint : MenuItem
  {
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    public UIImagePoolHint(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position + Vector2.UnitY * 40f;
    }

    public override void Render()
    {
      base.Render();

      int count = ProfileImages.Pool.Count;
      string text = count == 0
          ? "DROP .PNG FILES IN SAVES/EBE1.PROFILES/IMAGES"
          : $"{count} PNG IN POOL - SAVES/EBE1.PROFILES/IMAGES";

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
