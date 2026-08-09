using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TFModFortRiseProfiles
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

    /// <summary>
    /// Les pixels que la forge gardera : le canevas decoupe a la fenetre.
    ///
    /// Toujours <see cref="ForgeSlots.Frame"/> au carre, quelle que soit la taille des
    /// cases sources. C'est cette taille que les sprites du jeu attendent.
    /// </summary>
    public static Color[] Cut(ForgePose pose)
    {
      if (pose == null)
      {
        return null;
      }

      int size = ForgeSlots.Frame;
      var window = new Color[size * size];

      for (int y = 0; y < size; y++)
      {
        int sourceY = pose.WindowY + y;

        if (sourceY < 0 || sourceY >= pose.Height)
        {
          continue;
        }

        for (int x = 0; x < size; x++)
        {
          int sourceX = pose.WindowX + x;

          if (sourceX < 0 || sourceX >= pose.Width)
          {
            continue;
          }

          Color pixel = pose.Pixels[sourceY * pose.Width + sourceX];

          // Un pixel entierement transparent est laisse a zero plutot que recopie
          // avec sa couleur. C'est invisible en alpha ordinaire, mais les planches
          // exportees deviennent comparables octet a octet a celles du prototype - et
          // une comparaison qui echoue pour une raison invisible est une comparaison
          // qu'on finit par ne plus faire.
          if (pixel.A != 0)
          {
            window[y * size + x] = pixel;
          }
        }
      }

      return window;
    }

    /// <summary>Nombre de calques enregistres, lisibles ou non.</summary>
    public static int Count(ForgeDesign design, string slotKey)
    {
      return design == null ? 0 : design.LayersOf(slotKey).Count;
    }

    // ------------------------------------------------------------------

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
    /// Lit les calques lisibles d'une pose, dans l'ordre du choix.
    ///
    /// Un calque introuvable est saute et non fatal : perdre les bras vaut mieux que
    /// perdre la pose, et le journal dit lequel manque. Chaque calque garde donc une
    /// reference vers le choix dont il vient, seul lien fiable avec l'empilement
    /// enregistre une fois que des rangs ont saute.
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

        Color[] pixels = ForgeBank.ReadCell(source, pick.Cell);

        if (pixels == null)
        {
          continue;
        }

        ForgeNudge placement = design.PlacementOf(pick);

        layers.Add(new Layer
        {
          Pick = pick,
          Pixels = pixels,
          Width = source.CellWidth,
          Height = source.CellHeight,
          X = placement.X,
          Y = placement.Y
        });
      }

      return layers;
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
