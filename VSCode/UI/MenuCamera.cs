using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Aide au maintien d'un panneau a une place fixe pendant que la liste defile.
  ///
  /// Les MenuItem vivent sur le calque -1, qui est une CameraLayer : quand une liste
  /// depasse la hauteur de l'ecran, MainMenu deplace cette camera et tout ce qui s'y
  /// trouve suit - y compris un apercu ou un en-tete qu'on voulait immobile.
  ///
  /// D'ou le principe retenu ici : l'element garde une ancre, sa position voulue a
  /// l'ecran, et se replace a chaque image a cette ancre plus le decalage de la
  /// camera. Les interpolations d'entree et de sortie ecrivent l'ancre et non la
  /// position, sinon les deux se marcheraient dessus.
  /// </summary>
  internal static class MenuCamera
  {
    public static float OffsetY(MainMenu menu)
    {
      var layer = menu?.UILayer;
      return layer?.Camera == null ? 0f : layer.Camera.Y;
    }

    public static Vector2 Fixed(MainMenu menu, Vector2 anchor)
    {
      return anchor + Vector2.UnitY * OffsetY(menu);
    }
  }
}
