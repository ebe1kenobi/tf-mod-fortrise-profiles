using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Portrait de l'archer du profil courant, affiche a cote de la liste et de
  /// l'ecran d'edition.
  ///
  /// C'est un MenuItem alors qu'il n'est jamais selectionnable, et volontairement :
  /// MainMenu ne nettoie automatiquement, au changement d'etat, que les MenuItem du
  /// calque -1. Une Entity ordinaire survivrait a la transition et resterait a
  /// l'ecran par-dessus le menu suivant.
  ///
  /// Le portrait est dessine directement plutot que via ArcherPortrait : ce composant
  /// porte tout un etat d'animation (retournement, gemme, tremblement) pilote par le
  /// rollcall, dont rien n'est utile ici.
  /// </summary>
  public class UIProfilePreview : MenuItem
  {
    private ProfileData profile;
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    public UIProfilePreview(Vector2 position) : base(position)
    {
      tweenTo = position;
      anchor = position;
      tweenFrom = position - Vector2.UnitX * 200f;
    }

    public void Show(ProfileData profile)
    {
      this.profile = profile;
    }

    public override void Render()
    {
      base.Render();

      if (profile == null)
      {
        return;
      }

      ArcherData archer = ArcherCatalog.DataOf(profile);
      if (archer == null)
      {
        return;
      }

      Subtexture portrait = archer.Portraits.Win;
      if (portrait == null)
      {
        return;
      }

      var origin = new Vector2(portrait.Width / 2f, portrait.Height / 2f);

      // Le cadre de couleur derriere le portrait est ce que le rollcall dessine
      // autour d'un archer rejoint : il rattache le portrait a son archer meme quand
      // deux profils ont choisi la meme silhouette.
      Vector2 corner = Calc.Round(Position - origin - Vector2.One * 2f);
      Vector2 size = Calc.Round(new Vector2(portrait.Width + 4f, portrait.Height + 4f));
      Draw.Rect(corner.X, corner.Y, size.X, size.Y, archer.ColorA);

      Draw.Texture(portrait, Position, Color.White, origin, 1f, 0f);

      var below = Position + new Vector2(0f, portrait.Height / 2f + 12f);
      Draw.OutlineTextCentered(TFGame.Font, archer.Name0 ?? "", below, archer.ColorB, Color.Black);
      Draw.OutlineTextCentered(TFGame.Font, archer.Name1 ?? "", below + new Vector2(0f, 10f), archer.ColorB, Color.Black);

      if (profile.IsAlt)
      {
        Draw.OutlineTextCentered(TFGame.Font, ProfileCostumes.Alt,
            below + new Vector2(0f, 22f), Calc.HexToColor("70FF6B"), Color.Black);
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
}
