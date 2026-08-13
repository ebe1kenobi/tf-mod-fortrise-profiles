using System;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Ligne de menu : un libelle a gauche, une valeur facultative alignee sur le bord
  /// droit, et selon les cas une validation, un cyclage gauche/droite et un
  /// raccourci sur le bouton Alt.
  ///
  /// OptionsButton, le bouton de l'ecran d'options du jeu, ne convient pas ici : il
  /// centre sa valeur a trente pixels de son ancrage, ce qui va pour "ON" ou "OFF"
  /// mais fait chevaucher le libelle des qu'on affiche un nom d'archer. Il n'expose
  /// pas non plus de raccourci sur le bouton Alt, dont on a besoin pour supprimer.
  /// </summary>
  public class UIMenuRow : MenuItem
  {
    /// <summary>
    /// Largeur utile de la ligne : la valeur est calee sur son bord droit. Nommee
    /// ainsi et non Width, qui designe deja chez Entity la boite de collision.
    /// </summary>
    public float ContentWidth = 190f;

    /// <summary>Valeur affichee a droite. Relue a chaque image, elle suit les changements.</summary>
    public Func<string> RightText;

    public Action OnConfirmed;
    public Action OnLeft;
    public Action OnRight;

    /// <summary>Action du bouton Alt, annoncee au joueur par <see cref="AltGuide"/>.</summary>
    public Action OnAlt;
    public string AltGuide;

    /// <summary>Appele quand la ligne prend le focus.</summary>
    public Action OnSelected;

    /// <summary>
    /// Dessine par-dessus la ligne, une fois son texte pose. Sert aux lignes qui ont
    /// besoin de montrer autre chose qu'un libelle - une pastille de couleur, par
    /// exemple, qu'aucun code hexadecimal ne remplace a l'oeil.
    /// </summary>
    public Action OnRendered;

    private readonly string label;
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;
    private readonly Wiggler selectedWiggler;
    private readonly Wiggler changedWiggler;

    public UIMenuRow(Vector2 position, Vector2 tweenFrom, string label) : base(position)
    {
      this.label = label;
      this.tweenFrom = tweenFrom;
      tweenTo = position;

      selectedWiggler = Wiggler.Create(20, 4f, null, null, false, false);
      Add(selectedWiggler);
      changedWiggler = Wiggler.Create(30, 5f, null, null, false, false);
      Add(changedWiggler);
    }

    public override void Update()
    {
      // Un panneau modal - roue chromatique, clavier virtuel - baisse CanAct le temps
      // qu'il est ouvert. Sans ce test, les fleches piloteraient a la fois le panneau
      // et la ligne restee selectionnee dessous. C'est la convention que suivent les
      // widgets de FortRise, UIColorInputFieldButton en tete.
      if (MainMenu != null && !MainMenu.CanAct)
      {
        return;
      }

      base.Update();

      if (!Selected)
      {
        return;
      }

      if (OnAlt != null && MenuInput.Alt)
      {
        OnAlt();
        return;
      }

      // MenuInput.Left et Right portent deja la repetition au maintien : inutile
      // d'en tenir une ici.
      if (OnRight != null && MenuInput.Right)
      {
        OnRight();
        changedWiggler.Start();
        Sounds.ui_move1.Play(160f, 1f);
        return;
      }

      if (OnLeft != null && MenuInput.Left)
      {
        OnLeft();
        changedWiggler.Start();
        Sounds.ui_move1.Play(160f, 1f);
      }
    }

    public override void Render()
    {
      base.Render();

      Color color = Selected ? OptionsButton.SelectedColor : OptionsButton.NotSelectedColor;
      var offset = new Vector2(5f * selectedWiggler.Value, 0f);

      // Les libelles passent par le filtre : un caractere inconnu de la police ferait
      // lever MeasureString en plein rendu, et le jeu tomberait.
      Draw.OutlineTextJustify(TFGame.Font, MenuText.Safe(label), Position + offset, color, Color.Black,
          new Vector2(0f, 0.5f), 1f);

      OnRendered?.Invoke();

      string right = MenuText.Safe(RightText?.Invoke());
      if (string.IsNullOrEmpty(right))
      {
        return;
      }

      // Les chevrons ne sont montres que sur la ligne selectionnee : ils indiquent
      // que la valeur se change sur place, sans encombrer les autres lignes.
      if (Selected && (OnLeft != null || OnRight != null))
      {
        right = "< " + right + " >";
      }

      Draw.OutlineTextJustify(TFGame.Font, right,
          Position + new Vector2(ContentWidth + 2f * changedWiggler.Value, 0f) + offset,
          color * (Selected ? 1f : 0.65f), Color.Black, new Vector2(1f, 0.5f), 1f);
    }

    public override void TweenIn()
    {
      Position = tweenFrom;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 20, true);
      tween.OnUpdate = t => Position = Vector2.Lerp(tweenFrom, tweenTo, t.Eased);
      Add(tween);
    }

    public override void TweenOut()
    {
      Vector2 start = Position;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 12, true);
      tween.OnUpdate = t => Position = Vector2.Lerp(start, tweenFrom, t.Eased);
      Add(tween);
    }

    protected override void OnSelect()
    {
      selectedWiggler.Start();

      if (MainMenu != null)
      {
        // La liste peut depasser l'ecran : on suit la ligne courante plutot que de
        // laisser le joueur descendre dans le vide. TweenUICameraToY borne de
        // lui-meme la cible a MaxUICameraY.
        MainMenu.TweenUICameraToY(Math.Max(0f, Y - 120f), 10);

        if (AltGuide != null)
        {
          MainMenu.ButtonGuideC.SetDetails(MenuButtonGuide.ButtonModes.Alt, AltGuide);
        }
        else
        {
          MainMenu.ButtonGuideC.Clear();
        }
      }

      OnSelected?.Invoke();
    }

    protected override void OnDeselect()
    {
    }

    protected override void OnConfirm()
    {
      if (OnConfirmed == null)
      {
        return;
      }

      changedWiggler.Start();
      Sounds.ui_click.Play(160f, 1f);
      OnConfirmed();
    }
  }
}
