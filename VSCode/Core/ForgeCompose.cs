using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Une pose assemblee, avec la fenetre de decoupe reperee dedans.
  ///
  /// Le canevas est plus grand que ce que la forge gardera : il contient tous les
  /// calques, meme ceux qui debordent. C'est voulu, et c'est ce qui permet d'aligner
  /// - un bras hors du cadre doit se voir pour qu'on sache de combien le ramener.
  /// </summary>
  public class ForgePose
  {
    public Color[] Pixels;
    public int Width;
    public int Height;

    /// <summary>Coin de la fenetre DANS ce canevas, et non dans la case source.</summary>
    public int WindowX;
    public int WindowY;

    /// <summary>Calques reellement poses : une planche absente en retire un.</summary>
    public int Drawn;
  }

  /// <summary>
  /// Le cadre d'un archer : la taille d'une image de sa planche, et ou tombe son
  /// point d'ancrage dedans.
  ///
  /// Calcule sur SES poses, et non fixe d'avance. La fenetre orange ne decoupe plus
  /// rien : elle ne sert qu'a reperer l'ancre. Un dessin fait de grandes images
  /// donne donc un grand cadre, et rien n'est rogne.
  /// </summary>
  public readonly struct ForgeFrame
  {
    public readonly int Width;
    public readonly int Height;
    public readonly int OriginX;
    public readonly int OriginY;

    public ForgeFrame(int width, int height, int originX, int originY)
    {
      Width = width;
      Height = height;
      OriginX = originX;
      OriginY = originY;
    }
  }

  /// <summary>
  /// Assemble les calques d'une pose sur un canevas commun.
  ///
  /// Un seul assemblage pour l'ecran et pour la fabrication : celle-ci prend ce
  /// canevas et le decoupe a la fenetre. Les deux ne peuvent donc pas diverger, ce
  /// qui compte plus qu'il n'y parait - un apercu qui ment sur l'alignement fait
  /// regler dans le vide, et on ne s'en apercoit qu'apres l'export.
  ///
  /// Chaque calque est pose a son propre decalage, ce qui remplace l'ancienne fenetre
  /// unique : deux planches qui ne calent pas leur personnage au meme endroit se
  /// rattrapent, la ou une fenetre commune imposait de choisir laquelle serait juste.
  /// </summary>
  public static class ForgeCompose
  {
    /// <summary>
    /// Assemble une pose, ou rend null si aucun calque n'est lisible.
    /// </summary>
    /// <param name="only">
    /// Le seul calque a dessiner, ou null pour tous. Le canevas garde la meme taille
    /// et la fenetre la meme place dans les deux cas : c'est ce qui permet a l'ecran
    /// d'alignement de superposer un calque isole sur l'ensemble attenue sans que rien
    /// ne se decale entre les deux images.
    ///
    /// Le calque lui-meme et non son rang : les calques illisibles sont sautes a la
    /// lecture, et un rang compte sur l'empilement enregistre designerait alors le
    /// voisin - donc on eclairerait la mauvaise image, precisement quand quelque chose
    /// ne va deja pas.
    /// </param>
    public static ForgePose Pose(ForgeDesign design, string slotKey, ForgePick only = null)
    {
      if (design == null)
      {
        return null;
      }

      List<Layer> layers = Read(design, slotKey);

      if (layers.Count == 0)
      {
        return null;
      }

      // La fenetre entre dans la boite englobante meme si aucun calque ne l'atteint :
      // sans cela un calque pousse loin la ferait sortir du canevas, et l'ecran
      // n'aurait plus rien pour montrer ou tombe la decoupe.
      int minX = Math.Min(0, design.WindowX);
      int minY = Math.Min(0, design.WindowY);
      int maxX = Math.Max(design.WindowX + ForgeSlots.Frame, 0);
      int maxY = Math.Max(design.WindowY + ForgeSlots.Frame, 0);

      foreach (Layer layer in layers)
      {
        minX = Math.Min(minX, layer.X);
        minY = Math.Min(minY, layer.Y);
        maxX = Math.Max(maxX, layer.X + layer.Width);
        maxY = Math.Max(maxY, layer.Y + layer.Height);
      }

      var pose = new ForgePose
      {
        Width = maxX - minX,
        Height = maxY - minY,
        WindowX = design.WindowX - minX,
        WindowY = design.WindowY - minY
      };

      pose.Pixels = new Color[pose.Width * pose.Height];

      foreach (Layer layer in layers)
      {
        if (only != null && !ReferenceEquals(layer.Pick, only))
        {
          continue;
        }

        Blit(pose, layer, -minX, -minY);
        pose.Drawn++;
      }

      return pose;
    }

    public static int Count(ForgeDesign design, string slotKey)
    {
      return design == null ? 0 : design.LayersOf(slotKey).Count;
    }

    /// <summary>
    /// Le cadre qu'il faut pour contenir TOUTES les poses d'un dessin sans rien
    /// rogner, ancres alignees.
    ///
    /// On mesure, pour chaque pose, de combien elle deborde de son ancre dans les
    /// quatre directions, et on retient le pire de chaque cote. Le cadre en decoule,
    /// et l'ancre s'y place a la distance maximale trouvee - c'est ce qui garantit
    /// que la pose la plus large ne sort pas, et que toutes restent alignees entre
    /// elles.
    ///
    /// Plancher a la fenetre : un dessin vide ou minuscule garde le format
    /// historique plutot qu'un cadre de quelques pixels.
    /// </summary>
    public static ForgeFrame FrameOf(ForgeDesign design)
    {
      int left = ForgeSlots.AnchorX;
      int right = ForgeSlots.Frame - ForgeSlots.AnchorX;
      int top = ForgeSlots.AnchorY;
      int bottom = ForgeSlots.Frame - ForgeSlots.AnchorY;

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        ForgePose pose = Pose(design, slot.Key);

        if (pose == null)
        {
          continue;
        }

        int anchorX = pose.WindowX + ForgeSlots.AnchorX;
        int anchorY = pose.WindowY + ForgeSlots.AnchorY;

        left = Math.Max(left, anchorX);
        top = Math.Max(top, anchorY);
        right = Math.Max(right, pose.Width - anchorX);
        bottom = Math.Max(bottom, pose.Height - anchorY);
      }

      return new ForgeFrame(left + right, top + bottom, left, top);
    }

    /// <summary>
    /// Les pixels que la forge gardera : la pose entiere, posee dans le cadre par
    /// son ancre.
    ///
    /// Plus aucun rognage. Ce qui depasse la fenetre orange est conserve - c'est le
    /// cadre qui s'est adapte, pas le dessin qui a ete coupe.
    /// </summary>
    public static Color[] Cut(ForgePose pose, ForgeFrame frame)
    {
      if (pose == null)
      {
        return null;
      }

      var image = new Color[frame.Width * frame.Height];

      // Decalage a appliquer pour que l'ancre de la pose tombe sur celle du cadre.
      int shiftX = frame.OriginX - (pose.WindowX + ForgeSlots.AnchorX);
      int shiftY = frame.OriginY - (pose.WindowY + ForgeSlots.AnchorY);

      for (int y = 0; y < pose.Height; y++)
      {
        int targetY = y + shiftY;

        if (targetY < 0 || targetY >= frame.Height)
        {
          continue;
        }

        for (int x = 0; x < pose.Width; x++)
        {
          int targetX = x + shiftX;

          if (targetX < 0 || targetX >= frame.Width)
          {
            continue;
          }

          Color pixel = pose.Pixels[y * pose.Width + x];

          // Un pixel entierement transparent est laisse a zero plutot que recopie
          // avec sa couleur : invisible en alpha ordinaire, mais les planches
          // exportees deviennent comparables octet a octet.
          if (pixel.A != 0)
          {
            image[targetY * frame.Width + targetX] = pixel;
          }
        }
      }

      return image;
    }

    /// <summary>
    /// Lit les calques d'une pose, chacun a la taille REELLE de son image.
    ///
    /// Un calque introuvable est saute et non fatal : perdre les bras vaut mieux
    /// que perdre la pose, et le journal dit lequel manque.
    /// </summary>
    private static List<Layer> Read(ForgeDesign design, string slotKey)
    {
      var layers = new List<Layer>();

      foreach (ForgePick pick in design.LayersOf(slotKey))
      {
        if (pick == null)
        {
          continue;
        }

        ForgeSource source = ForgeBank.Find(pick.Source);

        if (source == null)
        {
          Log.Error($"[Forge] planche {pick.Source} absente du vivier");
          continue;
        }

        Color[] pixels = ForgeBank.ReadCell(source, pick.Cell, out var size);

        if (pixels == null)
        {
          continue;
        }

        ForgeNudge placement = design.PlacementOf(pick);
        float x = placement.X;
        float y = placement.Y;

        if (!pick.Untouched)
        {
          // Le rognage d'abord, la taille ensuite : les valeurs de rognage se
          // comptent sur le fichier, ce qui les rend independantes du facteur - on
          // peut changer l'un sans que l'autre se decale.
          pixels = ForgePixels.Crop(
              pixels, ref size, pick.CropLeft, pick.CropRight, pick.CropTop, pick.CropBottom);

          // Ce qui reste ne bouge pas : sans ce report, retirer une bordure a gauche
          // emmenerait le personnage avec elle.
          x += pick.CropLeft;
          y += pick.CropTop;

          // Le miroir ne deplace rien : l'image se retourne dans son cadre, la ou
          // elle est.
          pixels = ForgePixels.Flip(pixels, size, pick.FlipX, pick.FlipY);

          if (pick.Rotation != 0)
          {
            Point before = size;
            pixels = ForgePixels.Rotate(pixels, ref size, pick.Rotation);

            // La rotation agrandit le cadre autour du centre. On rend au calque la
            // moitie de ce qu'il a gagne de chaque cote, sans quoi l'image tournee
            // partirait vers le bas a droite.
            x -= (size.X - before.X) / 2f;
            y -= (size.Y - before.Y) / 2f;
          }

          if (pick.Scale != 100)
          {
            float k = pick.Scale / 100f;
            pixels = ForgePixels.Scale(pixels, ref size, pick.Scale);

            // On reduit AUTOUR DE L'ANCRE et non du coin de l'image : l'ancre est le
            // point que le jeu pose sur la position du personnage, les pieds au sol.
            // Reduire depuis le coin ferait flotter en l'air tout ce qu'on rapetisse,
            // et il faudrait recaler apres chaque changement de taille.
            float anchorX = design.WindowX + ForgeSlots.AnchorX;
            float anchorY = design.WindowY + ForgeSlots.AnchorY;

            x = anchorX - (anchorX - x) * k;
            y = anchorY - (anchorY - y) * k;
          }
        }

        layers.Add(new Layer
        {
          Pick = pick,
          Pixels = pixels,
          Width = size.X,
          Height = size.Y,

          // Arrondi explicite : Math.Round arrondit au pair, et un demi-pixel qui
          // tombe tantot d'un cote tantot de l'autre fait boiter la course.
          X = (int)Math.Floor(x + 0.5f),
          Y = (int)Math.Floor(y + 0.5f)
        });
      }

      return layers;
    }

    /// <summary>Un calque pose sur le canevas : ses pixels, sa taille, sa place.</summary>
    private class Layer
    {
      public ForgePick Pick;
      public Color[] Pixels;
      public int Width;
      public int Height;
      public int X;
      public int Y;
    }

    /// <summary>
    /// Pose un calque sur le canevas.
    ///
    /// Les sprites sont a bords francs : un pixel est opaque ou il ne l'est pas. On
    /// recouvre donc plutot que de melanger - un melange sur un pixel semi-opaque
    /// donnerait un halo la ou le dessin d'origine n'en a pas.
    /// </summary>
    private static void Blit(ForgePose pose, Layer layer, int shiftX, int shiftY)
    {
      for (int y = 0; y < layer.Height; y++)
      {
        int targetY = layer.Y + shiftY + y;

        if (targetY < 0 || targetY >= pose.Height)
        {
          continue;
        }

        for (int x = 0; x < layer.Width; x++)
        {
          int targetX = layer.X + shiftX + x;

          if (targetX < 0 || targetX >= pose.Width)
          {
            continue;
          }

          Color pixel = layer.Pixels[y * layer.Width + x];

          if (pixel.A != 0)
          {
            pose.Pixels[targetY * pose.Width + targetX] = pixel;
          }
        }
      }
    }
  }
}
