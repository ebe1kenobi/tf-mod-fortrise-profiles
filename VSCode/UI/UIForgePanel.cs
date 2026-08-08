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

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
