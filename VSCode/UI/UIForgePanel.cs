using System;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
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

    /// <summary>
    /// Vrai si ce panneau laisse changer le facteur d'agrandissement. Reserve a
    /// l'ecran des poses : ailleurs, la gachette gauche sert deja a autre chose.
    /// </summary>
    protected virtual bool AllowZoomToggle => false;

    public override void Update()
    {
      base.Update();
      Position = MenuCamera.Fixed(MainMenu, Anchor);

      if (AllowZoomToggle && ForgeZoom.PressedToggle())
      {
        ForgeZoom.Cycle();
        Sounds.ui_move2.Play(160f, 1f);
      }
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
      return ZoomFor(width, height, BoxSize);
    }

    /// <summary>La meme chose, pour un panneau dont la zone n'a pas la taille commune.</summary>
    protected static float ZoomFor(int width, int height, float box)
    {
      int largest = Math.Max(Math.Max(width, height), 1);

      // Part du facteur choisi par le joueur - 1 par defaut, donc la TAILLE REELLE
      // du sprite tel qu'il sera en jeu. C'est ce qui permet de juger un archer
      // sans lancer une partie : un apercu agrandi flatte toujours, et on ne voit
      // qu'a l'export que le personnage est trop gros.
      float zoom = ForgeZoom.Factor;

      // Reduit malgre tout ce qui ne tiendrait pas dans la zone : une planche en
      // ilots peut donner des images de 152 pixels. Par moities, jamais par un
      // facteur quelconque - un rapport qui n'est pas une puissance de deux fait
      // disparaitre une colonne de pixels sur trois.
      while (largest * zoom > box)
      {
        zoom *= 0.5f;
      }

      return zoom;
    }

    /// <summary>
    /// Le cadre orange : la place que tiendrait un archer du jeu, pose sur la meme
    /// ancre que le dessin.
    ///
    /// Il ne decoupe rien. Depuis que le cadre reel se mesure sur les images
    /// choisies, ce rectangle ne sert plus qu'a comparer - et c'est justement pour
    /// cela qu'il vaut mieux qu'il ait la taille d'un archer d'origine plutot que
    /// celle de notre fenetre : la question qu'on se pose devant une pose reprise
    /// ailleurs est de combien elle depasse un personnage du jeu.
    ///
    /// Cale sur l'ancre et non sur un coin : deux rectangles de tailles differentes
    /// ne se comparent que s'ils reposent au meme endroit, ici les pieds au sol.
    /// </summary>
    /// <param name="corner">Coin haut-gauche de l'image dessinee, a l'ecran.</param>
    /// <param name="anchorX">Ou tombe l'ancre dans l'image, en pixels d'image.</param>
    protected static void DrawVanillaFrame(Vector2 corner, float anchorX, float anchorY, float zoom)
    {
      Draw.HollowRect(new Rectangle(
          (int)(corner.X + (anchorX - ForgeSlots.VanillaAnchorX) * zoom),
          (int)(corner.Y + (anchorY - ForgeSlots.VanillaAnchorY) * zoom),
          (int)(ForgeSlots.VanillaWidth * zoom),
          (int)(ForgeSlots.VanillaHeight * zoom)), Color.Orange * 0.8f);
    }

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
