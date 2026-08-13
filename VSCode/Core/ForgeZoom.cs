using Microsoft.Xna.Framework.Input;
using Monocle;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Le facteur d'agrandissement des apercus de la forge.
  ///
  /// Vaut 1 par defaut, c'est-a-dire la TAILLE REELLE du sprite tel qu'il sera en
  /// jeu. C'est le point : un apercu agrandi flatte toujours, et on ne decouvre
  /// qu'apres l'export que le personnage fait deux fois la taille d'un archer. Le
  /// juger a l'echelle evite d'avoir a lancer une partie pour s'en rendre compte.
  ///
  /// La gachette gauche fait defiler les agrandissements pour examiner un detail,
  /// et l'etat est partage par tous les ecrans : on regle une fois, pas a chaque
  /// panneau.
  /// </summary>
  public static class ForgeZoom
  {
    private static readonly float[] Steps = [1f, 2f, 4f, 8f];

    private static int step;

    public static float Factor => Steps[step];

    public static void Cycle()
    {
      step = (step + 1) % Steps.Length;
    }

    /// <summary>
    /// La gachette gauche vient-elle d'etre pressee, sur n'importe quelle manette ?
    ///
    /// N'importe laquelle, et non celle d'un joueur donne : les ecrans de la forge
    /// se pilotent a une seule manette, celle qu'on a en main, et rien n'y designe
    /// un numero de joueur.
    /// </summary>
    public static bool PressedToggle()
    {
      for (int i = 0; i < MInput.XGamepads.Count; i++)
      {
        MInput.XGamepadData pad = MInput.XGamepads[i];

        if (pad != null && (pad.LeftTriggerPressed(0.5f) || pad.Pressed(Buttons.LeftShoulder)))
        {
          return true;
        }
      }

      // Au clavier, la meme fonction sur une touche libre : la forge doit rester
      // utilisable sans manette.
      return MInput.Keyboard.Pressed(Keys.Z);
    }
  }
}
