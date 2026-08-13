using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Les pieces que la forge reprend au jeu et repeint.
  ///
  /// Broforce n'a ni arc, ni viseur, ni ailes, ni gemme : ces objets n'existent que
  /// dans TowerFall. Plutot que de les laisser verts sur un archer brun - ce qui se
  /// verrait immediatement - on prend ceux du jeu et on leur impose la teinte de
  /// l'archer forge. C'est ce que fait deja le mod de l'archer Brones, et c'est la
  /// seule chose raisonnable a faire tant que personne ne dessine.
  ///
  /// Rien n'est modifie dans l'atlas : on en lit les pixels et on fabrique une
  /// texture a part, comme partout ailleurs dans ce mod.
  /// </summary>
  public static class ForgeBorrowed
  {
    // Les regions d'origine. Le vert est pris pour reference parce que sa teinte est
    // la plus franche : deplacer une teinte deja terne donne une couleur terne.
    public const string BowRegion = "player/bow0";
    public const string AimerRegion = "aimers/green";
    public const string GemRegion = "pickups/greenGem";

    /// <summary>
    /// La gemme du rollcall ne vit ni dans le meme atlas ni dans le meme dictionnaire
    /// que celle du jeu : c'est un sprite de menu, lu par ArcherPortrait.InitGem.
    /// </summary>
    public const string MenuGemRegion = "portraits/gem0";

    /// <summary>Piece de l'atlas principal, repeinte, ou null si la region manque.</summary>
    public static ForgeImage FromAtlas(string region, float hue)
    {
      return Repaint(Lookup(TFGame.Atlas, region), region, hue);
    }

    /// <summary>Piece de l'atlas des menus, repeinte, ou null si la region manque.</summary>
    public static ForgeImage FromMenuAtlas(string region, float hue)
    {
      return Repaint(Lookup(TFGame.MenuAtlas, region), region, hue);
    }

    private static Subtexture Lookup(Atlas atlas, string region)
    {
      try
      {
        return atlas == null ? null : atlas[region];
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] region {region} introuvable : {e.Message}");
        return null;
      }
    }

    private static ForgeImage Repaint(Subtexture source, string region, float hue)
    {
      Color[] pixels = ReadPixels(source);

      if (pixels == null)
      {
        Log.Error($"[Forge] region {region} illisible, la piece gardera sa couleur");
        return null;
      }

      var image = new ForgeImage(source.Rect.Width, source.Rect.Height);

      for (int i = 0; i < pixels.Length; i++)
      {
        if (pixels[i].A != 0)
        {
          image.Pixels[i] = ForgeBuild.ShiftHue(pixels[i], hue);
        }
      }

      return image;
    }

    /// <summary>
    /// Pixels d'une region d'atlas.
    ///
    /// La region demandee et la longueur du tampon doivent s'accorder exactement :
    /// c'est ce desaccord-la qui fait ecrire FNA3D hors des clous.
    /// </summary>
    public static Color[] ReadPixels(Subtexture subtexture)
    {
      if (subtexture == null)
      {
        return null;
      }

      try
      {
        Texture2D texture = subtexture.Texture2D;
        Rectangle rect = subtexture.Rect;

        if (texture == null || texture.IsDisposed || rect.Width <= 0 || rect.Height <= 0)
        {
          return null;
        }

        var pixels = new Color[rect.Width * rect.Height];
        texture.GetData(0, rect, pixels, 0, pixels.Length);
        return pixels;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] lecture de l'atlas impossible : {e.Message}");
        return null;
      }
    }
  }
}
