using System;
using Microsoft.Xna.Framework;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Les retouches qu'on fait subir a une image du vivier avant de la poser :
  /// rogner ses bords, changer sa taille.
  ///
  /// Rien n'est ecrit sur le disque. Une retouche est un REGLAGE du dessin, pas une
  /// modification du fichier : elle se defait, elle se change, et la meme image
  /// servie a deux archers peut y etre reglee differemment. C'est le meme choix que
  /// pour la recoloration et les decalages, et pour la meme raison - le vivier est
  /// une matiere premiere qu'on ne doit jamais abimer.
  ///
  /// Tout se fait au plus proche voisin, sans interpolation : un sprite a bords
  /// francs doit le rester. Une reduction mange donc des colonnes entieres, ce qui
  /// se voit et se corrige en cherchant un facteur plus rond - une moitie, un quart -
  /// alors qu'un lissage donnerait une image floue qu'on croirait juste.
  /// </summary>
  public static class ForgePixels
  {
    /// <summary>Retire des pixels sur les quatre bords. La taille rendue suit.</summary>
    public static Color[] Crop(Color[] pixels, ref Point size, int left, int right, int top, int bottom)
    {
      if (pixels == null || size.X <= 0 || size.Y <= 0)
      {
        return pixels;
      }

      left = Math.Max(0, left);
      right = Math.Max(0, right);
      top = Math.Max(0, top);
      bottom = Math.Max(0, bottom);

      if (left + right + top + bottom == 0)
      {
        return pixels;
      }

      // Jamais rien de vide : une image de zero pixel ferait disparaitre la pose, et
      // l'on croirait avoir perdu son choix plutot qu'avoir trop rogne. Un pixel qui
      // reste se voit, et le reglage se defait.
      int width = Math.Max(1, size.X - left - right);
      int height = Math.Max(1, size.Y - top - bottom);

      left = Math.Min(left, size.X - width);
      top = Math.Min(top, size.Y - height);

      var cropped = new Color[width * height];

      for (int y = 0; y < height; y++)
      {
        Array.Copy(pixels, (y + top) * size.X + left, cropped, y * width, width);
      }

      size = new Point(width, height);
      return cropped;
    }

    /// <summary>
    /// Change la taille de l'image, en pourcentage de la sienne.
    ///
    /// L'echantillonnage prend le pixel source au CENTRE de chaque pixel de sortie -
    /// (i + 0.5) / k et non i / k. Sans ce demi-pixel, une reduction retire toujours
    /// les memes bords et decale le personnage d'un pixel vers le haut a gauche ;
    /// avec, il reste centre sur lui-meme.
    /// </summary>
    public static Color[] Scale(Color[] pixels, ref Point size, int percent)
    {
      if (pixels == null || size.X <= 0 || size.Y <= 0 || percent == 100 || percent <= 0)
      {
        return pixels;
      }

      float k = percent / 100f;
      int width = Math.Max(1, (int)(size.X * k + 0.5f));
      int height = Math.Max(1, (int)(size.Y * k + 0.5f));

      var scaled = new Color[width * height];

      for (int y = 0; y < height; y++)
      {
        int sourceY = Math.Min(size.Y - 1, (int)((y + 0.5f) / k));

        for (int x = 0; x < width; x++)
        {
          int sourceX = Math.Min(size.X - 1, (int)((x + 0.5f) / k));
          scaled[y * width + x] = pixels[sourceY * size.X + sourceX];
        }
      }

      size = new Point(width, height);
      return scaled;
    }

    /// <summary>
    /// Retourne l'image dans son propre cadre. La taille ne change pas.
    /// </summary>
    public static Color[] Flip(Color[] pixels, Point size, bool flipX, bool flipY)
    {
      if (pixels == null || size.X <= 0 || size.Y <= 0 || (!flipX && !flipY))
      {
        return pixels;
      }

      var flipped = new Color[size.X * size.Y];

      for (int y = 0; y < size.Y; y++)
      {
        int sourceY = flipY ? size.Y - 1 - y : y;

        for (int x = 0; x < size.X; x++)
        {
          int sourceX = flipX ? size.X - 1 - x : x;
          flipped[y * size.X + x] = pixels[sourceY * size.X + sourceX];
        }
      }

      return flipped;
    }

    /// <summary>
    /// Tourne l'image autour de son centre, dans le sens des aiguilles d'une montre.
    ///
    /// Le cadre s'agrandit de ce qu'il faut pour que rien ne sorte : une image carree
    /// tournee de 45 degres deborde de sa case, et rogner ce debordement reviendrait
    /// a couper les mains du personnage.
    ///
    /// Les quarts de tour sont traites a part et sont EXACTS - une transposition, pas
    /// un echantillonnage. Ce sont les seuls angles ou un dessin au pixel ressort
    /// intact, et ce sont ceux dont on se sert le plus : coucher un cadavre, redresser
    /// une planche rangee de travers.
    /// </summary>
    public static Color[] Rotate(Color[] pixels, ref Point size, int degrees)
    {
      if (pixels == null || size.X <= 0 || size.Y <= 0)
      {
        return pixels;
      }

      int angle = ((degrees % 360) + 360) % 360;

      if (angle == 0)
      {
        return pixels;
      }

      if (angle % 90 == 0)
      {
        return Quarter(pixels, ref size, angle / 90);
      }

      double radians = angle * Math.PI / 180.0;
      double cos = Math.Cos(radians);
      double sin = Math.Sin(radians);

      // Le cadre d'arrivee : la boite qui contient les quatre coins tournes.
      int width = (int)(Math.Abs(size.X * cos) + Math.Abs(size.Y * sin) + 0.5);
      int height = (int)(Math.Abs(size.X * sin) + Math.Abs(size.Y * cos) + 0.5);

      var rotated = new Color[width * height];

      // On parcourt l'ARRIVEE et on remonte a la source : parcourir la source
      // laisserait des trous, deux pixels voisins pouvant tomber sur le meme pixel
      // d'arrivee en laissant celui d'entre eux inoccupe.
      double sourceCenterX = size.X / 2.0;
      double sourceCenterY = size.Y / 2.0;
      double targetCenterX = width / 2.0;
      double targetCenterY = height / 2.0;

      for (int y = 0; y < height; y++)
      {
        double dy = y + 0.5 - targetCenterY;

        for (int x = 0; x < width; x++)
        {
          double dx = x + 0.5 - targetCenterX;

          int sourceX = (int)(sourceCenterX + dx * cos + dy * sin);
          int sourceY = (int)(sourceCenterY - dx * sin + dy * cos);

          if (sourceX < 0 || sourceX >= size.X || sourceY < 0 || sourceY >= size.Y)
          {
            continue;
          }

          rotated[y * width + x] = pixels[sourceY * size.X + sourceX];
        }
      }

      size = new Point(width, height);
      return rotated;
    }

    /// <summary>Un, deux ou trois quarts de tour, sans perte.</summary>
    private static Color[] Quarter(Color[] pixels, ref Point size, int quarters)
    {
      int width = quarters == 2 ? size.X : size.Y;
      int height = quarters == 2 ? size.Y : size.X;

      var turned = new Color[width * height];

      for (int y = 0; y < size.Y; y++)
      {
        for (int x = 0; x < size.X; x++)
        {
          Color pixel = pixels[y * size.X + x];

          int targetX;
          int targetY;

          switch (quarters)
          {
            case 1:
              targetX = size.Y - 1 - y;
              targetY = x;
              break;

            case 2:
              targetX = size.X - 1 - x;
              targetY = size.Y - 1 - y;
              break;

            default:
              targetX = y;
              targetY = size.X - 1 - x;
              break;
          }

          turned[targetY * width + targetX] = pixel;
        }
      }

      size = new Point(width, height);
      return turned;
    }

    /// <summary>
    /// Les marges entierement transparentes d'une image : gauche, droite, haut, bas.
    ///
    /// Ce sont les valeurs de rognage qui detourent le dessin sans en perdre un
    /// pixel. Sert au detourage automatique : une image decoupee dans une grille
    /// porte presque toujours du vide autour d'elle, et le retirer a la main sur
    /// seize poses n'a pas d'interet.
    ///
    /// Rend quatre zeros pour une image entierement transparente : il n'y a rien a
    /// detourer, et rogner jusqu'au dernier pixel ferait disparaitre l'image.
    /// </summary>
    public static (int Left, int Right, int Top, int Bottom) Margins(Color[] pixels, Point size)
    {
      if (pixels == null || size.X <= 0 || size.Y <= 0)
      {
        return (0, 0, 0, 0);
      }

      int minX = size.X;
      int minY = size.Y;
      int maxX = -1;
      int maxY = -1;

      for (int y = 0; y < size.Y; y++)
      {
        for (int x = 0; x < size.X; x++)
        {
          if (pixels[y * size.X + x].A == 0)
          {
            continue;
          }

          if (x < minX) { minX = x; }
          if (x > maxX) { maxX = x; }
          if (y < minY) { minY = y; }
          if (y > maxY) { maxY = y; }
        }
      }

      if (maxX < 0)
      {
        return (0, 0, 0, 0);
      }

      return (minX, size.X - 1 - maxX, minY, size.Y - 1 - maxY);
    }
  }
}
