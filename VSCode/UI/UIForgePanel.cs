using System;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Panneau immobile pose a cote d'une liste : apercu, vignette, message.
  ///
  /// Les MenuItem vivent sur le calque -1, qui est une CameraLayer : quand une liste
  /// depasse la hauteur de l'ecran, MainMenu deplace cette camera et tout ce qui s'y
  /// trouve suit - y compris ce qu'on voulait immobile. D'ou l'ancre : la position
  /// voulue a l'ecran, a laquelle on se replace a chaque image en ajoutant le
  /// decalage de la camera.
  ///
  /// MenuItem declare TweenIn, TweenOut, OnSelect, OnDeselect et OnConfirm abstraits.
  /// Un panneau ne se selectionne pas et n'a rien a faire des trois derniers ; les
  /// ecrire vides dans chaque panneau serait de la place perdue cinq fois. Les
  /// interpolations ecrivent l'ancre et non la position, sinon les deux se
  /// marcheraient dessus a chaque image.
  /// </summary>
  public abstract class UIForgePanel : MenuItem
  {
    /// <summary>Cote maximal d'un apercu a l'ecran, en pixels.</summary>
    protected const int BoxSize = 112;

    protected Vector2 Anchor;

    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    protected UIForgePanel(Vector2 position) : this(position, position + Vector2.UnitX * 120f)
    {
    }

    protected UIForgePanel(Vector2 position, Vector2 from) : base(position)
    {
      Anchor = position;
      tweenTo = position;
      tweenFrom = from;
    }

    public override void Update()
    {
      base.Update();
      Position = MenuCamera.Fixed(MainMenu, Anchor);
    }

    public override void TweenIn()
    {
      Anchor = tweenFrom;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 20, true);
      tween.OnUpdate = t => Anchor = Vector2.Lerp(tweenFrom, tweenTo, t.Eased);
      Add(tween);
    }

    public override void TweenOut()
    {
      Vector2 start = Anchor;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 12, true);
      tween.OnUpdate = t => Anchor = Vector2.Lerp(start, tweenFrom, t.Eased);
      Add(tween);
    }

    /// <summary>
    /// Agrandissement d'un apercu, deduit de sa taille plutot que fixe.
    ///
    /// Un facteur constant convenait tant que toutes les cases faisaient 32 : depuis
    /// que le vivier en accepte d'autres, le meme quatre donnerait 256 pixels de haut
    /// pour une case de 64. Le canevas d'une pose grandit d'ailleurs tout seul des
    /// qu'un calque deborde.
    ///
    /// En dessous de un, on divise par deux plutot que par trois ou cinq : une case de
    /// 128 ne tient pas dans la zone d'apercu et doit etre reduite, mais un facteur
    /// qui n'est pas une puissance de deux fait disparaitre une colonne de pixels sur
    /// trois - sur un dessin de seize pixels de large, c'est le personnage qu'on ne
    /// reconnait plus.
    /// </summary>
    protected static float ZoomFor(int width, int height)
    {
      int largest = Math.Max(Math.Max(width, height), 1);

      if (largest <= BoxSize)
      {
        return Math.Clamp(BoxSize / largest, 1, 6);
      }

      float zoom = 1f;

      while (largest * zoom > BoxSize)
      {
        zoom *= 0.5f;
      }

      return zoom;
    }

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
