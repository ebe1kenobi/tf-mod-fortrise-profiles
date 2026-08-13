using System;
using FortRise;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  internal static class Dialogs
  {
    /// <summary>
    /// Pose une question oui/non par-dessus le menu.
    ///
    /// UIModal lit MenuInput pour son propre compte, mais les MenuItem du menu le
    /// lisent aussi : sans precaution, la meme pression validerait la modale et
    /// actionnerait la ligne qui se trouve dessous. On reprend donc ce que fait
    /// FortRise pour sa modale de confirmation des options : neutraliser le menu
    /// (CanAct) et deselectionner la ligne courante le temps de la question.
    /// </summary>
    public static void Confirm(MainMenu menu, string title, string message, Action onYes)
    {
      if (menu == null)
      {
        return;
      }

      MenuItem selected = SelectedItem(menu);
      menu.CanAct = false;
      if (selected != null)
      {
        selected.Selected = false;
      }

      void Restore()
      {
        Reactivate(menu);

        // La ligne a pu disparaitre entre-temps : c'est precisement le cas quand la
        // reponse etait oui et qu'elle portait sur sa propre suppression.
        if (selected != null && selected.Scene != null)
        {
          selected.Selected = true;
        }
      }

      var modal = new UIModal(0);
      modal.SetTitle(title);
      modal.AddFiller(message);
      modal.AddItem("YES", () =>
      {
        Restore();
        onYes();
      });
      modal.AddItem("NO", Restore);
      modal.SetOnBackCallBack(Restore);

      menu.Add(modal);
    }

    /// <summary>
    /// Rend la main au menu, mais pas avant l'image suivante.
    ///
    /// C'est la seule facon de fermer une modale par le bouton retour sans quitter
    /// l'ecran du meme coup. UIModal appelle son rappel de retour depuis son Update,
    /// donc pendant le base.Update() de MainMenu ; or MainMenu lit MenuInput.Back a
    /// son tour juste apres, dans la MEME image, et rien ne consomme la pression.
    /// Relever CanAct sur-le-champ, c'est lui rendre un bouton retour deja enfonce -
    /// on refermait la question ET on remontait d'un ecran.
    ///
    /// Un cran d'attente suffit : Back est un "vient d'etre presse", il est retombe
    /// a l'image suivante.
    /// </summary>
    public static void Reactivate(MainMenu menu)
    {
      if (menu == null)
      {
        return;
      }

      menu.Add(new NextFrame(() => menu.CanAct = true));
    }

    private static MenuItem SelectedItem(MainMenu menu)
    {
      if (!menu.Layers.TryGetValue(-1, out Layer layer) || layer == null)
      {
        return null;
      }

      foreach (Entity entity in layer.Entities)
      {
        if (entity is MenuItem item && item.Selected)
        {
          return item;
        }
      }

      return null;
    }

    /// <summary>
    /// Une action a jouer a la prochaine image, puis a oublier.
    ///
    /// Une entite plutot qu'une alarme : Alarm s'accroche a une entite deja presente,
    /// et celle qu'on aurait sous la main ici est justement la modale qu'on est en
    /// train de retirer.
    /// </summary>
    private sealed class NextFrame : Entity
    {
      private readonly Action action;

      public NextFrame(Action action) : base(0)
      {
        this.action = action;
        Depth = -100000;
      }

      public override void Update()
      {
        base.Update();
        action();
        RemoveSelf();
      }
    }
  }
}
