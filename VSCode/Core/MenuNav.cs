using System.Collections.Generic;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le fil d'Ariane des ecrans du mod : par ou l'on est passe pour arriver ici.
  ///
  /// MainMenu n'a qu'un seul <c>BackState</c>, que chaque ecran renseigne dans son
  /// Create(). Tant qu'un ecran n'a qu'un parent, l'ecrire en dur suffit. Des qu'il
  /// en a deux - l'ecran des reglages d'ensemble s'ouvre depuis les deux ecrans de
  /// couleur, le choix d'image depuis les poses comme depuis les calques - la valeur
  /// en dur est juste une fois sur deux, et le retour saute un cran ou deux.
  ///
  /// D'ou cette pile. Un ecran ne declare plus son parent : il declare comment on y
  /// entre (<see cref="Push"/> pour descendre, <see cref="Switch"/> pour une bascule
  /// laterale) et demande son parent a l'arrivee (<see cref="Arrive"/>).
  ///
  /// Le depilage ne retire pas UN cran mais tout ce qui se trouve au-dessus de
  /// l'ecran ou l'on arrive. C'est ce qui rattrape les sorties de secours - un ecran
  /// dont la fiche a disparu se renvoie a la liste, trois crans plus haut - sans
  /// qu'elles aient a se declarer.
  /// </summary>
  internal static class MenuNav
  {
    /// <summary>
    /// Borne de securite. Une pile qui grossit sans fin trahirait un aller-retour
    /// qu'on n'a pas prevu ; mieux vaut perdre le plus ancien cran que la memoire.
    /// </summary>
    private const int MaxDepth = 32;

    private static readonly List<MainMenu.MenuState> trail = new List<MainMenu.MenuState>();

    /// <summary>
    /// La ligne ou l'on etait, par ecran. Sans elle, revenir d'un sous-ecran repose
    /// le curseur en haut de la liste, et il faut redescendre jusqu'au profil qu'on
    /// vient de quitter - a chaque aller-retour.
    ///
    /// Un rang et non la ligne elle-meme : les MenuItem sont detruits en quittant
    /// l'ecran et refabriques en y revenant, il ne reste rien a retenir d'autre.
    /// </summary>
    private static readonly Dictionary<MainMenu.MenuState, int> selected =
        new Dictionary<MainMenu.MenuState, int>();

    /// <summary>Les lignes de l'ecran courant, dans l'ordre ou elles s'affichent.</summary>
    private static List<MenuItem> tracked;

    private static MainMenu.MenuState trackedState;

    /// <summary>
    /// Entree dans les ecrans du mod depuis un ecran du JEU - la lame ARCHER du menu
    /// principal. La pile repart de zero : ce qui s'y trouvait appartient a une
    /// visite precedente, dont les ecrans ont ete detruits depuis.
    /// </summary>
    public static void Open(MainMenu main, MainMenu.MenuState target)
    {
      trail.Clear();
      selected.Clear();
      Push(main, target);
    }

    /// <summary>Descend d'un cran : l'ecran courant devient le parent du suivant.</summary>
    public static void Push(MainMenu main, MainMenu.MenuState target)
    {
      if (main == null)
      {
        return;
      }

      Remember(main);
      trail.Add(main.State);

      if (trail.Count > MaxDepth)
      {
        trail.RemoveAt(0);
      }

      main.State = target;
    }

    /// <summary>
    /// Bascule laterale, ou remontee vers un ecran deja visite : rien n'est empile.
    ///
    /// Les deux ecrans de couleur se remplacent l'un l'autre sans s'emboiter -
    /// osciller entre eux ne doit pas creuser le retour - et "SAVE" ramene a la liste
    /// sans passer par le bouton retour. Dans les deux cas <see cref="Arrive"/> remet
    /// la pile a la bonne profondeur toute seule.
    /// </summary>
    public static void Switch(MainMenu main, MainMenu.MenuState target)
    {
      if (main != null)
      {
        Remember(main);
        main.State = target;
      }
    }

    /// <summary>
    /// Confie a la pile les lignes de l'ecran, dans l'ordre affiche. A appeler a la
    /// fin de chaque construction, avant de poser <c>Main.ToStartSelected</c>.
    ///
    /// Sans cela la pile saurait d'ou l'on vient, mais pas OU l'on en etait.
    /// </summary>
    public static void Track(MainMenu main, IEnumerable<MenuItem> items)
    {
      if (main == null)
      {
        return;
      }

      trackedState = main.State;
      tracked = new List<MenuItem>(items);
    }

    /// <summary>
    /// Le rang a reselectionner sur l'ecran qui s'ouvre, borne a <paramref name="count"/>.
    /// Zero quand on y entre pour la premiere fois.
    /// </summary>
    public static int Resume(MainMenu main, int count)
    {
      if (main == null || count <= 0 || !selected.TryGetValue(main.State, out int index))
      {
        return 0;
      }

      return index < 0 ? 0 : (index >= count ? count - 1 : index);
    }

    /// <summary>
    /// Retient ou en est le curseur avant de quitter l'ecran.
    ///
    /// La ligne selectionnee se cherche dans la scene et non dans une variable : les
    /// lignes changent de selection toutes seules, au clavier comme a la manette, et
    /// aucun de nos codes n'est prevenu.
    /// </summary>
    private static void Remember(MainMenu main)
    {
      if (tracked == null || trackedState != main.State)
      {
        return;
      }

      // -1 est le calque des MenuItem : celui que MenuItem impose a ses sous-classes.
      if (!main.Layers.TryGetValue(-1, out Layer layer) || layer == null)
      {
        return;
      }

      foreach (Entity entity in layer.Entities)
      {
        if (entity is MenuItem item && item.Selected)
        {
          int index = tracked.IndexOf(item);

          if (index >= 0)
          {
            selected[main.State] = index;
          }

          return;
        }
      }
    }

    /// <summary>
    /// Le parent de l'ecran qui vient de s'ouvrir, a poser dans son
    /// <c>Main.BackState</c>. A appeler au debut de chaque Create().
    /// </summary>
    /// <param name="fallback">
    /// Ou aller quand la pile est vide - l'ecran a ete atteint autrement que par nous.
    /// </param>
    public static MainMenu.MenuState Arrive(MainMenu main, MainMenu.MenuState fallback)
    {
      if (main == null)
      {
        return fallback;
      }

      // Arriver sur un ecran qui est DEJA dans la pile, c'est y revenir : tout ce qui
      // a ete ouvert depuis est referme, y compris quand on remonte de plusieurs
      // crans d'un coup.
      int at = trail.LastIndexOf(main.State);

      if (at >= 0)
      {
        trail.RemoveRange(at, trail.Count - at);
      }

      return trail.Count > 0 ? trail[trail.Count - 1] : fallback;
    }
  }
}
