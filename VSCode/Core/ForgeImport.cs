using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Une planche d'un archer installe : ou elle est, comment elle se decoupe, ou
  /// tombe son ancre, et quelle animation designe quelle image.
  ///
  /// Tout vient du SpriteData du mod, rien n'est devine. Une planche de vingt
  /// pixels dont l'ancre est a douze se lit dans le fichier ; la deduire de la
  /// largeur ferait entrer l'archer de travers.
  /// </summary>
  public sealed class ForgeImportSheet
  {
    public string Path;
    public int FrameWidth;
    public int FrameHeight;
    public int OriginX;
    public int OriginY;

    /// <summary>Les images de chaque animation, par identifiant d'animation.</summary>
    public Dictionary<string, int[]> Animations = new Dictionary<string, int[]>(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ou le jeu accroche la tete, une valeur par image du CORPS.
    ///
    /// Declares dans la planche du corps et non dans celle de la tete, et c'est
    /// logique : ils disent ou la tete se pose sur chaque pose, pas comment la tete
    /// est dessinee. <c>Player.UpdateHead</c> ecrase l'origine du sprite de tete avec
    /// eux a chaque image.
    /// </summary>
    public int[] HeadX = Array.Empty<int>();

    public int[] HeadY = Array.Empty<int>();

    /// <summary>Vrai si le jeu doit dessiner la tete pendant la glissade d'esquive.</summary>
    public bool SlideHead;

    public bool Valid => Path != null && FrameWidth > 0 && FrameHeight > 0 && File.Exists(Path);

    /// <summary>
    /// L'ancre, en comblant ce qui n'est pas declare par la regle du jeu : les pieds
    /// en bas, deux pixels a droite du centre.
    ///
    /// Les cadavres ne la declarent presque jamais - le jeu ne la leur demande pas -
    /// et il faut pourtant bien les poser quelque part.
    /// </summary>
    public void FillOrigin(ForgeImportSheet like)
    {
      if (OriginX != int.MinValue && OriginY != int.MinValue)
      {
        return;
      }

      // A cadre egal, c'est celle de l'autre planche : c'est le cas exact d'un archer
      // sorti d'ici, dont corps et cadavre sont fabriques dans le meme cadre.
      bool same = like != null
                  && FrameWidth == like.FrameWidth
                  && FrameHeight == like.FrameHeight
                  && like.OriginX != int.MinValue;

      OriginX = same ? like.OriginX : FrameWidth / 2 + 2;
      OriginY = same ? like.OriginY : FrameHeight;
    }
  }

  /// <summary>Un archer trouve dans un mod, candidat a l'import.</summary>
  public sealed class ForgeImportCandidate
  {
    /// <summary>Identifiant de l'archer, tel que son mod le declare.</summary>
    public string Name;

    /// <summary>Repertoire du mod d'ou il vient, pour distinguer deux homonymes.</summary>
    public string Mod;

    public ForgeImportSheet Body;

    /// <summary>Le cadavre, ou null : tous les archers n'en declarent pas.</summary>
    public ForgeImportSheet Corpse;

    /// <summary>
    /// La tete, ou null quand le personnage la porte deja dans son corps.
    ///
    /// Les deux familles existent et se reconnaissent a l'oeil nu : la planche de
    /// tete de Brones fait 50x10 et ne contient pas un seul pixel opaque - c'est un
    /// archer dont le corps porte la tete - alors que celle de l'archer vert en
    /// contient neuf cent soixante-quinze.
    /// </summary>
    public ForgeImportSheet Head;

    /// <summary>La tete coiffee et la tete couronnee, quand le mod les declare.</summary>
    public ForgeImportSheet HeadNormal;

    public ForgeImportSheet HeadCrown;

    /// <summary>
    /// Ce que le mod d'origine repond sur la tete pendant la glissade d'esquive.
    ///
    /// Les archers du jeu repondent non, leurs images de glissade portant deja la
    /// tete. Le deviner reviendrait a parier sur la facon dont ces images ont ete
    /// dessinees - et l'archer vert, qui est le cas type, en ferait deux.
    /// </summary>
    public bool SlideHead;

    public string Name0 = "";
    public string Name1 = "";
    public string ColorA = "";
    public string ColorB = "";

    /// <summary>Repertoire complet du mod, pour aller y chercher les fichiers.</summary>
    public string ModPath;

    /// <summary>
    /// Le motif de la balise SFX - "Content/SFX/CELESTE_{action}.wav" - ou null quand
    /// l'archer n'a pas de voix a lui.
    ///
    /// Un chemin ET un entier peuvent occuper cette balise : l'entier designe la voix
    /// d'un archer du jeu, et rien n'est alors a copier.
    /// </summary>
    public string SfxPattern;

    /// <summary>Voix de repli, celle des sons que le mod ne fournit pas.</summary>
    public int VoiceFallback;

    /// <summary>La balise VictoryMusic telle qu'elle est ecrite, ou vide.</summary>
    public string VictoryMusic = "";

    public override string ToString()
    {
      return Name;
    }
  }

  /// <summary>
  /// Remet sur l'etabli un archer deja fait.
  ///
  /// La forge exporte un mod ; elle sait maintenant le relire. Et pas seulement le
  /// sien : n'importe quel archer installe se reprend. Rien n'est demande a son
  /// auteur, parce que tout ce qu'il faut est deja dans ce que le JEU exige de lui -
  /// archerData.xml dit quelles planches sont les siennes, le SpriteData dit comment
  /// elles se decoupent et quelle image joue quelle animation. L'import ne fait que
  /// lire ce que le jeu lit.
  ///
  /// Les poses entrent dans le vivier comme le reste - un repertoire, un PNG par
  /// pose - et le dessin produit ne fait que les designer. L'archer repris se
  /// retouche donc comme un archer dessine ici : meme alignement, meme recoloration,
  /// meme export. C'est une COPIE : le mod d'origine n'est pas touche, et le
  /// desinstaller ne casse pas ce qui a ete importe.
  ///
  /// La tete demande un mot, parce qu'elle n'est pas une planche du meme genre : le
  /// jeu la pose lui-meme sur le corps, a une hauteur donnee par image, en ecrasant
  /// l'origine de son sprite. Voir <see cref="HeadAnchor"/>, qui reprend cette regle.
  /// Les archers qui portent leur tete dans leur corps - tout ce que la forge exporte -
  /// ont une planche de tete entierement transparente : elle n'apporte alors rien, et
  /// rien n'est importe.
  ///
  /// Ce qui n'est pas repris : les variantes d'equipe, la silhouette, les portraits,
  /// la statue. Ils se deduisent de la planche a la fabrication, et les reprendre
  /// serait garder des images que le prochain export refera de toute facon. La meche
  /// arriere - headBackSprite - ne l'est pas non plus : peu d'archers en ont une, et
  /// la forge n'a pas d'emplacement ou la mettre.
  /// </summary>
  public static class ForgeImport
  {
    /// <summary>
    /// Quelle animation remplit quel emplacement du corps.
    ///
    /// Par le NOM de l'animation et non par le rang de l'image, parce que les rangs
    /// ne sont pas les memes partout : chez les archers du jeu l'image 3 est le
    /// rebord, chez ceux que la forge exporte c'est la troisieme image de course. Le
    /// nom, lui, est le meme des deux cotes - c'est le jeu qui le cherche.
    ///
    /// Plusieurs noms pour un emplacement : la glissade s'appelle differemment selon
    /// que l'archer porte un chapeau, et le premier trouve fait l'affaire.
    /// </summary>
    private static readonly (string Slot, string[] Anims)[] BodyMap =
    {
      ("stand", new[] { "stand" }),
      ("ledge", new[] { "ledge" }),
      ("jump", new[] { "jump" }),
      ("fall", new[] { "fall", "glide" }),
      ("dodge", new[] { "dodge" }),

      // Les trois glissades vont a trois emplacements : c'est le seul endroit ou le
      // corps depend du couvre-chef, et les confondre ferait glisser un archer
      // couronne tete nue.
      ("slide", new[] { "slide_nohat", "slide" }),
      ("slide_normal", new[] { "slide_normal" }),
      ("slide_crown", new[] { "slide_crown" }),
      ("duck", new[] { "duck" })
    };

    /// <summary>Les emplacements de course, remplis par les images de l'animation "run".</summary>
    private static readonly string[] RunSlots = { "run1", "run2", "run3" };

    /// <summary>
    /// Les cinq images de tete, par le nom de leur animation.
    ///
    /// Cinq et non treize : le sprite declare treize animations, mais elles pointent
    /// toutes sur ces cinq images - regarder en haut en tombant et regarder en haut
    /// en sautant sont la meme image.
    /// </summary>
    private static readonly (string Slot, string Anim)[] HeadMap =
    {
      ("head_idle", "idle"),
      ("head_up", "lookUp"),
      ("head_down", "lookDown"),
      ("head_back", "lookBack"),
      ("head_duck", "duck")
    };

    private static readonly (string Slot, string Anim)[] CorpseMap =
    {
      ("corpse_ground", "ground"),
      ("corpse_fall", "fall"),
      ("corpse_pinned", "pinned"),
      ("corpse_slouched", "slouched"),
      ("corpse_flying", "flying"),
      ("corpse_ledge", "ledge")
    };

    /// <summary>Les archers importables installes, tries par nom.</summary>
    public static List<ForgeImportCandidate> Candidates()
    {
      var found = new List<ForgeImportCandidate>();

      try
      {
        string mods = ForgeExport.ModsRoot;

        if (!Directory.Exists(mods))
        {
          return found;
        }

        foreach (string mod in Directory.GetDirectories(mods))
        {
          ReadMod(mod, found);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] recherche d'archers a importer impossible : {e.Message}");
      }

      found.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
      return found;
    }

    /// <summary>
    /// Decoupe les planches d'un archer dans le vivier et rend un dessin qui les
    /// designe, pose par pose. Null si rien n'a pu etre lu.
    /// </summary>
    public static ForgeDesign Import(ForgeImportCandidate candidate)
    {
      if (candidate == null || candidate.Body == null || !candidate.Body.Valid)
      {
        return null;
      }

      // Un repertoire de vivier par archer repris, nomme comme lui : ses poses se
      // survolent et se rechoisissent ensuite comme celles de n'importe quelle
      // planche decoupee a cote.
      string bank = Path.Combine(ForgeBank.Root, candidate.Name);

      var design = new ForgeDesign
      {
        Name = candidate.Name,
        Name0 = candidate.Name0.Length > 0 ? candidate.Name0 : candidate.Name.ToUpperInvariant(),
        Name1 = candidate.Name1,
        Source = candidate.Name,

        // L'ancre du dessin est celle de la planche du corps : c'est elle qui tient
        // l'alignement, la fenetre n'etant plus qu'un moyen de la reperer.
        WindowX = candidate.Body.OriginX - ForgeSlots.AnchorX,
        WindowY = candidate.Body.OriginY - ForgeSlots.AnchorY
      };

      if (candidate.ColorA.Length == 6) { design.ColorA = candidate.ColorA; }
      if (candidate.ColorB.Length == 6) { design.ColorB = candidate.ColorB; }

      try
      {
        Directory.CreateDirectory(bank);

        Dictionary<int, string> bodySlots = BodySlots(candidate.Body);

        if (!Slice(candidate, candidate.Body, bodySlots, bank, design, "frame"))
        {
          return null;
        }

        if (candidate.Head != null && candidate.Head.Valid)
        {
          HeadAnchor(candidate, bodySlots);
          bool any = Slice(candidate, candidate.Head, HeadSlots(candidate.Head, ForgeSheet.Head),
              bank, design, "head");

          // Les deux autres etats gardent l'ancre de la tete nue : ce sont les memes
          // images coiffees, le jeu les pose au meme endroit, et leur donner une ancre
          // a part les ferait sauter d'un etat a l'autre.
          any |= State(candidate, candidate.HeadNormal, ForgeSheet.HeadNormal, bank, design, "hat");
          any |= State(candidate, candidate.HeadCrown, ForgeSheet.HeadCrown, bank, design, "crown");

          if (any)
          {
            HeadMotion(candidate, bodySlots, design);
            design.SlideHead = candidate.SlideHead;
          }
        }

        if (candidate.Corpse != null && candidate.Corpse.Valid)
        {
          // Prefixe distinct : les images sans emplacement sont nommees par leur
          // rang, et les deux planches en ont chacune un cinquieme - sans cela,
          // celles du cadavre ecraseraient celles du corps.
          Slice(candidate, candidate.Corpse, CorpseSlots(candidate.Corpse), bank, design, "corpse");
        }

        // Ce qui s'entend compte autant que ce qui se voit : un archer repris sans sa
        // voix ni sa musique n'est pas le meme personnage. Les deux sont copiees dans
        // les banques du mod, comme les poses le sont dans le vivier - l'archer repris
        // ne depend pas de son mod d'origine, qui peut etre desinstalle ensuite.
        design.VoiceFallback = candidate.VoiceFallback;
        Voice(candidate, design);
        Music(candidate, design);
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] import de {candidate.Name} impossible : {e.Message}");
        return null;
      }

      // Le vivier vient de changer sous nos pieds : sans relecture, les poses tout
      // juste ecrites resteraient introuvables jusqu'a la prochaine entree dans la
      // forge, et le dessin s'ouvrirait vide.
      ForgeBank.Refresh();

      Log.Info($"[Forge] {candidate.Name} importe depuis {candidate.Mod}");
      return design;
    }

    // ------------------------------------------------------------------
    // Voix et musique
    // ------------------------------------------------------------------

    /// <summary>
    /// Recopie dans la banque de sons les WAV que le mod d'origine fournit, et les
    /// rattache action par action.
    ///
    /// Les fichiers gardent leur nom - CELESTE_DIE.wav - plutot que d'etre renommes
    /// d'apres l'action : la banque est commune a tous les archers et a tous les
    /// profils, et un DIE.wav de plus n'y dirait pas de qui il est. C'est l'export qui
    /// renomme, parce que le chargeur l'exige (voir ForgeVoice.ExportName).
    /// </summary>
    private static void Voice(ForgeImportCandidate candidate, ForgeDesign design)
    {
      if (string.IsNullOrEmpty(candidate.SfxPattern) || candidate.ModPath == null)
      {
        return;
      }

      int taken = 0;

      foreach (ForgeVoiceAction action in ForgeVoice.Actions)
      {
        string relative = candidate.SfxPattern.Replace("{action}", action.Key);

        // Les sons a variantes sont numerotes sur le disque. La forge n'en garde
        // qu'un par action : c'est le premier, et les autres variantes du mod
        // d'origine sont laissees - une limite de la forge, pas un oubli d'ici.
        if (action.Varied)
        {
          relative = Path.ChangeExtension(relative, null) + "_01.wav";
        }

        string source = Path.Combine(candidate.ModPath,
            relative.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(source))
        {
          continue;
        }

        try
        {
          Directory.CreateDirectory(ProfileSfx.PoolDir);
          string file = Path.GetFileName(source);
          File.Copy(source, Path.Combine(ProfileSfx.PoolDir, file), true);
          ForgeVoice.Assign(design, action.Key, file);
          taken++;
        }
        catch (Exception e)
        {
          Log.Error($"[Forge] {Path.GetFileName(source)} non repris : {e.Message}");
        }
      }

      if (taken > 0)
      {
        // La banque vient de changer : sans relecture, les WAV tout juste copies
        // n'apparaitraient pas dans l'ecran des sons.
        ProfileSfx.RefreshPool();
        Log.Info($"[Forge] {candidate.Name} : {taken} sons repris");
      }
    }

    /// <summary>
    /// Recopie la musique de victoire.
    ///
    /// Elle se cite de trois facons : le nom d'une piste du jeu ("Green"), un renvoi a
    /// une piste du mod ("@VictoryCeleste"), ou un chemin complet. Seules les deux
    /// dernieres designent un fichier a copier ; la premiere se garde telle quelle.
    /// </summary>
    private static void Music(ForgeImportCandidate candidate, ForgeDesign design)
    {
      string music = candidate.VictoryMusic;

      if (string.IsNullOrEmpty(music) || candidate.ModPath == null)
      {
        return;
      }

      if (ForgeMusic.IsKnown(music))
      {
        design.VictoryMusic = music;
        return;
      }

      string source = FindMusic(candidate.ModPath, music.TrimStart('@'));

      if (source == null)
      {
        Log.Info($"[Forge] musique '{music}' de {candidate.Name} introuvable, laissee en AUTO");
        return;
      }

      try
      {
        Directory.CreateDirectory(ForgeMusic.BankDir);
        string file = Path.GetFileName(source);
        File.Copy(source, Path.Combine(ForgeMusic.BankDir, file), true);
        design.VictoryMusic = ForgeMusic.FilePrefix + file;
        Log.Info($"[Forge] {candidate.Name} : musique {file} reprise");
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] musique de {candidate.Name} non reprise : {e.Message}");
      }
    }

    /// <summary>
    /// Le fichier que designe une balise VictoryMusic : soit le chemin y est ecrit en
    /// entier, soit c'est un nom de piste, que le chargeur de mods va chercher dans
    /// Content/Music.
    /// </summary>
    private static string FindMusic(string mod, string name)
    {
      var tries = new List<string>
      {
        name,
        "Content/Music/" + name + ".wav",
        "Content/Music/" + name + ".ogg"
      };

      foreach (string relative in tries)
      {
        string path = Path.Combine(mod, relative.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(path))
        {
          return path;
        }
      }

      return null;
    }

    // ------------------------------------------------------------------
    // Correspondance image / emplacement
    // ------------------------------------------------------------------

    /// <summary>
    /// Quelle image de la planche va dans quel emplacement, par le nom des
    /// animations. Les images qu'aucune animation connue ne cite restent sans
    /// emplacement : elles seront ecrites dans le vivier quand meme, sous un nom
    /// numerote, et pourront etre choisies a la main.
    /// </summary>
    private static Dictionary<int, string> BodySlots(ForgeImportSheet sheet)
    {
      var slots = new Dictionary<int, string>();

      foreach (var entry in BodyMap)
      {
        foreach (string anim in entry.Anims)
        {
          int[] frames = Frames(sheet, anim);

          if (frames.Length > 0)
          {
            Claim(slots, frames[0], entry.Slot);
            break;
          }
        }
      }

      // La course cite ses images dans l'ordre du pas - "2,1,2,3" chez les uns,
      // "2,1,1,1,2" chez les autres - avec des repetitions. On garde les images
      // distinctes dans l'ordre des rangs : c'est le seul ordre qui soit le meme
      // partout, et celui dans lequel la forge les range deja.
      var run = new List<int>();

      foreach (int frame in Frames(sheet, "run"))
      {
        if (!run.Contains(frame))
        {
          run.Add(frame);
        }
      }

      run.Sort();

      for (int i = 0; i < run.Count && i < RunSlots.Length; i++)
      {
        Claim(slots, run[i], RunSlots[i]);
      }

      return slots;
    }

    private static Dictionary<int, string> CorpseSlots(ForgeImportSheet sheet)
    {
      return Named(sheet, CorpseMap);
    }

    /// <summary>
    /// Les emplacements d'un etat de tete : les memes animations, l'etat en plus.
    /// </summary>
    private static Dictionary<int, string> HeadSlots(ForgeImportSheet sheet, ForgeSheet state)
    {
      ForgeSlot[] slots = ForgeSlots.Of(state);
      var map = new (string, string)[HeadMap.Length];

      for (int i = 0; i < HeadMap.Length; i++)
      {
        map[i] = (slots[i].Key, HeadMap[i].Anim);
      }

      return Named(sheet, map);
    }

    /// <summary>
    /// Reprend un etat de tete, s'il est declare et s'il differe de la tete nue.
    ///
    /// Un mod qui declare trois planches identiques - cela arrive - n'a rien a nous
    /// apprendre : la forge deduirait la meme chose de la tete nue et de l'ornement,
    /// et garder trois copies encombrerait le vivier pour rien. On les reprend quand
    /// meme : les comparer demanderait de charger les deux planches, et une difference
    /// d'un pixel compte autant qu'une casquette.
    /// </summary>
    private static bool State(
        ForgeImportCandidate candidate, ForgeImportSheet sheet, ForgeSheet state,
        string bank, ForgeDesign design, string prefix)
    {
      if (sheet == null || !sheet.Valid)
      {
        return false;
      }

      // L'ancre est celle de la tete nue : ce sont les memes images, coiffees.
      sheet.OriginX = candidate.Head.OriginX;
      sheet.OriginY = candidate.Head.OriginY;

      return Slice(candidate, sheet, HeadSlots(sheet, state), bank, design, prefix);
    }

    /// <summary>Un emplacement par animation nommee, la premiere image de chacune.</summary>
    private static Dictionary<int, string> Named(ForgeImportSheet sheet, (string Slot, string Anim)[] map)
    {
      var slots = new Dictionary<int, string>();

      foreach (var entry in map)
      {
        int[] frames = Frames(sheet, entry.Anim);

        if (frames.Length > 0)
        {
          Claim(slots, frames[0], entry.Slot);
        }
      }

      return slots;
    }

    /// <summary>
    /// Attribue une image a un emplacement, sauf si elle en a deja un.
    ///
    /// Une meme image sert souvent a deux animations - la chute et le vol plane chez
    /// les archers du jeu. La premiere l'emporte, et l'autre emplacement restera
    /// vide : la fabrication le comblera avec la pose debout, ce qui est exactement
    /// ce que fait un archer qui n'a pas cette image.
    /// </summary>
    private static void Claim(Dictionary<int, string> slots, int frame, string slot)
    {
      if (frame >= 0 && !slots.ContainsKey(frame))
      {
        slots[frame] = slot;
      }
    }

    private static int[] Frames(ForgeImportSheet sheet, string anim)
    {
      return sheet.Animations.TryGetValue(anim, out int[] frames) ? frames : Array.Empty<int>();
    }

    /// <summary>
    /// Donne a la planche de tete l'ancre que le JEU lui impose, et non celle qu'elle
    /// declare.
    ///
    /// <c>Player.UpdateHead</c> ecrit, a chaque image :
    ///
    /// <code>
    /// Origin.Y = headYOrigins[image du corps]                 // toujours
    /// Origin.X = headXOrigins[image du corps]                 // si le tableau est assez long
    /// </code>
    ///
    /// L'origine declaree dans le SpriteData de la tete ne sert donc qu'en X, et
    /// seulement quand l'archer ne fournit pas le tableau. C'est cette ancre-la qu'il
    /// faut donner a l'import, sans quoi la tete se poserait ou elle est dessinee dans
    /// sa case plutot que sur le cou.
    ///
    /// On prend les valeurs de la pose DEBOUT. Le jeu en a une par pose - chez
    /// l'archer vert 19,20,18,19,19,19,18,18,18,18 - la notre en fige une par image
    /// de tete : le debout tombe juste, les autres a un ou deux pixels pres, que
    /// CALQUE X/Y rattrape. Garder la variation demanderait de sortir la tete du
    /// cadre du corps, donc de renoncer a la regler comme le reste.
    /// </summary>
    private static void HeadAnchor(ForgeImportCandidate candidate, Dictionary<int, string> bodySlots)
    {
      ForgeImportSheet head = candidate.Head;
      ForgeImportSheet body = candidate.Body;

      int stand = Stand(bodySlots);

      // A defaut de tableau, l'ancre declaree par la planche de tete - c'est
      // exactement ce que le jeu retient dans ce cas.
      head.FillOrigin(null);

      if (stand >= 0 && stand < body.HeadX.Length)
      {
        head.OriginX = body.HeadX[stand];
      }

      if (stand >= 0 && stand < body.HeadY.Length)
      {
        head.OriginY = body.HeadY[stand];
      }
    }

    /// <summary>
    /// Releve de combien la tete descend sur chaque pose, par rapport au debout.
    ///
    /// Notre image de tete est calee sur la pose debout et sert les dix poses : sans
    /// ces ecarts, elle resterait ou elle est pendant que le corps s'accroupit. Chez
    /// l'archer vert l'accroupi descend de quatre pixels et la course monte et
    /// descend d'un - c'est exactement ce qui donne une tete vivante.
    ///
    /// Ce sont des ECARTS et non les valeurs de l'archer d'origine : les siennes se
    /// comptent depuis sa propre ancre, les notres depuis celle de notre cadre. Seul
    /// l'ecart se transporte d'un repere a l'autre.
    /// </summary>
    private static void HeadMotion(
        ForgeImportCandidate candidate, Dictionary<int, string> bodySlots, ForgeDesign design)
    {
      int[] heights = candidate.Body.HeadY;

      if (heights.Length == 0)
      {
        return;
      }

      int stand = Stand(bodySlots);

      if (stand >= heights.Length)
      {
        return;
      }

      foreach (var entry in bodySlots)
      {
        if (entry.Key >= heights.Length)
        {
          continue;
        }

        int offset = heights[stand] - heights[entry.Key];

        if (offset != 0)
        {
          design.HeadOffsets[entry.Value] = offset;
        }
      }
    }

    /// <summary>Le rang de l'image de la pose debout dans la planche du corps.</summary>
    private static int Stand(Dictionary<int, string> bodySlots)
    {
      foreach (var entry in bodySlots)
      {
        if (entry.Value == "stand")
        {
          return entry.Key;
        }
      }

      return 0;
    }

    // ------------------------------------------------------------------
    // Lecture des mods
    // ------------------------------------------------------------------

    /// <summary>
    /// Les archers d'un mod.
    ///
    /// On part d'archerData.xml et non des planches : c'est lui qui dit ce qu'est un
    /// archer, quel sprite est son corps et quel autre est son cadavre. Chercher des
    /// PNG sous <c>sprites/player</c> retrouverait les memes images, mais sous le nom
    /// du sprite - "GCBody" au lieu de "GreenClone" - et sans savoir lequel des huit
    /// fichiers du repertoire est le personnage.
    /// </summary>
    private static void ReadMod(string mod, List<ForgeImportCandidate> found)
    {
      string atlas = Path.Combine(mod, "Content", "Atlas");
      string archerFile = Path.Combine(atlas, "GameData", "archerData.xml");

      if (!File.Exists(archerFile))
      {
        return;
      }

      Dictionary<string, ForgeImportSheet> sheets = ReadSpriteData(mod, atlas);

      try
      {
        XElement root = XDocument.Load(archerFile).Root;

        if (root == null)
        {
          return;
        }

        foreach (XElement archer in root.Elements())
        {
          // Tout ce qui finit par "Archer" en est un : Archer, AltArcher pour le
          // costume, SecretArcher pour celui qu'on debloque. Ils declarent tous leurs
          // planches de la meme facon, et c'est souvent un costume qu'on vient
          // reprendre. Enumerer les trois noms connus ferait manquer le quatrieme.
          if (!archer.Name.LocalName.EndsWith("Archer", StringComparison.Ordinal))
          {
            continue;
          }

          string id = (string)archer.Attribute("id");
          ForgeImportSheet body = Sheet(sheets, archer.Element("Sprites"), "Body");

          if (string.IsNullOrEmpty(id) || body == null || !body.Valid)
          {
            continue;
          }

          body.FillOrigin(null);

          ForgeImportSheet corpse = Sheet(sheets, archer, "Corpse");
          corpse?.FillOrigin(body);

          // La tete sans chapeau : c'est celle que le jeu montre a un archer qui a
          // perdu le sien, et celle que la forge sait poser. La tete a couronne est
          // la meme avec un ornement, et HeadNormal celle d'un archer chapeaute -
          // reprendre les trois donnerait trois fois le meme visage.
          XElement sprites = archer.Element("Sprites");
          ForgeImportSheet head = Sheet(sheets, sprites, "HeadNoHat")
                                  ?? Sheet(sheets, sprites, "HeadNormal");

          ForgeImportSheet headNormal = Sheet(sheets, sprites, "HeadNormal");
          ForgeImportSheet headCrown = Sheet(sheets, sprites, "HeadCrown");

          // La voix : un chemin a motif, ou l'index d'un archer du jeu. Les deux
          // formes vivent dans la meme balise, et on les distingue comme le chargeur
          // le fait - un entier n'est pas un chemin.
          string sfx = Text(archer, "SFX").Trim();
          bool sfxIsPath = sfx.Length > 0 && !int.TryParse(sfx, out int _);

          if (!int.TryParse(Text(archer, "SFXFallback").Trim(), out int fallback) && !sfxIsPath)
          {
            int.TryParse(sfx, out fallback);
          }

          found.Add(new ForgeImportCandidate
          {
            Name = id,
            Mod = Path.GetFileName(mod),
            ModPath = mod,
            SfxPattern = sfxIsPath ? sfx : null,
            VoiceFallback = fallback,
            VictoryMusic = Text(archer, "VictoryMusic").Trim(),
            Body = body,
            Corpse = corpse != null && corpse.Valid ? corpse : null,
            Head = head != null && head.Valid ? head : null,
            HeadNormal = headNormal != null && headNormal.Valid ? headNormal : null,
            HeadCrown = headCrown != null && headCrown.Valid ? headCrown : null,
            SlideHead = body.SlideHead,
            Name0 = Text(archer, "Name0"),
            Name1 = Text(archer, "Name1"),
            ColorA = Text(archer, "ColorA").TrimStart('#'),
            ColorB = Text(archer, "ColorB").TrimStart('#')
          });
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] archerData.xml de {Path.GetFileName(mod)} illisible : {e.Message}");
      }
    }

    /// <summary>
    /// La planche citee par un champ d'archerData, ou null.
    ///
    /// Les renvois y sont prefixes d'un arobase - <c>@BronesCorpse</c> - qui designe
    /// une entree du SpriteData plutot qu'un chemin de fichier.
    /// </summary>
    private static ForgeImportSheet Sheet(
        Dictionary<string, ForgeImportSheet> sheets, XElement parent, string field)
    {
      if (parent == null)
      {
        return null;
      }

      string reference = Text(parent, field).TrimStart('@');

      return reference.Length > 0 && sheets.TryGetValue(reference, out ForgeImportSheet sheet)
          ? sheet
          : null;
    }

    /// <summary>
    /// Toutes les planches declarees par un mod, par identifiant.
    ///
    /// Les fichiers sont lus sans distinction : le chargeur du jeu, lui, tient a ce
    /// qu'un cadavre soit dans corpseSpriteData.xml et une gemme dans
    /// menuSpriteData.xml, mais nous ne cherchons qu'un identifiant, et un
    /// dictionnaire commun evite d'avoir a savoir ou chacun range ses declarations.
    /// </summary>
    private static Dictionary<string, ForgeImportSheet> ReadSpriteData(string mod, string atlas)
    {
      var sheets = new Dictionary<string, ForgeImportSheet>(StringComparer.OrdinalIgnoreCase);
      string dir = Path.Combine(atlas, "SpriteData");

      if (!Directory.Exists(dir))
      {
        return sheets;
      }

      foreach (string file in Directory.GetFiles(dir, "*.xml"))
      {
        try
        {
          XElement root = XDocument.Load(file).Root;

          if (root == null)
          {
            continue;
          }

          foreach (XElement sprite in root.Elements())
          {
            string id = (string)sprite.Attribute("id");
            string texture = Text(sprite, "Texture").Replace('\\', '/');

            if (string.IsNullOrEmpty(id) || texture.Length == 0 || sheets.ContainsKey(id))
            {
              continue;
            }

            var sheet = new ForgeImportSheet
            {
              Path = Path.Combine(mod, texture.Replace('/', Path.DirectorySeparatorChar)),
              FrameWidth = Number(sprite, "FrameWidth", 0),
              FrameHeight = Number(sprite, "FrameHeight", 0),
              OriginX = Number(sprite, "OriginX", int.MinValue),
              OriginY = Number(sprite, "OriginY", int.MinValue)
            };

            ReadAnimations(sprite, sheet);
            sheet.HeadX = Numbers(sprite, "HeadXOrigins");
            sheet.HeadY = Numbers(sprite, "HeadYOrigins");
            sheet.SlideHead = string.Equals(
                Text(sprite, "SlideHead"), "True", StringComparison.OrdinalIgnoreCase);
            sheets[id] = sheet;
          }
        }
        catch (Exception e)
        {
          Log.Error($"[Forge] {Path.GetFileName(file)} illisible : {e.Message}");
        }
      }

      return sheets;
    }

    private static void ReadAnimations(XElement sprite, ForgeImportSheet sheet)
    {
      XElement animations = sprite.Element("Animations");

      if (animations == null)
      {
        return;
      }

      foreach (XElement anim in animations.Elements("Anim"))
      {
        string id = (string)anim.Attribute("id");
        string frames = (string)anim.Attribute("frames");

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(frames))
        {
          continue;
        }

        var indices = new List<int>();

        foreach (string part in frames.Split(','))
        {
          if (int.TryParse(part.Trim(), out int index))
          {
            indices.Add(index);
          }
        }

        if (indices.Count > 0 && !sheet.Animations.ContainsKey(id))
        {
          sheet.Animations[id] = indices.ToArray();
        }
      }
    }

    // ------------------------------------------------------------------
    // Decoupe
    // ------------------------------------------------------------------

    /// <summary>
    /// Coupe une bande en ses images, les ecrit dans le vivier et rattache chacune a
    /// son emplacement.
    ///
    /// Toutes les images sont ecrites, y compris celles qu'aucun emplacement ne
    /// reclame : une planche plus riche que nos seize poses ne doit pas perdre ce
    /// qu'elle a en plus, on peut vouloir aller le chercher a la main.
    /// </summary>
    private static bool Slice(
        ForgeImportCandidate candidate, ForgeImportSheet sheet, Dictionary<int, string> slots,
        string bank, ForgeDesign design, string prefix)
    {
      Texture2D texture = null;

      try
      {
        using FileStream stream = File.OpenRead(sheet.Path);
        texture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, stream);

        if (texture == null)
        {
          return false;
        }

        // Une planche est une GRILLE et pas une bande : les images se lisent de
        // gauche a droite puis de haut en bas, et c'est ainsi que les animations les
        // numerotent. L'archer vert range ses huit images de glissade sur la
        // troisieme rangee - les chercher sur une seule ligne les perdrait, et la
        // planche ferait douze images au lieu de quarante-huit.
        int columns = texture.Width / sheet.FrameWidth;
        int rows = texture.Height / sheet.FrameHeight;
        int count = columns * rows;

        if (count == 0)
        {
          Log.Error($"[Forge] {Path.GetFileName(sheet.Path)} fait {texture.Width}x{texture.Height} "
                    + $"pour des images de {sheet.FrameWidth}x{sheet.FrameHeight} : bande illisible");
          return false;
        }

        var whole = new Color[texture.Width * texture.Height];
        texture.GetData(whole);

        // Ce qu'on a reellement tire de la planche : une planche entierement
        // transparente n'est pas un echec de lecture, mais elle ne donne rien, et
        // l'appelant doit pouvoir faire la difference avec une planche posee.
        int written = 0;

        // Le decalage qui ramene l'ancre de CETTE planche sur celle du dessin. Nul
        // pour le corps, dont l'ancre a justement servi a regler la fenetre ; non nul
        // pour un cadavre cadre autrement, qui se poserait sinon de travers.
        int offsetX = candidate.Body.OriginX - sheet.OriginX;
        int offsetY = candidate.Body.OriginY - sheet.OriginY;

        for (int index = 0; index < count; index++)
        {
          string slot = slots.TryGetValue(index, out string key) ? key : null;
          string file = slot ?? $"{prefix}{index:00}";

          Color[] frame = Frame(whole, texture.Width, sheet, index, columns);

          // Une image entierement transparente n'est pas une pose : on ne l'ecrit pas
          // et on ne l'attribue a rien. C'est ce qui fait que la planche de tete d'un
          // archer qui porte la sienne dans son corps - cinquante pixels sur dix, pas
          // un seul opaque, le cas de tout ce que la forge exporte - n'ajoute rien du
          // tout, plutot que cinq images vides qui feraient croire a une tete.
          if (Empty(frame))
          {
            continue;
          }

          Write(frame, sheet.FrameWidth, sheet.FrameHeight, Path.Combine(bank, file + ".png"));
          written++;

          if (slot == null)
          {
            continue;
          }

          design.Set(slot, new ForgePick
          {
            Source = candidate.Name,
            File = file,
            OffsetX = offsetX,
            OffsetY = offsetY
          });
        }

        return written > 0;
      }
      catch (Exception e)
      {
        Log.Error($"[Forge] {Path.GetFileName(sheet.Path)} illisible : {e.Message}");
        return false;
      }
      finally
      {
        try { texture?.Dispose(); } catch { }
      }
    }

    private static bool Empty(Color[] frame)
    {
      foreach (Color pixel in frame)
      {
        if (pixel.A != 0)
        {
          return false;
        }
      }

      return true;
    }

    private static Color[] Frame(
        Color[] whole, int stride, ForgeImportSheet sheet, int index, int columns)
    {
      var frame = new Color[sheet.FrameWidth * sheet.FrameHeight];
      int left = index % columns * sheet.FrameWidth;
      int top = index / columns * sheet.FrameHeight;

      for (int y = 0; y < sheet.FrameHeight; y++)
      {
        Array.Copy(whole, (top + y) * stride + left, frame, y * sheet.FrameWidth, sheet.FrameWidth);
      }

      return frame;
    }

    private static void Write(Color[] pixels, int width, int height, string path)
    {
      Texture2D texture = null;

      try
      {
        texture = new Texture2D(Engine.Instance.GraphicsDevice, width, height);
        texture.SetData(pixels);

        using FileStream stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
      }
      finally
      {
        try { texture?.Dispose(); } catch { }
      }
    }

    // ------------------------------------------------------------------

    private static string Text(XElement parent, string name)
    {
      XElement child = parent.Element(name);
      return child == null ? "" : child.Value.Trim();
    }

    private static int Number(XElement parent, string name, int fallback)
    {
      string text = Text(parent, name);
      return int.TryParse(text, out int value) ? value : fallback;
    }

    /// <summary>Une liste d'entiers separes par des virgules, ou un tableau vide.</summary>
    private static int[] Numbers(XElement parent, string name)
    {
      string text = Text(parent, name);

      if (text.Length == 0)
      {
        return Array.Empty<int>();
      }

      var values = new List<int>();

      foreach (string part in text.Split(','))
      {
        if (int.TryParse(part.Trim(), out int value))
        {
          values.Add(value);
        }
      }

      return values.ToArray();
    }
  }
}
