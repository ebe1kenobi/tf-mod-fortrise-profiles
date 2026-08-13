using System;
using System.Collections.Generic;
using System.IO;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Enregistre un archer forge dans le jeu en cours, sans redemarrage.
  ///
  /// FortRise l'autorise : sa file d'enregistrement appelle l'invoker sans attendre
  /// des lors que le chargement est termine, et le registre de textures accepte une
  /// fonction rendant une Subtexture - donc une texture fabriquee en memoire, ce que
  /// ForgeBuild produit deja.
  ///
  /// L'ordre compte, parce que chaque etape nomme la precedente : les textures
  /// d'abord, les sprites qui les citent ensuite, l'archer qui cite les sprites en
  /// dernier.
  ///
  /// Ce qui reste imparfait a chaud, et qu'il vaut mieux savoir : le nombre
  /// d'archers change sous une interface qui l'a peut-etre deja lu. Sortir du menu
  /// et y revenir suffit a le corriger. Pour un archer definitif, l'export ecrit un
  /// vrai mod, qui passe par le chemin de chargement normal.
  /// </summary>
  public static class ForgeRegister
  {
    // Les archers deja poses, par identifiant de dessin. Reenregistrer le meme
    // dessin doit refaire ses textures - on vient peut-etre d'en changer une pose -
    // mais ne doit pas lui donner un second emplacement d'archer.
    private static readonly Dictionary<string, IArcherEntry> registered =
        new Dictionary<string, IArcherEntry>();

    // Rang d'enregistrement, ajoute aux identifiants de texture. Sans lui, refaire
    // un archer reutiliserait les memes noms, et FortRise se contenterait de
    // prevenir qu'il ecrase - en gardant, selon le moment, l'ancienne image.
    private static int generation;

    public static bool IsRegistered(ForgeDesign design)
    {
      return design != null && registered.ContainsKey(design.Id);
    }

    /// <summary>Index de l'archer dans ArcherData.Archers, ou -1.</summary>
    public static int IndexOf(ForgeDesign design)
    {
      if (design != null && registered.TryGetValue(design.Id, out IArcherEntry entry))
      {
        return entry.Index;
      }

      return -1;
    }

    /// <summary>
    /// Pose l'archer dans le jeu. Rend null si tout s'est bien passe, sinon la raison
    /// de l'echec, telle que l'ecran doit l'afficher.
    /// </summary>
    public static string Register(ForgeDesign design)
    {
      if (design == null)
      {
        return "NO ARCHER";
      }

      if (!design.Buildable)
      {
        return "NAME OR STAND POSE MISSING";
      }

      // Deja essaye : on refait ses planches et on les remet a la place des
      // anciennes, sans lui donner un second emplacement d'archer. Voir Apply.
      registered.TryGetValue(design.Id, out IArcherEntry existing);

      ForgeArt art = ForgeBuild.Build(design);

      if (art == null)
      {
        return "BUILD FAILED";
      }

      try
      {
        IModRegistry registry = TFModFortRiseArcherModule.Instance.Context.Registry;
        generation++;

        string prefix = $"forge/{design.Name}/{generation}/";
        string name = design.Name;

        // --- textures ---

        ISubtextureEntry body = Texture(registry, prefix + "body", art.Body);
        ISubtextureEntry bodyRed = Texture(registry, prefix + "body_red", art.BodyRed);
        ISubtextureEntry bodyBlue = Texture(registry, prefix + "body_blue", art.BodyBlue);

        ISubtextureEntry corpse = Texture(registry, prefix + "corpse", art.Corpse);
        ISubtextureEntry corpseRed = Texture(registry, prefix + "corpse_red", art.CorpseRed);
        ISubtextureEntry corpseBlue = Texture(registry, prefix + "corpse_blue", art.CorpseBlue);
        ISubtextureEntry corpseFlash = Texture(registry, prefix + "corpse_flash", art.CorpseFlash);

        ISubtextureEntry headNoHat = Texture(registry, prefix + "head_nohat", art.HeadNoHat);
        ISubtextureEntry headNoHatRed = Texture(registry, prefix + "head_nohat_red", art.HeadNoHatRed);
        ISubtextureEntry headNoHatBlue = Texture(registry, prefix + "head_nohat_blue", art.HeadNoHatBlue);

        ISubtextureEntry headNormal = Texture(registry, prefix + "head_normal", art.HeadNormal);
        ISubtextureEntry headNormalRed = Texture(registry, prefix + "head_normal_red", art.HeadNormalRed);
        ISubtextureEntry headNormalBlue = Texture(registry, prefix + "head_normal_blue", art.HeadNormalBlue);

        ISubtextureEntry headCrown = Texture(registry, prefix + "head_crown", art.HeadCrown);
        ISubtextureEntry headCrownRed = Texture(registry, prefix + "head_crown_red", art.HeadCrownRed);
        ISubtextureEntry headCrownBlue = Texture(registry, prefix + "head_crown_blue", art.HeadCrownBlue);
        ISubtextureEntry bow = Texture(registry, prefix + "bow", art.Bow);
        ISubtextureEntry bowRed = Texture(registry, prefix + "bow_red", art.BowRed);
        ISubtextureEntry bowBlue = Texture(registry, prefix + "bow_blue", art.BowBlue);
        ISubtextureEntry aimer = Texture(registry, prefix + "aimer", art.Aimer);
        ISubtextureEntry gem = Texture(registry, prefix + "gem", art.Gem);

        // La gemme du rollcall se lit dans l'atlas des menus et pas dans l'autre.
        ISubtextureEntry menuGem = Texture(
            registry, prefix + "gem_menu", art.MenuGem, SubtextureAtlasDestination.MenuAtlas);

        // --- sprites ---

        ISpriteContainerEntry bodySprite = registry.Sprites.RegisterSprite(
            name, BodyConfig(design, body, bodyRed, bodyBlue, art.HasHead, art.Frame));

        // Trois planches et non une : le jeu choisit celle qu'il montre selon le
        // couvre-chef, et c'est ainsi qu'un chapeau s'envole sans emmener la tete.
        ISpriteContainerEntry headNoHatSprite = registry.Sprites.RegisterSprite(
            name + "HeadNoHat", HeadConfig(headNoHat, headNoHatRed, headNoHatBlue, art.Frame));

        ISpriteContainerEntry headNormalSprite = registry.Sprites.RegisterSprite(
            name + "HeadNormal", HeadConfig(headNormal, headNormalRed, headNormalBlue, art.Frame));

        ISpriteContainerEntry headCrownSprite = registry.Sprites.RegisterSprite(
            name + "HeadCrown", HeadConfig(headCrown, headCrownRed, headCrownBlue, art.Frame));

        ISpriteContainerEntry bowSprite = registry.Sprites.RegisterSprite(
            name + "Bow", BowConfig(bow, bowRed, bowBlue));

        ICorpseSpriteContainerEntry corpseSprite = registry.Sprites.RegisterCorpseSprite(
            name + "Corpse", CorpseConfig(corpse, corpseRed, corpseBlue, corpseFlash, art.Frame));

        ISpriteContainerEntry gemSprite = registry.Sprites.RegisterSprite(
            "Gem" + name, GemConfig(gem));

        IMenuSpriteContainerEntry menuGemSprite = registry.Sprites.RegisterMenuSprite(
            "Gem" + name + "Menu", MenuGemConfig(menuGem));

        // --- archer ---

        var configuration = new ArcherConfiguration
        {
          TopName = Line(design.Name0, name),
          BottomName = Line(design.Name1, "FORGE"),
          ColorA = Hex(design.ColorA, Color.White),
          ColorB = Hex(design.ColorB, Color.White),
          LightbarColor = Hex(design.ColorA, Color.White),
          Aimer = aimer,
          CorpseSprite = corpseSprite,
          Gender = TFGame.Genders.Male,

          // Obligatoire malgre les apparences : PlayVictoryMusic interroge un
          // dictionnaire avec ce nom, sans le verifier. Laisse vide, il fait tomber
          // le jeu a la fin du round.
          VictoryMusic = ForgeMusic.Resolve(design),

          // Le personnage porte sa tete dans son corps : lui en poser une seconde
          // par-dessus serait absurde, et la tete enregistree est vide de toute
          // facon.
          //
          // On part tete nue SAUF si un chapeau a ete choisi : StartNoHat a True
          // avec un chapeau declare donnerait un archer qui n'en perd jamais, donc
          // un chapeau qu'on ne verrait jamais.
          StartNoHat = art.Hat == null,
          Hat = HatOf(registry, prefix, art),

          Sprites = new SpriteInfo
          {
            Body = bodySprite,
            HeadNormal = headNormalSprite,
            HeadNoHat = headNoHatSprite,
            HeadCrown = headCrownSprite,
            Bow = bowSprite
          },
          Portraits = new PortraitInfo
          {
            NotJoined = Texture(registry, prefix + "notjoined", art.PortraitNotJoined),
            Joined = Texture(registry, prefix + "joined", art.PortraitJoined),
            Win = Texture(registry, prefix + "win", art.PortraitWin),
            Lose = Texture(registry, prefix + "lose", art.PortraitLose)
          },
          Statue = new StatueInfo
          {
            Image = Texture(registry, prefix + "statue", art.Statue),
            Glow = Texture(registry, prefix + "statue_glow", art.Statue)
          },
          Gems = new GemInfo
          {
            Gameplay = gemSprite,
            Menu = menuGemSprite
          }
        };

        configuration = configuration with { SFX = Voice(registry, design, name) };

        // Deuxieme essai et suivants : l'archer existe deja a son index, on ne fait
        // que remplacer ce qu'il montre. Rien n'est ajoute nulle part, donc rien ne
        // se decale - et on peut corriger une pose et reessayer sans redemarrer.
        if (existing != null)
        {
          Apply(existing.ArcherData, configuration);
          Substitutions(art, name);
          Log.Info($"[Forge] {name} refait a l'index {existing.Index}");
          return null;
        }

        // Le costume ALT occupe l'emplacement de son parent : il faut donc que le
        // parent soit deja pose. On ne peut pas l'enregistrer a sa place - son propre
        // dessin peut etre incomplet - alors on le dit et on s'arrete.
        if (design.IsAlt)
        {
          ForgeDesign parent = ForgeStorage.Find(design.AltOf);

          if (parent == null)
          {
            return "L'ARCHER PARENT N'EXISTE PLUS";
          }

          if (!registered.TryGetValue(parent.Id, out IArcherEntry parentEntry))
          {
            return "ESSAYER D'ABORD " + parent.Name;
          }

          configuration = configuration with { AltFor = parentEntry };
        }

        IArcherEntry entry = registry.Archers.RegisterArcher(name, configuration);
        registered[design.Id] = entry;

        // Trois familles de tableaux sont indexees par archer et dimensionnees au
        // chargement. Un archer ajoute a chaud les deborde toutes, et chacune tombe
        // a un moment different : les particules a la premiere esquive, les
        // compteurs a la premiere mort et en fin de match. La voix, elle, est posee
        // par Seat au moment ou on la construit.
        ForgeParticles.Extend();
        ForgeStats.Extend();

        Substitutions(art, name);
        Log.Info($"[Forge] {name} enregistre a l'index {entry.Index}");
        return null;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] enregistrement de {design.Name} impossible : {e}");
        return "REGISTER FAILED";
      }
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Remet un archer deja pose a jour, sur place.
    ///
    /// C'est ce qui permet de corriger une pose et de reessayer sans redemarrer. On
    /// ne peut pas l'enregistrer une seconde fois - FortRise ne sait pas retirer un
    /// archer, et un second sous le meme nom laisserait deux entrees dont une morte -
    /// mais on peut remplacer ce que la premiere montre.
    ///
    /// Les memes affectations que celles de <c>ModArchers.Invoke</c>, qui remplit
    /// cette meme structure au premier essai : un sprite s'y designe par un
    /// identifiant qui sera resolu a l'apparition du joueur, une image par l'objet
    /// lui-meme. C'est ce qui rend le remplacement possible - rien n'est fige dans
    /// l'archer, tout est relu quand on entre en partie.
    ///
    /// Ce qui a ete enregistre entre-temps - textures, sprites, sons - reste dans les
    /// registres de FortRise. On ne peut pas les en retirer, et cela n'a pas grande
    /// importance : ce sont quelques planches par essai, dans une session ou l'on
    /// essaie justement.
    /// </summary>
    private static void Apply(ArcherData data, ArcherConfiguration configuration)
    {
      data.Name0 = configuration.TopName;
      data.Name1 = configuration.BottomName;
      data.ColorA = configuration.ColorA;
      data.ColorB = configuration.ColorB;
      data.LightbarColor = configuration.LightbarColor;
      data.Aimer = configuration.Aimer.Subtexture;
      data.Corpse = configuration.CorpseSprite.Entry.ID;
      data.Gender = configuration.Gender;
      data.StartNoHat = configuration.StartNoHat;
      data.VictoryMusic = configuration.VictoryMusic;
      data.SFXID = configuration.SFX;

      // La structure est relue puis reecrite entiere : elle porte des champs que la
      // forge ne remplit pas - la tete arriere - et les recreer de zero les
      // effacerait.
      ArcherData.SpriteInfo sprites = data.Sprites;
      sprites.Body = configuration.Sprites.Body.Entry.ID;
      sprites.HeadNormal = configuration.Sprites.HeadNormal.Entry.ID;
      sprites.HeadNoHat = configuration.Sprites.HeadNoHat.Entry.ID;
      sprites.HeadCrown = configuration.Sprites.HeadCrown.Entry.ID;
      sprites.Bow = configuration.Sprites.Bow.Entry.ID;
      data.Sprites = sprites;

      ArcherData.PortraitInfo portraits = data.Portraits;
      portraits.NotJoined = configuration.Portraits.NotJoined.Subtexture;
      portraits.Joined = configuration.Portraits.Joined.Subtexture;
      portraits.Win = configuration.Portraits.Win.Subtexture;
      portraits.Lose = configuration.Portraits.Lose.Subtexture;
      data.Portraits = portraits;

      ArcherData.StatueInfo statue = data.Statue;
      statue.Image = configuration.Statue.Image.Subtexture;
      statue.Glow = configuration.Statue.Glow.Subtexture;
      data.Statue = statue;

      ArcherData.GemInfo gems = data.Gems;
      gems.Gameplay = configuration.Gems.Gameplay.Entry.ID;
      gems.Menu = configuration.Gems.Menu.Entry.ID;
      data.Gems = gems;

      ArcherData.HatInfo hat = data.Hat;

      if (configuration.Hat.TryGetValue(out HatInfo chosen))
      {
        hat.Normal = chosen.Normal.Subtexture;
        hat.Blue = chosen.Blue.Subtexture;
        hat.Red = chosen.Red.Subtexture;
      }
      else
      {
        // Un chapeau retire doit disparaitre : le laisser en place donnerait un
        // archer qui perd un chapeau qu'il ne porte plus.
        hat.Normal = null;
        hat.Blue = null;
        hat.Red = null;
      }

      data.Hat = hat;
    }

    /// <summary>
    /// Dit quelles poses ont pris la pose debout faute d'image.
    ///
    /// L'archer marche, mais il ne saute pas et ne meurt pas comme il devrait : le
    /// dire evite de chercher un defaut d'animation la ou il manque un choix.
    /// </summary>
    private static void Substitutions(ForgeArt art, string name)
    {
      if (art.Substituted.Count > 0)
      {
        Log.Info($"[Forge] {name} : poses comblees par la pose debout - "
                 + string.Join(", ", art.Substituted));
      }
    }

    /// <summary>
    /// Construit la voix de l'archer et rend son identifiant de son.
    ///
    /// Chaque action est prise dans la banque WAV si le dessin lui en a assigne une,
    /// et rendue par la voix de repli sinon - action par action, pas en bloc. Un
    /// archer sans aucun fichier a donc deja une voix complete.
    ///
    /// Le repli passe par la surcharge a fonction : elle rend l'objet son du jeu
    /// lui-meme, sans le recharger ni le copier.
    /// </summary>
    private static int Voice(IModRegistry registry, ForgeDesign design, string name)
    {
      try
      {
        CharacterSounds fallback = Sounds.Characters[Bounded(design.VoiceFallback)];
        string prefix = $"forge/{name}/{generation}/";

        var configuration = new CharacterSoundConfiguration
        {
          // READY est le seul son a variantes. Faute d'assignation il rend celui du
          // repli ; un fichier unique suffit a le remplacer, le jeu tirant alors
          // toujours la meme variante.
          Ready = registry.SFXs.RegisterSFXVaried(prefix + "READY",
              () => Varied(design, "READY", fallback.Ready)),

          WallSlide = registry.SFXs.RegisterSFXLooped(prefix + "WALLSLIDE",
              () => Looped(design, "WALLSLIDE_LOOP", fallback.WallSlide)),

          Aim = Sound(registry, design, prefix, "AIM", () => fallback.Aim),
          AimCancel = Sound(registry, design, prefix, "AIM_CANCEL", () => fallback.AimCancel),
          AimDir = Sound(registry, design, prefix, "AIM_DIR", () => fallback.AimDir),
          ArrowGrab = Sound(registry, design, prefix, "ARROW_GRAB", () => fallback.ArrowGrab),
          ArrowRecover = Sound(registry, design, prefix, "ARROW_RECOVER", () => fallback.ArrowRecover),
          ArrowSteal = Sound(registry, design, prefix, "ARROW_STEAL", () => fallback.ArrowSteal),
          Deselect = Sound(registry, design, prefix, "DESELECT", () => fallback.Deselect),
          Die = Sound(registry, design, prefix, "DIE", () => fallback.Die),
          DieBomb = Sound(registry, design, prefix, "DIE_BOMB", () => fallback.DieBomb),
          DieEnv = Sound(registry, design, prefix, "DIE_ENV", () => fallback.DieEnv),
          DieLaser = Sound(registry, design, prefix, "DIE_LASER", () => fallback.DieLaser),
          DieStomp = Sound(registry, design, prefix, "DIE_STOMP", () => fallback.DieStomp),
          Duck = Sound(registry, design, prefix, "DUCK", () => fallback.Duck),
          FireArrow = Sound(registry, design, prefix, "FIRE_ARROW", () => fallback.FireArrow),
          Grab = Sound(registry, design, prefix, "GRAB", () => fallback.Grab),
          Jump = Sound(registry, design, prefix, "JUMP", () => fallback.Jump),
          Land = Sound(registry, design, prefix, "LAND", () => fallback.Land),
          NoFire = Sound(registry, design, prefix, "NOFIRE", () => fallback.NoFire),
          Revive = Sound(registry, design, prefix, "REVIVE", () => fallback.Revive)
        };

        ICharacterSoundEntry voice = registry.CharacterSounds
            .RegisterCharacterSounds(prefix + "voice", configuration);

        return Seat(voice, configuration);
      }
      catch (Exception e)
      {
        // Une voix ratee ne doit pas empecher l'archer d'exister : il parlera avec
        // celle de l'archer de repli, ce que fait deja tout archer forge.
        Log.Error($"[Forge] voix de {name} impossible : {e.Message}");
        return Bounded(design.VoiceFallback);
      }
    }

    /// <summary>
    /// Installe la voix dans Sounds.Characters et rend son identifiant.
    ///
    /// C'est le meme piege que celui des particules, et il se paie au meme endroit :
    /// Sounds.Characters est dimensionne au chargement. FortRise le rallonge une fois
    /// pour toutes avec les voix des mods, mais une voix enregistree APRES - ce que
    /// fait la forge quand elle essaie a chaud - recoit un identifiant qui sort du
    /// tableau. ArcherData.SFX le lit sans filet : le jeu tombe des qu'on valide
    /// l'archer au rollcall, pas a l'enregistrement.
    ///
    /// On ne peut pas rejouer la routine de FortRise : elle recopie toutes les voix
    /// des mods a la suite de la derniere, et un second passage les decalerait, chaque
    /// archer heritant alors de la voix d'un autre. On pose donc la notre a sa place,
    /// ce qui reste juste quel que soit le nombre d'essais.
    /// </summary>
    private static int Seat(ICharacterSoundEntry voice, CharacterSoundConfiguration configuration)
    {
      var sounds = new CharacterSounds
      {
        Ready = configuration.Ready.SFXVaried,
        WallSlide = configuration.WallSlide.SFXLooped,
        Aim = configuration.Aim.BaseSFX,
        AimCancel = configuration.AimCancel.BaseSFX,
        AimDir = configuration.AimDir.BaseSFX,
        ArrowGrab = configuration.ArrowGrab.BaseSFX,
        ArrowRecover = configuration.ArrowRecover.BaseSFX,
        ArrowSteal = configuration.ArrowSteal.BaseSFX,
        Deselect = configuration.Deselect.BaseSFX,
        Die = configuration.Die.BaseSFX,
        DieBomb = configuration.DieBomb.BaseSFX,
        DieEnv = configuration.DieEnv.BaseSFX,
        DieLaser = configuration.DieLaser.BaseSFX,
        DieStomp = configuration.DieStomp.BaseSFX,
        Duck = configuration.Duck.BaseSFX,
        FireArrow = configuration.FireArrow.BaseSFX,
        Grab = configuration.Grab.BaseSFX,
        Jump = configuration.Jump.BaseSFX,
        Land = configuration.Land.BaseSFX,
        NoFire = configuration.NoFire.BaseSFX,
        Revive = configuration.Revive.BaseSFX
      };

      CharacterSounds[] characters = Sounds.Characters;

      if (characters == null)
      {
        return 0;
      }

      if (voice.SFXID >= characters.Length)
      {
        Array.Resize(ref characters, voice.SFXID + 1);
        Sounds.Characters = characters;
      }

      characters[voice.SFXID] = sounds;
      return voice.SFXID;
    }

    /// <summary>Index de voix du jeu, borne : Sounds.Characters n'a pas de filet.</summary>
    private static int Bounded(int id)
    {
      return Sounds.Characters != null && id >= 0 && id < Sounds.Characters.Length ? id : 0;
    }

    private static ISFXEntry Sound(
        IModRegistry registry, ForgeDesign design, string prefix, string action, Func<SFX> fallback)
    {
      string path = ForgeVoice.PathOf(design, action);

      return registry.SFXs.RegisterSFX(
          prefix + action,
          path == null ? fallback : () => FromFile(path));
    }

    /// <summary>
    /// Charge un WAV de la banque. Le flux est ouvert et referme ici : le
    /// constructeur lit tout, le garder ouvert retiendrait le fichier pour rien.
    /// </summary>
    private static SFX FromFile(string path)
    {
      using FileStream stream = File.OpenRead(path);
      return new SFX(stream);
    }

    /// <summary>
    /// Le son a variantes. Un seul fichier assigne donne une seule variante, ce qui
    /// revient a un son ordinaire - c'est le comportement attendu, pas un defaut.
    /// </summary>
    private static SFXVaried Varied(ForgeDesign design, string action, SFXVaried fallback)
    {
      string path = ForgeVoice.PathOf(design, action);

      if (path == null)
      {
        return fallback;
      }

      using FileStream stream = File.OpenRead(path);
      return new SFXVaried(new Stream[] { stream }, 1, true);
    }

    private static SFXLooped Looped(ForgeDesign design, string action, SFXLooped fallback)
    {
      string path = ForgeVoice.PathOf(design, action);

      if (path == null)
      {
        return fallback;
      }

      using FileStream stream = File.OpenRead(path);
      return new SFXLooped(stream, true);
    }

    /// <summary>
    /// Le chapeau, ou l'absence de chapeau.
    ///
    /// Option.None() et non un HatInfo vide : ModArchers ne pose les textures que si
    /// l'option porte une valeur, et un HatInfo aux trois sous-textures nulles
    /// donnerait un chapeau qui existe sans image.
    /// </summary>
    private static Option<HatInfo> HatOf(IModRegistry registry, string prefix, ForgeArt art)
    {
      if (art.Hat == null)
      {
        return Option<HatInfo>.None();
      }

      return new HatInfo
      {
        Normal = Texture(registry, prefix + "hat", art.Hat),
        Red = Texture(registry, prefix + "hat_red", art.HatRed),
        Blue = Texture(registry, prefix + "hat_blue", art.HatBlue)
      };
    }

    private static ISubtextureEntry Texture(
        IModRegistry registry,
        string id,
        ForgeImage image,
        SubtextureAtlasDestination destination = SubtextureAtlasDestination.Atlas)
    {
      if (image == null)
      {
        throw new InvalidOperationException($"image {id} absente");
      }

      // La fabrication est differee : le registre appelle cette fonction au moment ou
      // il en a besoin, et c'est lui qui decide quand. Poser la texture tout de suite
      // marcherait aussi, mais ferait une texture morte a chaque enregistrement
      // avorte.
      return registry.Subtextures.RegisterTexture(id, () => image.ToSubtexture(), destination);
    }

    private static SpriteConfiguration<string> BodyConfig(
        ForgeDesign design,
        ISubtextureEntry texture, ISubtextureEntry red, ISubtextureEntry blue, bool hasHead,
        ForgeFrame frame)
    {
      return new SpriteConfiguration<string>
      {
        Texture = texture,
        RedTexture = red,
        BlueTexture = blue,
        FrameWidth = frame.Width,
        FrameHeight = frame.Height,
        OriginX = frame.OriginX,
        OriginY = frame.OriginY,
        X = 0,
        Y = 8,

        // Obligatoire, et indexe sans borne par l'image courante du corps. Voir
        // ForgeSlots.HeadYOrigins : un tableau trop court fait tomber le jeu en plein
        // match, pas au chargement.
        HeadYOrigins = ForgeSlots.HeadYOrigins(design, hasHead, frame.OriginY),

        // La tete est dessinee dans le corps : la masquer pendant la glissade
        // d'esquive evite d'en poser une seconde par-dessus. C'est ce que font les
        // archers du jeu dont le corps porte deja la tete.
        AdditionalData = new Dictionary<string, object>
        {
          // Masquer la tete pendant la glissade n'a de sens que si le corps la porte
          // deja. Un dessin qui fournit sa propre tete la perdrait a chaque esquive -
          // sauf s'il vient d'un archer qui repond le contraire, ses images de
          // glissade portant deja la tete. Voir ForgeDesign.SlideHead.
          { "SlideHead", (design.SlideHead ?? hasHead) ? "True" : "False" }
        },

        Animations = new[]
        {
          Anim("stand", 0),
          Anim("run", 0.06f, true, 2, 1, 2, 3),
          Anim("jump", 5),
          Anim("fall", 6),
          Anim("glide", 6),
          Anim("ledge", 4),
          Anim("dodge", 0.1f, false, 7),
          Anim("duck", 9),
          // Trois images distinctes, et non trois fois la huitieme : la glissade est
          // le seul moment ou le corps porte le couvre-chef, la tete etant cachee.
          Anim("slide_nohat", 8),
          Anim("slide_normal", 10),
          Anim("slide_crown", 11)
        }
      };
    }

    /// <summary>
    /// La tete : cinq images, ou du vide si le dessin n'en fournit pas.
    ///
    /// Le jeu exige les treize animations meme pointant toutes sur du vide : sans
    /// elles le sprite ne se construit pas, et l'archer tombe a l'apparition.
    ///
    /// Meme cadre que le corps, 24x24, et non les 10x10 des archers d'origine. Les
    /// images de tete se choisissent dans le vivier par la meme fenetre de decoupe que
    /// les poses : les cadrer autrement obligerait a un second reglage de fenetre, et
    /// a deviner ou la tete se trouve dans une planche qu'on ne connait pas.
    ///
    /// OriginY est sans effet : Player l'ecrase a chaque image avec
    /// <c>headYOrigins[bodySprite.CurrentFrame]</c>. C'est donc ce tableau, et lui
    /// seul, qui decide de la hauteur de la tete - voir ForgeSlots.HeadYOrigins.
    /// </summary>
    private static SpriteConfiguration<string> HeadConfig(
        ISubtextureEntry texture, ISubtextureEntry red, ISubtextureEntry blue, ForgeFrame frame)
    {
      return new SpriteConfiguration<string>
      {
        Texture = texture,
        RedTexture = red,
        BlueTexture = blue,
        FrameWidth = frame.Width,
        FrameHeight = frame.Height,
        OriginX = frame.OriginX,
        OriginY = frame.OriginY,
        X = 0,
        Y = 8,
        Animations = new[]
        {
          Anim("idle", 0), Anim("lookUp", 1), Anim("lookDown", 2), Anim("lookBack", 3),
          Anim("idleFall", 0), Anim("lookUpFall", 1), Anim("lookDownFall", 2), Anim("lookBackFall", 3),
          Anim("idleJump", 0), Anim("lookUpJump", 1), Anim("lookDownJump", 2), Anim("lookBackJump", 3),
          Anim("duck", 4)
        }
      };
    }

    private static SpriteConfiguration<string> BowConfig(
        ISubtextureEntry texture, ISubtextureEntry red, ISubtextureEntry blue)
    {
      return new SpriteConfiguration<string>
      {
        Texture = texture,
        RedTexture = red,
        BlueTexture = blue,
        FrameWidth = 12,
        FrameHeight = 10,
        OriginX = 2,
        OriginY = 5,
        X = 0,
        Y = -2,

        // DownY n'a pas de champ dans la configuration : il passe par les donnees
        // libres, que FortRise recopie telles quelles dans le XML du sprite.
        AdditionalData = new Dictionary<string, object> { { "DownY", 1 } },

        Animations = new[]
        {
          Anim("idle", 2),
          Anim("drawn", 0.04f, false, 1, 0),
          Anim("drawnEmpty", 4)
        }
      };
    }

    private static SpriteConfiguration<string> CorpseConfig(
        ISubtextureEntry texture, ISubtextureEntry red, ISubtextureEntry blue, ISubtextureEntry flash,
        ForgeFrame frame)
    {
      return new SpriteConfiguration<string>
      {
        Texture = texture,
        RedTeam = red,
        BlueTeam = blue,
        Flash = flash,
        FrameWidth = frame.Width,
        FrameHeight = frame.Height,
        Animations = new[]
        {
          Anim("ground", 0),
          Anim("fall", 1),
          Anim("pinned", 2),
          Anim("slouched", 3),
          Anim("flying", 4),
          Anim("ledge", 5)
        }
      };
    }

    private static SpriteConfiguration<int> GemConfig(ISubtextureEntry texture)
    {
      return new SpriteConfiguration<int>
      {
        Texture = texture,
        FrameWidth = 10,
        FrameHeight = 10,
        OriginX = 5,
        OriginY = 5,
        Animations = new[]
        {
          new Animation<int>
          {
            ID = 0,
            Frames = new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            Delay = 0.1f,
            Loop = true
          }
        }
      };
    }

    private static SpriteConfiguration<string> MenuGemConfig(ISubtextureEntry texture)
    {
      return new SpriteConfiguration<string>
      {
        Texture = texture,
        FrameWidth = 20,
        FrameHeight = 20,
        OriginX = 10,
        OriginY = 10,
        Y = 60,
        Animations = new[]
        {
          Anim("off", 0),

          // Neuf images qui tournent, puis la premiere repetee une trentaine de fois.
          // Ce n'est pas une maladresse recopiee du jeu : c'est ainsi qu'un sprite
          // fait attendre entre deux scintillements, la boucle etant son seul
          // temporisateur.
          Anim("on", 0.06f, true,
              1, 2, 3, 4, 5, 6, 7, 8, 9,
              1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
              1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1)
        }
      };
    }

    private static Animation<string> Anim(string id, params int[] frames)
    {
      return new Animation<string> { ID = id, Frames = frames, Delay = 0f, Loop = false };
    }

    private static Animation<string> Anim(string id, float delay, bool loop, params int[] frames)
    {
      return new Animation<string> { ID = id, Frames = frames, Delay = delay, Loop = loop };
    }

    /// <summary>
    /// Une ligne de nom jamais vide : le jeu ecrit les deux lignes sans se demander
    /// si elles existent, et une chaine vide y laisse un trou au lieu d'un nom.
    /// </summary>
    private static string Line(string text, string fallback)
    {
      return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim().ToUpperInvariant();
    }

    private static Color Hex(string value, Color fallback)
    {
      try
      {
        return string.IsNullOrWhiteSpace(value) ? fallback : Calc.HexToColor(value.Trim());
      }
      catch
      {
        return fallback;
      }
    }
  }
}
