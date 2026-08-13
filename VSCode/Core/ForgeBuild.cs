using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseArcher
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
    /// <summary>
    /// La taille d'une image de la planche, et l'ancre dedans.
    ///
    /// Portee jusqu'a l'enregistrement : c'est elle que le jeu doit recevoir, et
    /// non une constante. Une planche faite d'images de 40 declaree en 24 serait
    /// decoupee n'importe ou.
    /// </summary>
    public ForgeFrame Frame;

    public ForgeImage Body;
    public ForgeImage BodyRed;
    public ForgeImage BodyBlue;

    public ForgeImage Corpse;
    public ForgeImage CorpseRed;
    public ForgeImage CorpseBlue;
    public ForgeImage CorpseFlash;

    /// <summary>
    /// Les trois etats de tete que le jeu attend : nue, coiffee, couronnee.
    ///
    /// Vides quand le personnage porte sa tete dans son corps - le jeu exige les trois
    /// planches meme alors, et les treize animations avec.
    /// </summary>
    public ForgeImage HeadNoHat;

    public ForgeImage HeadNormal;
    public ForgeImage HeadCrown;

    /// <summary>
    /// Les memes, aux couleurs des equipes.
    ///
    /// Deduites par deplacement de teinte comme le corps, et pour la meme raison :
    /// une tete qui ne suivrait pas donnerait un archer vert sur un corps rouge. Le
    /// deplacement est le meme que celui du corps, donc les deux restent d'accord.
    /// </summary>
    public ForgeImage HeadNoHatRed;

    public ForgeImage HeadNoHatBlue;
    public ForgeImage HeadNormalRed;
    public ForgeImage HeadNormalBlue;
    public ForgeImage HeadCrownRed;
    public ForgeImage HeadCrownBlue;

    /// <summary>Vrai si l'un des trois etats porte quelque chose, donc si la tete s'affiche.</summary>
    public bool HasHead;

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
      // Le cadre est mesure UNE fois sur toutes les poses : chaque planche assemblee
      // doit avoir des images de taille identique, et l'ancre au meme endroit dans
      // chacune. Le recalculer par pose ferait sautiller le personnage.
      ForgeFrame frame = ForgeCompose.FrameOf(design);
      if (design == null || !design.Buildable)
      {
        return null;
      }

      try
      {
        var art = new ForgeArt();
        art.Frame = frame;

        // La pose debout sert de repli : une planche a trous ferait clignoter le
        // personnage, et le vide se remarque moins qu'un archer qui disparait en
        // sautant. Les emplacements ainsi combles sont rapportes, pour que l'ecran
        // puisse les signaler au lieu de les laisser passer pour un choix.
        Color[] fallback = ReadPose(design, "stand", frame);
        if (fallback == null)
        {
          Log.Error("[Forge] la pose debout est illisible, rien a fabriquer");
          return null;
        }

        // Les tetes d'abord : c'est leur presence qui decide si la tete est montree
        // pendant la glissade, donc de la facon dont les images de glissade du corps
        // se deduisent.
        //
        // Une planche Broforce dessine deja la tete dans le corps : lui en poser une
        // seconde par-dessus donnerait deux tetes. D'autres sources la separent, et
        // c'est pour celles-la que ces emplacements existent. Sans image, on garde les
        // planches vides d'avant - le jeu exige les treize animations meme pointant
        // toutes sur du vide.
        art.HeadNoHat = HeadStrip(design, ForgeSheet.Head, frame, art);
        art.HeadNormal = HeadStrip(design, ForgeSheet.HeadNormal, frame, art);
        art.HeadCrown = HeadStrip(design, ForgeSheet.HeadCrown, frame, art);

        art.HeadNoHatRed = Tinted(art.HeadNoHat, RedHue);
        art.HeadNoHatBlue = Tinted(art.HeadNoHat, BlueHue);
        art.HeadNormalRed = Tinted(art.HeadNormal, RedHue);
        art.HeadNormalBlue = Tinted(art.HeadNormal, BlueHue);
        art.HeadCrownRed = Tinted(art.HeadCrown, RedHue);
        art.HeadCrownBlue = Tinted(art.HeadCrown, BlueHue);

        bool slideHead = design.SlideHead ?? art.HasHead;

        art.Body = Assemble(design, ForgeSheet.Body, fallback, art.Substituted, frame, slideHead);
        art.Corpse = Assemble(design, ForgeSheet.Corpse, fallback, art.Substituted, frame, slideHead);

        art.BodyRed = Tinted(art.Body, RedHue);
        art.BodyBlue = Tinted(art.Body, BlueHue);

        art.CorpseRed = Tinted(art.Corpse, RedHue);
        art.CorpseBlue = Tinted(art.Corpse, BlueHue);
        art.CorpseFlash = Silhouette(art.Corpse);

        ForgeImage idle = FirstFrame(art.Body, frame);
        art.Statue = Statue(idle);
        art.PortraitJoined = Enlarged(idle, 60, 120, 4);
        art.PortraitNotJoined = Enlarged(idle, 60, 120, 4);
        art.PortraitWin = Enlarged(idle, 50, 50, 2);
        art.PortraitLose = Enlarged(idle, 50, 50, 2);

        BuildHat(art, design, frame);

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
      ForgeFrame frame = ForgeCompose.FrameOf(design);
      if (design == null || design.PickOf("stand") == null)
      {
        return null;
      }

      try
      {
        Color[] fallback = ReadPose(design, "stand", frame);

        if (fallback == null)
        {
          return null;
        }

        ForgeImage strip = Assemble(
            design, ForgeSheet.Body, fallback, substituted ?? new List<string>(), frame,
            design.SlideHead ?? design.HasHeadArt);

        OverlayHead(strip, design, frame);
        return strip;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] apercu du corps impossible : {e.Message}");
        return null;
      }
    }

    /// <summary>
    /// Une planche de tete : celle qu'on a choisie, ou celle qu'on en deduit.
    ///
    /// Le jeu veut trois planches - nue, coiffee, couronnee - et un dessin n'en change
    /// le plus souvent que le sommet. Les redemander en entier ferait quinze images a
    /// choisir pour dix qui seraient les memes ; on pose donc l'ornement sur la tete
    /// nue, et les emplacements des deux etats ne servent qu'a reprendre la main.
    ///
    /// Sans tete nue mais avec un ornement, l'etat vaut l'ornement SEUL. Ce n'est pas
    /// un cas limite, c'est le cas courant : un personnage qui porte sa tete dans son
    /// corps ne peut pas perdre son chapeau, puisqu'il est dessine avec. Le sortir en
    /// sprite de tete lui rend le seul effet de jeu qui lui manquait.
    /// </summary>
    private static ForgeImage HeadStrip(
        ForgeDesign design, ForgeSheet sheet, ForgeFrame frame, ForgeArt art)
    {
      ForgeSlot[] slots = ForgeSlots.Of(sheet);
      var strip = new ForgeImage(slots.Length * frame.Width, frame.Height);

      Dictionary<uint, Color> swaps =
          ColorPalette.SwapMap(design.Colors, ForgeColorSubject.GroupOf(sheet));

      string ornament = ForgeSlots.OrnamentOf(sheet);

      foreach (ForgeSlot slot in slots)
      {
        Color[] pose = HeadPose(design, slot, ornament, frame);

        if (pose == null)
        {
          continue;
        }

        pose = (Color[])pose.Clone();
        ColorPalette.Apply(pose, swaps, design.Colors);

        art.HasHead = true;
        int left = slot.Index * frame.Width;

        for (int y = 0; y < frame.Height; y++)
        {
          Array.Copy(pose, y * frame.Width, strip.Pixels, y * strip.Width + left, frame.Width);
        }
      }

      return strip;
    }

    /// <summary>
    /// Une image de tete : celle qu'on a choisie pour cet etat, ou la tete nue avec
    /// son ornement pose dessus.
    /// </summary>
    private static Color[] HeadPose(
        ForgeDesign design, ForgeSlot slot, string ornament, ForgeFrame frame)
    {
      Color[] chosen = ReadPose(design, slot.Key, frame);

      if (chosen != null)
      {
        return chosen;
      }

      ForgeSlot bare = ForgeSlots.BaseHeadOf(slot) ?? slot;

      return Merge(
          ReadPose(design, bare.Key, frame),
          ornament == null ? null : ReadPose(design, ornament, frame));
    }

    /// <summary>
    /// Les images de glissade coiffee et couronnee, quand elles ne sont pas choisies.
    ///
    /// Elles ne different de la glissade nue que si la tete est CACHEE a ce
    /// moment-la : c'est alors le corps qui doit porter le couvre-chef, et c'est la
    /// seule raison pour laquelle le jeu prevoit trois images. Quand la tete reste
    /// affichee, elle porte deja l'ornement, et le poser une seconde fois sur le corps
    /// en ferait deux.
    /// </summary>
    private static Color[] Slide(
        ForgeDesign design, ForgeSlot slot, ForgeFrame frame, bool slideHead)
    {
      if (slot.Key != "slide_normal" && slot.Key != "slide_crown")
      {
        return null;
      }

      Color[] bare = ReadPose(design, "slide", frame);

      if (bare == null || slideHead)
      {
        return bare;
      }

      return Merge(bare, ReadPose(design, slot.Key == "slide_crown" ? "crown" : "hat_normal", frame));
    }

    /// <summary>Pose la seconde image sur la premiere. Null si les deux manquent.</summary>
    private static Color[] Merge(Color[] under, Color[] over)
    {
      if (over == null)
      {
        return under;
      }

      if (under == null)
      {
        return over;
      }

      var merged = (Color[])under.Clone();

      for (int i = 0; i < merged.Length && i < over.Length; i++)
      {
        if (over[i].A != 0)
        {
          merged[i] = over[i];
        }
      }

      return merged;
    }

    /// <summary>
    /// Pose la tete au repos sur chaque image du corps, pour le seul apercu.
    ///
    /// En jeu la tete est un sprite a part : le moteur la place lui-meme au-dessus du
    /// corps, image par image, et la fabrication les livre donc separement. L'apercu,
    /// lui, ne deroule qu'une planche - sans ce report, une tete choisie n'apparait
    /// nulle part avant d'essayer l'archer en jeu, et l'on ne peut ni la cadrer ni la
    /// recolorer en la voyant.
    ///
    /// Toujours l'image au repos : les quatre autres suivent la visee, qui n'a pas de
    /// sens dans un apercu qui se contente de derouler les poses du corps.
    ///
    /// La superposition est directe parce que la tete est cadree comme le corps, dans
    /// la meme fenetre, et que HeadYOrigins vaut alors l'ancre du cadre - le jeu les
    /// empile donc exactement de la meme facon. Voir ForgeSlots.HeadYOrigins.
    ///
    /// Avec un detail qui n'en est pas un : la tete DESCEND sur certaines poses, de ce
    /// que dit ForgeDesign.HeadOffsets. Sans ce report, l'apercu montrerait une tete
    /// rigide posee sur un corps qui s'accroupit, et l'on croirait le reglage sans
    /// effet alors qu'il ne manque qu'a l'image.
    /// </summary>
    private static void OverlayHead(ForgeImage strip, ForgeDesign design, ForgeFrame frame)
    {
      if (strip == null)
      {
        return;
      }

      // L'etat coiffe et non la tete nue : c'est celui que le jeu montre au depart,
      // et c'est aussi le seul moyen de voir ou tombe un chapeau pose en ornement.
      Color[] head = HeadPose(
          design, ForgeSlots.HeadNormal[0], ForgeSlots.OrnamentOf(ForgeSheet.HeadNormal), frame);

      if (head == null)
      {
        return;
      }

      // La tete porte ses propres remplacements : sans ce passage, l'apercu la
      // montrerait dans ses couleurs d'origine alors que le corps est deja recolore.
      head = (Color[])head.Clone();
      ColorPalette.Apply(
          head, ColorPalette.SwapMap(design.Colors, SpritePartGroups.Head), design.Colors);

      ForgeSlot[] poses = ForgeSlots.Of(ForgeSheet.Body);

      for (int index = 0; index * frame.Width + frame.Width <= strip.Width; index++)
      {
        int left = index * frame.Width;

        // Une ancre plus basse dessine la tete plus haut : l'ecart la fait donc
        // descendre, exactement comme le jeu le fera avec le meme nombre.
        int drop = index < poses.Length ? design.HeadOffsetOf(poses[index].Key) : 0;

        for (int y = 0; y < frame.Height; y++)
        {
          int targetY = y + drop;

          if (targetY < 0 || targetY >= frame.Height)
          {
            continue;
          }

          for (int x = 0; x < frame.Width; x++)
          {
            Color pixel = head[y * frame.Width + x];

            if (pixel.A != 0)
            {
              strip[left + x, targetY] = pixel;
            }
          }
        }
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
    private static void BuildHat(ForgeArt art, ForgeDesign design, ForgeFrame frame)
    {
      art.Hat = Trimmed(Cutout(design, "hat_normal", frame));

      if (art.Hat == null)
      {
        return;
      }

      art.HatRed = Trimmed(Cutout(design, "hat_red", frame)) ?? Tinted(art.Hat, RedHue);
      art.HatBlue = Trimmed(Cutout(design, "hat_blue", frame)) ?? Tinted(art.Hat, BlueHue);
    }

    /// <summary>Image d'un emplacement, a la taille de la fenetre, ou null.</summary>
    private static ForgeImage Cutout(ForgeDesign design, string slotKey, ForgeFrame frame)
    {
      Color[] pixels = ReadPose(design, slotKey, frame);

      if (pixels == null)
      {
        return null;
      }

      var image = new ForgeImage(frame.Width, frame.Height);
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
    private static Color[] ReadPose(ForgeDesign design, string slotKey, ForgeFrame frame)
    {
      return ForgeCompose.Cut(ForgeCompose.Pose(design, slotKey), frame);
    }

    private static ForgeImage Assemble(
        ForgeDesign design, ForgeSheet sheet, Color[] fallback, List<string> substituted,
        ForgeFrame frame, bool slideHead)
    {
      ForgeSlot[] slots = ForgeSlots.Of(sheet);
      var strip = new ForgeImage(slots.Length * frame.Width, frame.Height);

      // La table de remplacement est calculee une fois par planche et non par pose :
      // toutes les poses d'une planche partagent la meme famille de couleurs.
      Dictionary<uint, Color> swaps =
          ColorPalette.SwapMap(design.Colors, ForgeColorSubject.GroupOf(sheet));

      foreach (ForgeSlot slot in slots)
      {
        Color[] pose = ReadPose(design, slot.Key, frame) ?? Slide(design, slot, frame, slideHead);

        if (pose == null)
        {
          pose = fallback;
          substituted.Add(slot.Key);
        }

        // Recopie avant recoloration : Apply travaille en place, et la pose de repli
        // est le MEME tableau pour tous les emplacements manquants. La recolorer sur
        // place lui appliquerait les reglages autant de fois qu'elle comble de trous.
        pose = (Color[])pose.Clone();
        ColorPalette.Apply(pose, swaps, design.Colors);

        int left = slot.Index * frame.Width;

        for (int y = 0; y < frame.Height; y++)
        {
          Array.Copy(pose, y * frame.Width, strip.Pixels, y * strip.Width + left, frame.Width);
        }
      }

      return strip;
    }

    private static ForgeImage FirstFrame(ForgeImage strip, ForgeFrame frame)
    {
      var first = new ForgeImage(frame.Width, frame.Height);

      for (int y = 0; y < frame.Height; y++)
      {
        Array.Copy(strip.Pixels, y * strip.Width, first.Pixels, y * frame.Width, frame.Width);
      }

      return first;
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
