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
  /// Choix du PNG d'un emplacement. Valider affecte le fichier et revient a la liste :
  /// un emplacement n'en recoit qu'un, il n'y a pas de selection multiple a tenir.
  ///
  /// L'apercu suit la ligne survolee, pour choisir sur l'image et non sur un nom de
  /// fichier.
  /// </summary>
  public class UIProfileImagePicker : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 30f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private ProfileData profile;
    private string slot;
    private UIImageFilePreview preview;

    public UIProfileImagePicker(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState listState = ModRegisters.MenuState<UIProfileImages>();

      profile = UIProfilesMenu.Editing;
      slot = UIProfileImages.EditingSlot;

      if (profile == null || string.IsNullOrEmpty(slot))
      {
        Main.State = listState;
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileImagePicker>());
      Main.BackState = listState;
      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), profile.Name, ProfileImages.Label(slot)));

      preview = new UIImageFilePreview(PreviewPosition);
      Main.Add(preview);

      IReadOnlyList<ImageFile> files = ProfileImages.Pool;

      if (files.Count == 0)
      {
        Main.Add(new UIImagePoolHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      for (int i = 0; i < files.Count; i++)
      {
        ImageFile file = files[i];
        float y = FirstRowY + i * RowStep;
        var from = new Vector2(i % 2 == 0 ? -260f : 580f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, Label(file))
        {
          ContentWidth = 150f,
          RightText = () => file.Source == "MOD" ? "MOD" : "",
          OnConfirmed = () =>
          {
            if (ProfileImages.Assign(profile, slot, file))
            {
              Main.State = listState;
            }
            else
            {
              Sounds.ui_invalid.Play(160f, 1f);
            }
          },
          OnSelected = () => preview.Show(file)
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

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      preview?.Release();
    }

    /// <summary>
    /// Nom du fichier sans extension, en majuscules et tronque : la police du jeu ne
    /// dessine pas lisiblement les minuscules.
    /// </summary>
    private static string Label(ImageFile file)
    {
      string name = file.Name;
      if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
      {
        name = name.Substring(0, name.Length - 4);
      }

      name = name.ToUpperInvariant();
      return name.Length > 20 ? name.Substring(0, 19) + "." : name;
    }
  }

  /// <summary>
  /// Apercu d'un fichier du vivier, decode a la volee.
  ///
  /// La texture est liberee des qu'on passe a une autre ligne : parcourir le vivier
  /// en laisserait sinon une par fichier survole sur la carte graphique.
  /// </summary>
  public class UIImageFilePreview : MenuItem
  {
    private const float BoxWidth = 62f;
    private const float BoxHeight = 122f;

    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    private Microsoft.Xna.Framework.Graphics.Texture2D texture;
    private string shownName;

    public UIImageFilePreview(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position + Vector2.UnitX * 120f;
    }

    public void Show(ImageFile file)
    {
      if (file == null || string.Equals(file.Name, shownName, StringComparison.OrdinalIgnoreCase))
      {
        return;
      }

      Release();

      try
      {
        using Stream stream = file.Open();
        texture = Microsoft.Xna.Framework.Graphics.Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);
        shownName = file.Name;
      }
      catch (Exception e)
      {
        Log.Error($"[Images] apercu de {file.Name} impossible : {e.Message}");
      }
    }

    public void Release()
    {
      try { texture?.Dispose(); } catch { }
      texture = null;
      shownName = null;
    }

    public override void Render()
    {
      base.Render();

      var corner = new Vector2(Position.X - BoxWidth * 0.5f, Position.Y - BoxHeight * 0.5f);
      Draw.Rect(corner.X, corner.Y, BoxWidth, BoxHeight, Color.Black * 0.55f);
      Draw.HollowRect(new Rectangle((int)corner.X, (int)corner.Y, (int)BoxWidth, (int)BoxHeight), Color.Gray);

      if (texture == null || texture.IsDisposed)
      {
        return;
      }

      float scale = Math.Min(1f, Math.Min(BoxWidth / texture.Width, BoxHeight / texture.Height));
      var origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

      Draw.SpriteBatch.Draw(texture, Position, null, Color.White, 0f, origin,
          scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);

      Draw.TextCentered(TFGame.Font, texture.Width + "X" + texture.Height,
          new Vector2(Position.X, corner.Y + BoxHeight + 8f), Color.Gray * 0.9f);
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
