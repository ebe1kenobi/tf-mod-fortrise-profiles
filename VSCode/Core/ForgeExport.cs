using System;
using System.Globalization;
using System.IO;
using System.Text;
using FortRise;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Ecrit un archer forge sous la forme d'un mod FortRise autonome.
  ///
  /// L'enregistrement a chaud sert a essayer ; l'export sert a garder. Un mod ecrit
  /// sur le disque passe par le chemin de chargement normal, celui que le mod de
  /// l'archer Brones valide deja, et il survit a la suppression du dessin dans la
  /// forge - il se partage, aussi, ce que rien d'autre ici ne permet.
  ///
  /// La forme produite est exactement celle du mod Brones, jusqu'aux noms de
  /// fichiers. Ce n'est pas de la deference : c'est la seule forme dont on sache
  /// qu'elle se charge.
  /// </summary>
  public static class ForgeExport
  {
    private const string Prefix = "Ebe1.Forge.";

    /// <summary>Racine des mods, a cote de laquelle FortRise range aussi les sauvegardes.</summary>
    public static string ModsRoot => Path.Combine(RiseCore.GameRootPath, "Mods");

    public static string DirOf(ForgeDesign design)
    {
      return Path.Combine(ModsRoot, Prefix + design.Name);
    }

    /// <summary>
    /// Ecrit le mod. Rend null si tout s'est bien passe, sinon la raison de l'echec -
    /// l'ecran l'affiche telle quelle, il n'y a rien a traduire en cours de route.
    /// </summary>
    public static string Write(ForgeDesign design)
    {
      if (design == null)
      {
        return "NO ARCHER";
      }

      if (!design.Buildable)
      {
        return "NAME OR STAND POSE MISSING";
      }

      ForgeArt art = ForgeBuild.Build(design);

      if (art == null)
      {
        return "BUILD FAILED";
      }

      try
      {
        string root = DirOf(design);
        string sprites = Path.Combine(root, "Content", "Atlas", "sprites");
        string name = design.Name;

        Directory.CreateDirectory(root);

        WritePng(art.Body, sprites, "player", name + ".png");
        WritePng(art.BodyRed, sprites, "player", name + "_red.png");
        WritePng(art.BodyBlue, sprites, "player", name + "_blue.png");

        WritePng(art.Corpse, sprites, "player/corpses", name + "-normal.png");
        WritePng(art.CorpseRed, sprites, "player/corpses", name + "-redTeam.png");
        WritePng(art.CorpseBlue, sprites, "player/corpses", name + "-blueTeam.png");
        WritePng(art.CorpseFlash, sprites, "player/corpses", name + "-flash.png");

        WritePng(art.HeadNoHat, sprites, "player/head", name + "NoHat.png");
        WritePng(art.HeadNoHatRed, sprites, "player/head", name + "NoHat_red.png");
        WritePng(art.HeadNoHatBlue, sprites, "player/head", name + "NoHat_blue.png");

        WritePng(art.HeadNormal, sprites, "player/head", name + "Normal.png");
        WritePng(art.HeadNormalRed, sprites, "player/head", name + "Normal_red.png");
        WritePng(art.HeadNormalBlue, sprites, "player/head", name + "Normal_blue.png");

        WritePng(art.HeadCrown, sprites, "player/head", name + "Crown.png");
        WritePng(art.HeadCrownRed, sprites, "player/head", name + "Crown_red.png");
        WritePng(art.HeadCrownBlue, sprites, "player/head", name + "Crown_blue.png");

        WritePng(art.Statue, sprites, "player/statues", name + ".png");
        WritePng(art.PortraitNotJoined, sprites, "portraits", "notJoined" + name + ".png");
        WritePng(art.PortraitJoined, sprites, "portraits", "joined" + name + ".png");
        WritePng(art.PortraitWin, sprites, "portraits", "win" + name + ".png");
        WritePng(art.PortraitLose, sprites, "portraits", "lose" + name + ".png");

        WritePng(art.Bow, sprites, "player", name + "Bow.png");
        WritePng(art.BowRed, sprites, "player", name + "Bow_red.png");
        WritePng(art.BowBlue, sprites, "player", name + "Bow_blue.png");
        WritePng(art.Aimer, sprites, "aimers", name + ".png");
        WritePng(art.Gem, sprites, "pickups", name + "Gem.png");
        WritePng(art.MenuGem, sprites, "portraits", "gem" + name + ".png");

        // Le chapeau est facultatif : son absence est un choix, pas une piece
        // manquante. L'ecrire sans garde ferait trois erreurs au journal a chaque
        // export d'archer tete nue - c'est-a-dire presque a chaque fois.
        if (art.Hat != null)
        {
          WritePng(art.Hat, sprites, "player/hat", name + ".png");
          WritePng(art.HatRed, sprites, "player/hat", name + "_red.png");
          WritePng(art.HatBlue, sprites, "player/hat", name + "_blue.png");
        }

        WriteVoice(design, root);
        WriteMusic(design, root);

        WriteText(Path.Combine(root, "meta.json"), Meta(design));

        string gameData = Path.Combine(root, "Content", "Atlas", "GameData");
        string spriteData = Path.Combine(root, "Content", "Atlas", "SpriteData");

        WriteText(Path.Combine(gameData, "archerData.xml"), ArcherXml(design));
        WriteText(Path.Combine(spriteData, "spriteData.xml"),
            SpriteXml(design, art.Frame, art.HasHead));
        WriteText(Path.Combine(spriteData, "corpseSpriteData.xml"), CorpseXml(design, art.Frame));
        WriteText(Path.Combine(spriteData, "menuSpriteData.xml"), MenuXml(design));

        if (art.Substituted.Count > 0)
        {
          Log.Info($"[Forge] {name} exporte avec des poses comblees par la pose debout - "
                   + string.Join(", ", art.Substituted));
        }

        Log.Info($"[Forge] {name} exporte dans {root}");
        return null;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] export de {design.Name} impossible : {e}");
        return "WRITE FAILED";
      }
    }

    // ------------------------------------------------------------------
    // Fichiers
    // ------------------------------------------------------------------

    private static void WritePng(ForgeImage image, string root, string folder, string file)
    {
      if (image == null)
      {
        // Une piece absente - une region d'atlas qui a change de nom, par exemple -
        // ne doit pas faire echouer l'export entier. Le mod se chargera avec un arc
        // manquant, ce qui se voit et se corrige, plutot que pas du tout.
        Log.Error($"[Forge] {file} absente, ignoree a l'export");
        return;
      }

      string dir = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar));
      Directory.CreateDirectory(dir);

      Texture2D texture = null;

      try
      {
        texture = new Texture2D(Engine.Instance.GraphicsDevice, image.Width, image.Height);
        texture.SetData(image.Pixels);

        using FileStream stream = File.Create(Path.Combine(dir, file));
        texture.SaveAsPng(stream, image.Width, image.Height);
      }
      finally
      {
        try { texture?.Dispose(); } catch { }
      }
    }

    /// <summary>
    /// Recopie dans le mod les sons assignes.
    ///
    /// Le chargeur ne cherche pas les fichiers sous leur nom d'origine mais sous le
    /// motif declare dans SFX : chaque WAV est donc renomme d'apres son action. Les
    /// sons a variantes se cherchent en plus numerotes - READY_01.wav et non
    /// READY.wav - et un fichier mal nomme est ignore sans un mot.
    /// </summary>
    private static void WriteVoice(ForgeDesign design, string root)
    {
      string dir = Path.Combine(root, "Content", "SFX");

      // L'export se rejoue : un son retire du dessin doit disparaitre du mod, sans
      // quoi il continuerait d'etre joue.
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }

      if (ForgeVoice.AssignedCount(design) == 0)
      {
        return;
      }

      Directory.CreateDirectory(dir);

      foreach (ForgeVoiceAction action in ForgeVoice.Actions)
      {
        string source = ForgeVoice.PathOf(design, action.Key);

        if (source != null)
        {
          File.Copy(source, Path.Combine(dir, ForgeVoice.ExportName(action)), true);
        }
      }
    }

    /// <summary>
    /// Recopie la musique de victoire apportee.
    ///
    /// Contrairement aux sons, le fichier garde son nom : c'est le chemin cite dans
    /// archerData.xml qui le designe, et le chargeur d'archer enregistre la piste
    /// depuis ce chemin. Aucun content.json n'est necessaire.
    /// </summary>
    private static void WriteMusic(ForgeDesign design, string root)
    {
      string dir = Path.Combine(root, "Content", "Music");

      // L'export se rejoue : une musique changee ne doit pas laisser l'ancienne
      // derriere elle, ou le mod grossirait a chaque essai.
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }

      string source = ForgeMusic.FilePathOf(design);

      if (source == null)
      {
        return;
      }

      Directory.CreateDirectory(dir);
      File.Copy(source, Path.Combine(dir, Path.GetFileName(source)), true);
    }

    private static void WriteText(string path, string content)
    {
      string dir = Path.GetDirectoryName(path);

      if (!string.IsNullOrEmpty(dir))
      {
        Directory.CreateDirectory(dir);
      }

      File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    // ------------------------------------------------------------------
    // Descriptions
    // ------------------------------------------------------------------

    private static string Meta(ForgeDesign design)
    {
      return "{\n"
             + $"  \"name\": \"{Prefix}{design.Name}\",\n"
             + "  \"version\": \"1.0.0\",\n"
             + "  \"author\": \"forge\",\n"
             + $"  \"description\": \"Archer forge : {Escape(design.Name0)} {Escape(design.Name1)}\",\n"
             + "  \"dependencies\": [\n"
             + "    { \"name\": \"FortRise\", \"version\": \"5.3.3\" }\n"
             + "  ]\n"
             + "}\n";
    }

    private static string ArcherXml(ForgeDesign design)
    {
      string name = design.Name;
      var xml = new StringBuilder();

      xml.AppendLine("<Archers>");
      xml.AppendLine();
      xml.AppendLine("  <!--");
      xml.AppendLine("    Archer produit par la forge du mod Profiles. Ce fichier se reecrit a");
      xml.AppendLine("    chaque export : le modifier a la main ne survit pas.");
      xml.AppendLine();
      xml.AppendLine("    StartNoHat vaut True et HeadNormal pointe sur la tete vide : le");
      xml.AppendLine("    personnage porte deja sa tete dans son corps, lui en poser une seconde");
      xml.AppendLine("    par-dessus serait absurde. Le seul effet de jeu perdu est le chapeau qui");
      xml.AppendLine("    s'envole quand on est touche.");
      xml.AppendLine("  -->");
      xml.AppendLine();
      xml.AppendLine($"  <Archer id=\"{name}\">");
      xml.AppendLine($"    <Name0>{Escape(design.Name0)}</Name0>");
      xml.AppendLine($"    <Name1>{Escape(design.Name1)}</Name1>");
      xml.AppendLine($"    <ColorA>{design.ColorA}</ColorA>");
      xml.AppendLine($"    <ColorB>{design.ColorB}</ColorB>");
      xml.AppendLine($"    <LightbarColor>{design.ColorA}</LightbarColor>");
      xml.AppendLine($"    <Aimer>Content/Atlas/sprites/aimers/{name}.png</Aimer>");
      xml.AppendLine($"    <Corpse>@{name}Corpse</Corpse>");
      xml.AppendLine("    <Gender>Male</Gender>");

      // Tete nue SAUF si un chapeau a ete choisi : declarer les deux donnerait un
      // archer qui ne perd jamais son chapeau, donc un chapeau invisible.
      bool hat = design.PickOf("hat_normal") != null;
      xml.AppendLine($"    <StartNoHat>{(hat ? "False" : "True")}</StartNoHat>");

      if (hat)
      {
        xml.AppendLine();
        xml.AppendLine("    <Hat>");
        xml.AppendLine($"      <Normal>Content/Atlas/sprites/player/hat/{name}.png</Normal>");
        xml.AppendLine($"      <Blue>Content/Atlas/sprites/player/hat/{name}_blue.png</Blue>");
        xml.AppendLine($"      <Red>Content/Atlas/sprites/player/hat/{name}_red.png</Red>");
        xml.AppendLine("    </Hat>");
      }

      // Le chargeur ne fournit aucune valeur par defaut a ce champ, et le jeu
      // interroge un dictionnaire avec : absent, il fait tomber la fin du round.
      //
      // Une musique apportee se cite par son chemin dans le mod : le chargeur
      // d'archer enregistre lui-meme la piste quand ce chemin existe. C'est la
      // seule chose que l'export sait faire et que l'essai a chaud ne sait pas.
      string music = ForgeMusic.ExportPath(design) ?? ForgeMusic.Resolve(design);
      xml.AppendLine($"    <VictoryMusic>{music}</VictoryMusic>");
      xml.AppendLine();
      xml.Append(VoiceXml(design));
      xml.AppendLine("    <Sprites>");
      xml.AppendLine($"      <Body>@{name}</Body>");
      xml.AppendLine($"      <HeadNormal>@{name}Normal</HeadNormal>");
      xml.AppendLine($"      <HeadNoHat>@{name}NoHat</HeadNoHat>");
      xml.AppendLine($"      <HeadCrown>@{name}Crown</HeadCrown>");
      xml.AppendLine($"      <Bow>@{name}Bow</Bow>");
      xml.AppendLine("    </Sprites>");
      xml.AppendLine();
      xml.AppendLine("    <Portraits>");
      xml.AppendLine($"      <NotJoined>Content/Atlas/sprites/portraits/notJoined{name}.png</NotJoined>");
      xml.AppendLine($"      <Joined>Content/Atlas/sprites/portraits/joined{name}.png</Joined>");
      xml.AppendLine($"      <Win>Content/Atlas/sprites/portraits/win{name}.png</Win>");
      xml.AppendLine($"      <Lose>Content/Atlas/sprites/portraits/lose{name}.png</Lose>");
      xml.AppendLine("    </Portraits>");
      xml.AppendLine();
      xml.AppendLine("    <Statue>");
      xml.AppendLine($"      <Image>Content/Atlas/sprites/player/statues/{name}.png</Image>");
      xml.AppendLine($"      <Glow>Content/Atlas/sprites/player/statues/{name}.png</Glow>");
      xml.AppendLine("    </Statue>");
      xml.AppendLine();
      xml.AppendLine("    <Gems>");
      xml.AppendLine($"      <Menu>@Gem{name}Menu</Menu>");
      xml.AppendLine($"      <Gameplay>@Gem{name}</Gameplay>");
      xml.AppendLine("    </Gems>");
      xml.AppendLine();
      xml.AppendLine("    <Breathing>");
      xml.AppendLine("      <Offset x=\"10\" y=\"-4\"/>");
      xml.AppendLine("      <DuckingOffset x=\"10\" y=\"1\"/>");
      xml.AppendLine("      <Interval>120</Interval>");
      xml.AppendLine("    </Breathing>");
      xml.AppendLine();
      xml.AppendLine();
      xml.AppendLine("  </Archer>");
      xml.AppendLine();
      xml.AppendLine("</Archers>");

      return xml.ToString();
    }

    /// <summary>
    /// La voix : un repli, et les fichiers qui le remplacent action par action.
    ///
    /// Le motif {action} est ce que le chargeur remplace par la cle de chaque son.
    /// SFXFallback n'a d'effet QUE lorsque SFX est un chemin - avec un entier, le
    /// chargeur reprend la voix entiere de l'archer designe et ne cherche aucun
    /// fichier. On ecrit donc l'une ou l'autre forme, jamais les deux.
    /// </summary>
    private static string VoiceXml(ForgeDesign design)
    {
      var xml = new StringBuilder();

      if (ForgeVoice.AssignedCount(design) == 0)
      {
        xml.AppendLine($"    <SFX>{design.VoiceFallback}</SFX>");
        xml.AppendLine();
        return xml.ToString();
      }

      xml.AppendLine("    <SFX>Content/SFX/{action}.wav</SFX>");
      xml.AppendLine($"    <SFXFallback>{design.VoiceFallback}</SFXFallback>");
      xml.AppendLine();
      return xml.ToString();
    }

    private static string SpriteXml(ForgeDesign design, ForgeFrame frame, bool hasHead)
    {
      string name = design.Name;
      var xml = new StringBuilder();

      xml.AppendLine("<SpriteData>");
      xml.AppendLine();
      xml.AppendLine("  <!--");
      xml.AppendLine("    Produit par la forge. La taille des images n'est pas une constante : elle");
      xml.AppendLine("    est mesuree sur les poses choisies, de facon que rien ne soit rogne. Les");
      xml.AppendLine("    archers d'origine tiennent dans 20x20, un personnage Broforce demande 24,");
      xml.AppendLine("    une planche plus grande demandera davantage.");
      xml.AppendLine();
      xml.AppendLine("    L'origine est le point du dessin que le jeu pose sur la position de");
      xml.AppendLine("    l'entite : les pieds, et deux pixels a droite du centre du corps - c'est");
      xml.AppendLine("    ainsi que sont cales tous les archers du jeu, arc devant eux.");
      xml.AppendLine("  -->");
      xml.AppendLine();
      xml.AppendLine($"  <sprite_string id=\"{name}\">");
      xml.AppendLine($"    <Texture>Content/Atlas/sprites/player/{name}.png</Texture>");
      xml.AppendLine($"    <RedTexture>Content/Atlas/sprites/player/{name}_red.png</RedTexture>");
      xml.AppendLine($"    <BlueTexture>Content/Atlas/sprites/player/{name}_blue.png</BlueTexture>");
      xml.AppendLine($"    <FrameWidth>{frame.Width}</FrameWidth>");
      xml.AppendLine($"    <FrameHeight>{frame.Height}</FrameHeight>");
      xml.AppendLine($"    <OriginX>{frame.OriginX}</OriginX>");
      xml.AppendLine($"    <OriginY>{frame.OriginY}</OriginY>");
      xml.AppendLine("    <X>0</X>");
      xml.AppendLine("    <Y>8</Y>");
      xml.AppendLine();
      xml.AppendLine("    <!--");
      xml.AppendLine("      Faut-il dessiner la tete pendant la glissade d'esquive ? Non quand le");
      xml.AppendLine("      corps la porte deja - en poser une seconde par-dessus en ferait deux -");
      xml.AppendLine("      oui quand elle est fournie a part. Un archer importe garde la reponse");
      xml.AppendLine("      de son mod d'origine.");
      xml.AppendLine("    -->");
      xml.AppendLine($"    <SlideHead>{((design.SlideHead ?? hasHead) ? "True" : "False")}</SlideHead>");
      xml.AppendLine();
      xml.AppendLine("    <Animations>");
      xml.AppendLine("      <Anim id=\"stand\" frames=\"0\"/>");
      xml.AppendLine("      <Anim id=\"run\" delay=\".06\" frames=\"2,1,2,3\" loop=\"True\"/>");
      xml.AppendLine("      <Anim id=\"jump\" delay=\".06\" frames=\"5\" loop=\"False\"/>");
      xml.AppendLine("      <Anim id=\"fall\" delay=\".06\" frames=\"6\" loop=\"True\"/>");
      xml.AppendLine("      <Anim id=\"glide\" delay=\".06\" frames=\"6\" loop=\"True\"/>");
      xml.AppendLine("      <Anim id=\"ledge\" frames=\"4\"/>");
      xml.AppendLine("      <Anim id=\"dodge\" delay=\".1\" frames=\"7\"/>");
      xml.AppendLine("      <Anim id=\"duck\" frames=\"9\"/>");
      xml.AppendLine("      <Anim id=\"slide_nohat\" frames=\"8\"/>");
      xml.AppendLine("      <Anim id=\"slide_normal\" frames=\"10\"/>");
      xml.AppendLine("      <Anim id=\"slide_crown\" frames=\"11\"/>");
      xml.AppendLine("    </Animations>");
      xml.AppendLine("    <!--");
      xml.AppendLine("      Une valeur par image du corps : la hauteur a laquelle le jeu accroche");
      xml.AppendLine("      la tete. Obligatoire - Player le lit sans verifier qu'il existe - et");
      xml.AppendLine("      indexe sans borne par l'image courante : un tableau plus court que la");
      xml.AppendLine("      planche fait tomber le jeu en plein match.");
      xml.AppendLine("    -->");
      // Le cas "le dessin porte sa propre tete" doit passer ici aussi : sans lui, un
      // archer a tete exporte sortirait avec les hauteurs relevees sur les planches
      // Broforce, qui n'ont rien a voir avec l'ancre de son cadre.
      xml.AppendLine("    <HeadYOrigins>"
                     + string.Join(",", ForgeSlots.HeadYOrigins(design, hasHead, frame.OriginY))
                     + "</HeadYOrigins>");
      xml.AppendLine("  </sprite_string>");
      xml.AppendLine();

      AppendHeadSprite(xml, name, "NoHat", 7, frame);
      AppendHeadSprite(xml, name, "Normal", 8, frame);
      AppendHeadSprite(xml, name, "Crown", 8, frame);

      xml.AppendLine("  <!--");
      xml.AppendLine("    Arc repris du jeu et repeint. Meme decoupage que l'original :");
      xml.AppendLine("    2 colonnes de 12x10 sur 3 rangees.");
      xml.AppendLine("  -->");
      xml.AppendLine();
      xml.AppendLine($"  <sprite_string id=\"{name}Bow\">");
      xml.AppendLine($"    <Texture>Content/Atlas/sprites/player/{name}Bow.png</Texture>");
      xml.AppendLine($"    <RedTexture>Content/Atlas/sprites/player/{name}Bow_red.png</RedTexture>");
      xml.AppendLine($"    <BlueTexture>Content/Atlas/sprites/player/{name}Bow_blue.png</BlueTexture>");
      xml.AppendLine("    <FrameWidth>12</FrameWidth>");
      xml.AppendLine("    <FrameHeight>10</FrameHeight>");
      xml.AppendLine("    <OriginX>2</OriginX>");
      xml.AppendLine("    <OriginY>5</OriginY>");
      xml.AppendLine("    <X>0</X>");
      xml.AppendLine("    <Y>-2</Y>");
      xml.AppendLine("    <DownY>1</DownY>");
      xml.AppendLine("    <Animations>");
      xml.AppendLine("      <Anim id=\"idle\" frames=\"2\"/>");
      xml.AppendLine("      <Anim id=\"drawn\" loop=\"False\" delay=\".04\" frames=\"1,0\"/>");
      xml.AppendLine("      <Anim id=\"drawnEmpty\" frames=\"4\"/>");
      xml.AppendLine("    </Animations>");
      xml.AppendLine("  </sprite_string>");
      xml.AppendLine();
      xml.AppendLine("  <!-- Gemme de jeu, reprise et repeinte comme l'arc. -->");
      xml.AppendLine();
      xml.AppendLine($"  <sprite_int id=\"Gem{name}\">");
      xml.AppendLine($"    <Texture>Content/Atlas/sprites/pickups/{name}Gem.png</Texture>");
      xml.AppendLine("    <FrameWidth>10</FrameWidth>");
      xml.AppendLine("    <FrameHeight>10</FrameHeight>");
      xml.AppendLine("    <OriginX>5</OriginX>");
      xml.AppendLine("    <OriginY>5</OriginY>");
      xml.AppendLine("    <Animations>");
      xml.AppendLine("      <Anim id=\"0\" delay=\".1\" loop=\"True\" frames=\"0,1,2,3,4,5,6,7\"/>");
      xml.AppendLine("    </Animations>");
      xml.AppendLine("  </sprite_int>");
      xml.AppendLine();
      xml.AppendLine("</SpriteData>");

      return xml.ToString();
    }

    /// <summary>
    /// Une planche de tete, vide.
    ///
    /// Le jeu exige les treize animations meme si elles pointent toutes sur du vide.
    /// Les ecrire n'est pas une precaution : sans elles le sprite ne se construit pas.
    /// </summary>
    private static void AppendHeadSprite(
        StringBuilder xml, string name, string suffix, int y, ForgeFrame frame)
    {
      string path = $"Content/Atlas/sprites/player/head/{name}{suffix}";

      xml.AppendLine($"  <sprite_string id=\"{name}{suffix}\">");
      xml.AppendLine($"    <Texture>{path}.png</Texture>");
      xml.AppendLine($"    <RedTexture>{path}_red.png</RedTexture>");
      xml.AppendLine($"    <BlueTexture>{path}_blue.png</BlueTexture>");
      // Au cadre du CORPS et non aux 10x10 des archers d'origine : la planche ecrite
      // juste a cote est faite de la meme facon, images du corps et images de tete
      // cadrees dans la meme fenetre. Declarer 10x10 la decouperait en morceaux.
      //
      // OriginY est sans effet - Player l'ecrase a chaque image avec HeadYOrigins -
      // mais on l'ecrit juste quand meme : un fichier qui ment finit par etre lu.
      xml.AppendLine($"    <FrameWidth>{frame.Width}</FrameWidth>");
      xml.AppendLine($"    <FrameHeight>{frame.Height}</FrameHeight>");
      xml.AppendLine($"    <OriginX>{frame.OriginX}</OriginX>");
      xml.AppendLine($"    <OriginY>{frame.OriginY}</OriginY>");
      xml.AppendLine("    <X>0</X>");
      xml.AppendLine($"    <Y>{y.ToString(CultureInfo.InvariantCulture)}</Y>");
      xml.AppendLine("    <Animations>");

      foreach (string anim in HeadAnimations)
      {
        xml.AppendLine($"      <Anim id=\"{anim.Split(':')[0]}\" frames=\"{anim.Split(':')[1]}\"/>");
      }

      xml.AppendLine("    </Animations>");
      xml.AppendLine("  </sprite_string>");
      xml.AppendLine();
    }

    private static readonly string[] HeadAnimations =
    {
      "idle:0", "lookUp:1", "lookDown:2", "lookBack:3",
      "idleFall:0", "lookUpFall:1", "lookDownFall:2", "lookBackFall:3",
      "idleJump:0", "lookUpJump:1", "lookDownJump:2", "lookBackJump:3",
      "duck:4"
    };

    /// <summary>
    /// Le cadavre, dans son propre fichier.
    ///
    /// Il vit ici et nulle part ailleurs : le chargeur de FortRise range chaque
    /// entree selon le FICHIER qui la contient, pas selon sa forme. PlayerCorpse les
    /// cherche dans TFGame.CorpseSpriteData ; declare dans spriteData.xml, le cadavre
    /// serait introuvable et le jeu tomberait a la premiere mort.
    /// </summary>
    private static string CorpseXml(ForgeDesign design, ForgeFrame frame)
    {
      string name = design.Name;
      var xml = new StringBuilder();

      xml.AppendLine("<SpriteData>");
      xml.AppendLine();
      xml.AppendLine($"  <sprite_string id=\"{name}Corpse\">");
      xml.AppendLine($"    <Texture>Content/Atlas/sprites/player/corpses/{name}-normal.png</Texture>");
      xml.AppendLine($"    <BlueTeam>Content/Atlas/sprites/player/corpses/{name}-blueTeam.png</BlueTeam>");
      xml.AppendLine($"    <RedTeam>Content/Atlas/sprites/player/corpses/{name}-redTeam.png</RedTeam>");
      xml.AppendLine($"    <Flash>Content/Atlas/sprites/player/corpses/{name}-flash.png</Flash>");
      xml.AppendLine($"    <FrameWidth>{frame.Width}</FrameWidth>");
      xml.AppendLine($"    <FrameHeight>{frame.Height}</FrameHeight>");
      xml.AppendLine();
      xml.AppendLine("    <!--");
      xml.AppendLine("      L'ancre est celle du corps, le cadavre etant fabrique dans le meme");
      xml.AppendLine("      cadre. Le jeu ne la demande pas ici, mais la forge la relit a l'import :");
      xml.AppendLine("      sans elle, un cadavre repris se reposerait de travers.");
      xml.AppendLine("    -->");
      xml.AppendLine($"    <OriginX>{frame.OriginX}</OriginX>");
      xml.AppendLine($"    <OriginY>{frame.OriginY}</OriginY>");
      xml.AppendLine("    <Animations>");
      xml.AppendLine("      <Anim id=\"ground\" frames=\"0\"/>");
      xml.AppendLine("      <Anim id=\"fall\" frames=\"1\"/>");
      xml.AppendLine("      <Anim id=\"pinned\" frames=\"2\"/>");
      xml.AppendLine("      <Anim id=\"slouched\" frames=\"3\"/>");
      xml.AppendLine("      <Anim id=\"flying\" frames=\"4\"/>");
      xml.AppendLine("      <Anim id=\"ledge\" frames=\"5\"/>");
      xml.AppendLine("    </Animations>");
      xml.AppendLine("  </sprite_string>");
      xml.AppendLine();
      xml.AppendLine("</SpriteData>");

      return xml.ToString();
    }

    /// <summary>
    /// Les images de l'animation allumee de la gemme du rollcall.
    ///
    /// Les neuf premieres tournent, puis la premiere se repete une trentaine de fois.
    /// Ce n'est pas une maladresse recopiee : c'est ainsi que le jeu fait attendre
    /// entre deux scintillements, la boucle etant le seul temporisateur dont un
    /// sprite dispose.
    /// </summary>
    private const string MenuGemOnFrames =
        "1,2,3,4,5,6,7,8,9,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";

    /// <summary>
    /// La gemme du rollcall.
    ///
    /// Meme piege que le cadavre, dans l'autre sens : c'est un sprite_string de
    /// menuSpriteData.xml, lu par ArcherPortrait.InitGem par GetSpriteString.
    /// Declaree en sprite_int ou dans le mauvais fichier, elle est introuvable de la,
    /// et le jeu tombe des qu'on arrive sur l'archer.
    /// </summary>
    private static string MenuXml(ForgeDesign design)
    {
      string name = design.Name;
      var xml = new StringBuilder();

      xml.AppendLine("<SpriteData>");
      xml.AppendLine();
      xml.AppendLine($"  <sprite_string id=\"Gem{name}Menu\">");
      xml.AppendLine($"    <Texture>Content/Atlas/sprites/portraits/gem{name}.png</Texture>");
      xml.AppendLine("    <FrameWidth>20</FrameWidth>");
      xml.AppendLine("    <FrameHeight>20</FrameHeight>");
      xml.AppendLine("    <OriginX>10</OriginX>");
      xml.AppendLine("    <OriginY>10</OriginY>");
      xml.AppendLine("    <Y>60</Y>");
      xml.AppendLine("    <Animations>");
      xml.AppendLine("      <Anim id=\"off\" frames=\"0\"/>");
      xml.AppendLine($"      <Anim id=\"on\" delay=\".06\" loop=\"True\" frames=\"{MenuGemOnFrames}\"/>");
      xml.AppendLine("    </Animations>");
      xml.AppendLine("  </sprite_string>");
      xml.AppendLine();
      xml.AppendLine("</SpriteData>");

      return xml.ToString();
    }

    private static string Escape(string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return "";
      }

      return text
          .Replace("&", "&amp;")
          .Replace("<", "&lt;")
          .Replace(">", "&gt;")
          .Replace("\"", "&quot;");
    }
  }
}
