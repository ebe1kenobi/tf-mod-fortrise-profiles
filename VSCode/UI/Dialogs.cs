using System;
using FortRise;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
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
        menu.CanAct = true;

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
  }
}
