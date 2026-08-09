using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseProfiles
{
  /// <summary>Une planche de pixels, avant qu'elle ne devienne une texture.</summary>
  public sealed class ForgeImage
  {
    public readonly int Width;
    public readonly int Height;
    public readonly Color[] Pixels;

    public ForgeImage(int width, int height)
    {
      Width = width;
      Height = height;
      Pixels = new Color[width * height];
    }

    public Color this[int x, int y]
    {
      get => Pixels[y * Width + x];
      set => Pixels[y * Width + x] = value;
    }

    /// <summary>
    /// Texture autonome calee en (0,0).
    ///
    /// C'est la forme que SwapSubtexture et le registre de FortRise acceptent tous
    /// les deux : une texture independante de l'atlas, dont les rectangles de frames
    /// se deduisent de sa seule taille.
    /// </summary>
    public Subtexture ToSubtexture()
    {
      var texture = new Texture2D(Engine.Instance.GraphicsDevice, Width, Height);
      texture.SetData(Pixels);
      return new Subtexture(new Monocle.Texture(texture));
    }
  }

  /// <summary>Tout ce qu'un archer forge a besoin de porter, en images.</summary>
  public sealed class ForgeArt
  {
    public ForgeImage Body;
    public ForgeImage BodyRed;
    public ForgeImage BodyBlue;

    public ForgeImage Corpse;
    public ForgeImage CorpseRed;
    public ForgeImage CorpseBlue;
    public ForgeImage CorpseFlash;

    /// <summary>Tetes vides. Le personnage porte la sienne dans son corps.</summary>
    public ForgeImage Head;

    public ForgeImage Statue;
    public ForgeImage PortraitJoined;
    public ForgeImage PortraitNotJoined;
    public ForgeImage PortraitWin;
    public ForgeImage PortraitLose;

    /// <summary>Pieces reprises du jeu et repeintes : Broforce n'en a aucune.</summary>
    public ForgeImage Bow;

    public ForgeImage BowRed;
    public ForgeImage BowBlue;
    public ForgeImage Aimer;
    public ForgeImage Gem;
    public ForgeImage MenuGem;

    /// <summary>Le chapeau qui s'envole, ou null si l'archer part tete nue.</summary>
    public ForgeImage Hat;
    public ForgeImage HatRed;
    public ForgeImage HatBlue;

    /// <summary>Emplacements restes vides, qui ont donc pris la pose debout.</summary>
    public List<string> Substituted = new List<string>();
  }

  /// <summary>
  /// L'assemblage : seize images choisies deviennent les planches d'un archer.
  ///
  /// Tout se passe en memoire, en Color[], et rien ne touche l'atlas du jeu. C'est
  /// la meme approche que SpriteRecolor, pour la meme raison : deux joueurs, deux
  /// archers forges et l'atlas partage ne doivent jamais se marcher dessus.
  ///
  /// La geometrie de cette classe a ete verifiee autrement que par lecture : le
  /// prototype script/forge_preview.py fait le meme calcul et ressort, au pixel
  /// pres, les planches livrees avec le mod de l'archer Brones. Toute divergence
  /// entre les deux est un bug de l'un des deux, pas une preference.
  /// </summary>
  public static class ForgeBuild
  {
    // Rouge et bleu des modes par equipes, en degres de teinte.
    private const float RedHue = 0f;
    private const float BlueHue = 220f;

    // En dessous, une couleur n'a pas de teinte a deplacer : ce sont les contours et
    // les yeux, qui doivent rester ce qu'ils sont. Un personnage dont on deplace
    // aussi le noir devient une silhouette coloree sans regard.
    private const int TintMinValue = 40;
    private const int TintMinChroma = 12;

    /// <summary>
    /// Fabrique toutes les planches d'un dessin, ou null s'il n'y a rien a fabriquer.
    /// </summary>
    public static ForgeArt Build(ForgeDesign design)
    {
      if (design == null || !design.Buildable)
      {
        return null;
      }

      try
      {
        var art = new ForgeArt();

        // La pose debout sert de repli : une planche a trous ferait clignoter le
        // personnage, et le vide se remarque moins qu'un archer qui disparait en
        // sautant. Les emplacements ainsi combles sont rapportes, pour que l'ecran
        // puisse les signaler au lieu de les laisser passer pour un choix.
        Color[] fallback = ReadPose(design, "stand");
        if (fallback == null)
        {
          Log.Error("[Forge] la pose debout est illisible, rien a fabriquer");
          return null;
        }

        art.Body = Assemble(design, ForgeSheet.Body, fallback, art.Substituted);
        art.Corpse = Assemble(design, ForgeSheet.Corpse, fallback, art.Substituted);

        art.BodyRed = Tinted(art.Body, RedHue);
        art.BodyBlue = Tinted(art.Body, BlueHue);

        art.CorpseRed = Tinted(art.Corpse, RedHue);
        art.CorpseBlue = Tinted(art.Corpse, BlueHue);
        art.CorpseFlash = Silhouette(art.Corpse);

        // Le jeu exige les treize animations de tete, meme pointant toutes sur du
        // vide. Cinq images de 10x10 suffisent a les porter.
        art.Head = new ForgeImage(50, 10);

        ForgeImage idle = FirstFrame(art.Body);
        art.Statue = Statue(idle);
        art.PortraitJoined = Enlarged(idle, 60, 120, 4);
        art.PortraitNotJoined = Enlarged(idle, 60, 120, 4);
        art.PortraitWin = Enlarged(idle, 50, 50, 2);
        art.PortraitLose = Enlarged(idle, 50, 50, 2);

        BuildHat(art, design);

        Borrow(art, design);
        return art;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] assemblage de {design.Name} impossible : {e}");
        return null;
      }
    }

    /// <summary>
    /// La seule planche du corps, sans rien en deriver.
    ///
    /// L'apercu des menus n'a besoin que d'elle, et il la redemande a chaque ligne
    /// survolee. Passer par Build() lui ferait calculer, pour rien et cinquante fois
    /// par seconde de defilement, quatre planches teintees pixel par pixel, une
    /// silhouette, quatre portraits et cinq pieces lues dans l'atlas.
    /// </summary>
    public static ForgeImage BuildBody(ForgeDesign design, List<string> substituted = null)
    {
      if (design == null || design.PickOf("stand") == null)
      {
        return null;
      }

      try
      {
        Color[] fallback = ReadPose(design, "stand");
        return fallback == null
            ? null
            : Assemble(design, ForgeSheet.Body, fallback, substituted ?? new List<string>());
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] apercu du corps impossible : {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Les pieces que le jeu prete, repeintes a la teinte du dessin.
    ///
    /// Les variantes d'equipe de l'arc partent de la piece d'origine et non de celle
    /// deja repeinte : deux deplacements de teinte a la suite n'arriveraient jamais
    /// au rouge et au bleu attendus, ils arriveraient a une couleur qui depend de
    /// l'ordre dans lequel on les a faits.
    /// </summary>
    /// <summary>
    /// Le chapeau qui s'envole : trois images independantes, une par camp.
    ///
    /// Elles sont RECADREES AU DESSIN et non laissees a la taille de la fenetre.
    /// C'est indispensable : DefaultHat fait <c>image.CenterOrigin()</c>, le chapeau
    /// pivote donc autour du centre de sa propre texture. Une image de 24x24 presque
    /// vide le ferait tourner autour du vide et voler de travers. Les chapeaux du jeu
    /// font huit pixels sur quatre.
    ///
    /// Sans image normale, il n'y a pas de chapeau du tout et l'archer part tete nue.
    /// Les variantes d'equipe manquantes sont deduites par teinte, comme le reste.
    /// </summary>
    private static void BuildHat(ForgeArt art, ForgeDesign design)
    {
      art.Hat = Trimmed(Cutout(design, "hat_normal"));

      if (art.Hat == null)
      {
        return;
      }

      art.HatRed = Trimmed(Cutout(design, "hat_red")) ?? Tinted(art.Hat, RedHue);
      art.HatBlue = Trimmed(Cutout(design, "hat_blue")) ?? Tinted(art.Hat, BlueHue);
    }

    /// <summary>Image d'un emplacement, a la taille de la fenetre, ou null.</summary>
    private static ForgeImage Cutout(ForgeDesign design, string slotKey)
    {
      Color[] pixels = ReadPose(design, slotKey);

      if (pixels == null)
      {
        return null;
      }

      var image = new ForgeImage(ForgeSlots.Frame, ForgeSlots.Frame);
      Array.Copy(pixels, image.Pixels, Math.Min(pixels.Length, image.Pixels.Length));
      return image;
    }

    /// <summary>
    /// Recadre une image sur ses pixels opaques. Rend null si elle est entierement
    /// transparente - une case vide du vivier n'est pas un chapeau.
    /// </summary>
    private static ForgeImage Trimmed(ForgeImage image)
    {
      if (image == null)
      {
        return null;
      }

      int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;

      for (int y = 0; y < image.Height; y++)
      {
        for (int x = 0; x < image.Width; x++)
        {
          if (image.Pixels[y * image.Width + x].A == 0)
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
        return null;
      }

      var trimmed = new ForgeImage(maxX - minX + 1, maxY - minY + 1);

      for (int y = 0; y < trimmed.Height; y++)
      {
        for (int x = 0; x < trimmed.Width; x++)
        {
          trimmed.Pixels[y * trimmed.Width + x] =
              image.Pixels[(y + minY) * image.Width + x + minX];
        }
      }

      return trimmed;
    }

    private static void Borrow(ForgeArt art, ForgeDesign design)
    {
      art.Bow = ForgeBorrowed.FromAtlas(ForgeBorrowed.BowRegion, design.Hue);
      art.BowRed = ForgeBorrowed.FromAtlas(ForgeBorrowed.BowRegion, RedHue);
      art.BowBlue = ForgeBorrowed.FromAtlas(ForgeBorrowed.BowRegion, BlueHue);
      art.Aimer = ForgeBorrowed.FromAtlas(ForgeBorrowed.AimerRegion, design.Hue);
      art.Gem = ForgeBorrowed.FromAtlas(ForgeBorrowed.GemRegion, design.Hue);
      art.MenuGem = ForgeBorrowed.FromMenuAtlas(ForgeBorrowed.MenuGemRegion, design.Hue);
    }

    // ------------------------------------------------------------------
    // Decoupe et accolement
    // ------------------------------------------------------------------

    /// <summary>
    /// Les pixels d'une pose, deja recadres a la fenetre, ou null.
    ///
    /// Broforce dessine les bras sur une planche a part - ils s'animent
    /// independamment du corps - et une pose faite du seul corps sort manchote. Le
    /// premier calque est le fond, chaque suivant se pose par-dessus, chacun a son
    /// propre decalage.
    ///
    /// L'assemblage est celui de <see cref="ForgeCompose"/>, qui sert aussi aux
    /// ecrans : la fabrication ne fait qu'y decouper la fenetre. Deux assemblages
    /// separes finiraient par diverger d'un pixel, et c'est l'apercu qu'on croirait.
    /// </summary>
    private static Color[] ReadPose(ForgeDesign design, string slotKey)
    {
      return ForgeCompose.Cut(ForgeCompose.Pose(design, slotKey));
    }

    private static ForgeImage Assemble(
        ForgeDesign design, ForgeSheet sheet, Color[] fallback, List<string> substituted)
    {
      ForgeSlot[] slots = ForgeSlots.Of(sheet);
      int size = ForgeSlots.Frame;
      var strip = new ForgeImage(slots.Length * size, size);

      foreach (ForgeSlot slot in slots)
      {
        Color[] pose = ReadPose(design, slot.Key);

        if (pose == null)
        {
          pose = fallback;
          substituted.Add(slot.Key);
        }

        int left = slot.Index * size;

        for (int y = 0; y < size; y++)
        {
          Array.Copy(pose, y * size, strip.Pixels, y * strip.Width + left, size);
        }
      }

      return strip;
    }

    private static ForgeImage FirstFrame(ForgeImage strip)
    {
      int size = ForgeSlots.Frame;
      var frame = new ForgeImage(size, size);

      for (int y = 0; y < size; y++)
      {
        Array.Copy(strip.Pixels, y * strip.Width, frame.Pixels, y * size, size);
      }

      return frame;
    }

    // ------------------------------------------------------------------
    // Derives
    // ------------------------------------------------------------------

    /// <summary>
    /// Deplace la teinte en gardant saturation et valeur.
    ///
    /// Deplacer plutot que poser un aplat : une equipe se reconnait a sa couleur,
    /// mais le personnage doit garder ses ombres et ses contours, sans quoi les deux
    /// camps deviennent deux taches.
    /// </summary>
    private static ForgeImage Tinted(ForgeImage source, float hue)
    {
      var result = new ForgeImage(source.Width, source.Height);

      for (int i = 0; i < source.Pixels.Length; i++)
      {
        Color pixel = source.Pixels[i];

        if (pixel.A == 0)
        {
          continue;
        }

        result.Pixels[i] = ShiftHue(pixel, hue);
      }

      return result;
    }

    public static Color ShiftHue(Color pixel, float hue)
    {
      int high = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
      int low = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));

      if (high < TintMinValue || high - low < TintMinChroma)
      {
        return pixel;
      }

      float value = high / 255f;
      float saturation = high == 0 ? 0f : (high - low) / (float)high;

      float sector = hue / 60f % 6f;
      if (sector < 0f)
      {
        sector += 6f;
      }

      int index = (int)sector;
      float fraction = sector - index;

      float p = value * (1f - saturation);
      float q = value * (1f - fraction * saturation);
      float t = value * (1f - (1f - fraction) * saturation);

      float r, g, b;

      switch (index)
      {
        case 0: r = value; g = t; b = p; break;
        case 1: r = q; g = value; b = p; break;
        case 2: r = p; g = value; b = t; break;
        case 3: r = p; g = q; b = value; break;
        case 4: r = t; g = p; b = value; break;
        default: r = value; g = p; b = q; break;
      }

      // Arrondi et non troncature. L'ecart parait negligeable et ne l'est pas : sur
      // un canal calcule a 32.9995, tronquer donne 32 et arrondir 33, et cet ecart
      // d'un point se produit sur des centaines de pixels d'une planche - assez pour
      // qu'une comparaison au pixel pres avec le prototype echoue partout, et donc
      // pour qu'on cesse de la faire.
      //
      // L'alpha d'origine est conserve : les bords adoucis du pixel art le portent,
      // et le remplacer par 255 ferait apparaitre un lisere dur.
      return new Color(
          (int)Math.Round(r * 255f),
          (int)Math.Round(g * 255f),
          (int)Math.Round(b * 255f),
          pixel.A);
    }

    /// <summary>Silhouette blanche, superposee par le jeu au moment du coup.</summary>
    private static ForgeImage Silhouette(ForgeImage source)
    {
      var result = new ForgeImage(source.Width, source.Height);

      for (int i = 0; i < source.Pixels.Length; i++)
      {
        byte alpha = source.Pixels[i].A;

        if (alpha != 0)
        {
          result.Pixels[i] = new Color(255, 255, 255, alpha);
        }
      }

      return result;
    }

    /// <summary>
    /// Statue de 20x20, recadree et non reduite.
    ///
    /// Le script de l'archer Brones y arrivait par une reduction a 0.83, qui est le
    /// seul endroit de tout l'assemblage ou des pixels se melangent : a cette echelle
    /// le plus proche voisin mange une colonne sur six, au hasard de l'arrondi. Un
    /// recadrage donne le meme cadre sans toucher un seul pixel - le personnage
    /// debout tient dans x 5..18 sur toutes les planches essayees, il reste donc
    /// entier. S'il n'y tient pas un jour, on le rogne : une statue legerement coupee
    /// se remarque moins qu'une statue floue.
    /// </summary>
    private static ForgeImage Statue(ForgeImage idle)
    {
      var statue = new ForgeImage(20, 20);
      int left = Math.Max(0, (idle.Width - 20) / 2);
      int top = Math.Max(0, idle.Height - 20);

      for (int y = 0; y < 20 && top + y < idle.Height; y++)
      {
        for (int x = 0; x < 20 && left + x < idle.Width; x++)
        {
          statue[x, y] = idle[left + x, top + y];
        }
      }

      return statue;
    }

    /// <summary>
    /// Agrandissement au plus proche voisin, cale en bas et centre.
    ///
    /// Les portraits sont des agrandissements de la pose debout parce qu'il n'y a
    /// rien de mieux a faire : les archers d'origine ont des bustes dessines a la
    /// main, qu'on ne peut pas deduire d'un sprite de treize pixels. Ce qui deborde
    /// du cadre est rogne, le personnage etant centre.
    /// </summary>
    private static ForgeImage Enlarged(ForgeImage idle, int width, int height, int scale)
    {
      var result = new ForgeImage(width, height);

      int drawnWidth = idle.Width * scale;
      int drawnHeight = idle.Height * scale;
      int offsetX = (width - drawnWidth) / 2;
      int offsetY = height - drawnHeight;

      for (int y = 0; y < drawnHeight; y++)
      {
        int targetY = offsetY + y;
        if (targetY < 0 || targetY >= height)
        {
          continue;
        }

        for (int x = 0; x < drawnWidth; x++)
        {
          int targetX = offsetX + x;
          if (targetX < 0 || targetX >= width)
          {
            continue;
          }

          Color pixel = idle[x / scale, y / scale];
          if (pixel.A != 0)
          {
            result[targetX, targetY] = pixel;
          }
        }
      }

      return result;
    }
  }
}
