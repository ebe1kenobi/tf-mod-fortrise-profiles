using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Reglages d'ensemble de l'essai actif : saturation, teinte, luminosite, contraste.
  ///
  /// Ils s'appliquent apres le remplacement des couleurs et sur tout le sprite - un
  /// reglage de lumiere se juge sur la silhouette entiere, le limiter a la tete
  /// donnerait un personnage incoherent.
  ///
  /// Les quatre ne se valent pas. La saturation est celle qui change le plus
  /// l'impression sans nuire a la lecture ; le contraste est la plus risquee, une
  /// silhouette de douze pixels n'ayant que quelques teintes pour rendre son volume.
  /// D'ou l'ordre d'affichage.
  /// </summary>
  public class UIProfileAdjust : CustomMenuState
  {
    private const float FirstRowY = 66f;
    private const float RowStep = 18f;
    private const float RowX = 30f;
    private const float ContentWidth = 150f;
    private const float Step = 0.05f;
    private const float HueStep = 5f;
    private static readonly Vector2 PreviewPosition = new Vector2(250f, 120f);

    private ProfileData profile;
    private ColorTrial trial;
    private UISpritePreview preview;

    public UIProfileAdjust(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      profile = UIProfilesMenu.Editing;
      trial = ProfileTrials.Active(profile);

      if (profile == null || trial == null)
      {
        Main.State = ModRegisters.MenuState<UIProfileTrials>();
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileAdjust>());
      Main.BackState = ModRegisters.MenuState<UIProfileColorGroups>();
      Main.TweenBGCameraToY(2);

      preview = new UISpritePreview(PreviewPosition);
      Main.Add(preview);
      preview.Rebuild(profile);

      var rows = new List<UIMenuRow>
      {
        Slider(0, "SATURATION",
            () => trial.Saturation,
            v => trial.Saturation = Clamp(v, 0f, 3f)),

        Slider(1, "HUE",
            () => trial.Hue,
            v => trial.Hue = Wrap(v), HueStep, degrees: true),

        Slider(2, "BRIGHTNESS",
            () => trial.Brightness,
            v => trial.Brightness = Clamp(v, 0.2f, 2f)),

        Slider(3, "CONTRAST",
            () => trial.Contrast,
            v => trial.Contrast = Clamp(v, 0.2f, 2.5f))
      };

      UIMenuRow reset = MakeRow(rows.Count, "RESET ADJUSTMENTS");
      reset.OnConfirmed = () =>
      {
        trial.ResetAdjustments();
        Apply();
      };
      rows.Add(reset);

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
      Main.MaxUICameraY = 0f;
      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      SpriteRecolor.Export(profile);
      ProfileStorage.Save();
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -240f : 560f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label) { ContentWidth = ContentWidth };
    }

    private UIMenuRow Slider(int index, string label, Func<float> get, Action<float> set,
                             float step = Step, bool degrees = false)
    {
      UIMenuRow row = MakeRow(index, label);

      // Pas de signe degre ni de pourcent : la police du jeu ne les connait pas. Le
      // filtre de MenuText les retirerait sans planter, mais autant afficher quelque
      // chose de voulu plutot qu'un mot amoindri.
      row.RightText = () => degrees
          ? (get() > 0f ? "+" : "") + Math.Round(get())
          : Math.Round(get() * 100f).ToString();

      row.OnLeft = () => { set(get() - step); Apply(); };
      row.OnRight = () => { set(get() + step); Apply(); };

      return row;
    }

    private static float Clamp(float value, float min, float max)
    {
      return value < min ? min : (value > max ? max : value);
    }

    /// <summary>La teinte tourne : -180 et 180 designent le meme decalage.</summary>
    private static float Wrap(float degrees)
    {
      while (degrees > 180f) degrees -= 360f;
      while (degrees < -180f) degrees += 360f;
      return degrees;
    }

    private void Apply()
    {
      SpriteRecolor.Invalidate(profile);
      preview?.Rebuild(profile);
    }
  }
}
