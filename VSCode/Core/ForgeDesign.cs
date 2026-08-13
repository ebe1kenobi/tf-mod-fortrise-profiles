using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TFModFortRiseArcher
{
  /// <summary>Une image du vivier : la planche dont elle vient, et sa case.</summary>
  public class ForgePick
  {
    /// <summary>Nom du repertoire du vivier, donc de la planche source.</summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Nom du fichier de l'image, sans extension.
    ///
    /// Remplace l'ancien couple ligne/colonne, qui n'etait qu'un detour pour
    /// reconstruire ce meme nom et obligeait chaque planche a declarer sa grille.
    /// </summary>
    public string File { get; set; } = "";

    /// <summary>
    /// De combien ce calque se deplace, en pixels, dans la pose assemblee.
    ///
    /// Compte en DEPLACEMENT DU PERSONNAGE et non en position de fenetre : une
    /// valeur positive pousse l'image vers la droite, ce que la fleche droite fait
    /// donc aussi. L'inverse - deplacer la fenetre - se code plus court d'un signe et
    /// rend un ecran d'alignement ou chaque touche fait le contraire de ce qu'on voit.
    ///
    /// S'ajoute au decalage de la planche, voir <see cref="ForgeDesign.NudgeOf"/>.
    /// Sert aux images qui derivent entre elles a l'interieur d'une meme planche :
    /// deux images de course calees differemment font boiter le personnage, et aucun
    /// reglage d'ensemble ne les rattrape.
    /// </summary>
    public int OffsetX { get; set; }

    public int OffsetY { get; set; }

    /// <summary>
    /// Taille de l'image, en POURCENTAGE de son fichier.
    ///
    /// Absolu et non relatif : redemander 40% deux fois donne la meme image, alors
    /// qu'un reglage relatif reduirait a chaque passage sans qu'on sache plus ou l'on
    /// en est. C'est aussi ce qui permet a l'ecran d'en poser un sur toutes les
    /// images d'un coup sans que celles deja reglees derivent.
    ///
    /// La forge sait desormais quel format un archer du jeu occupe - le cadre orange
    /// des apercus - mais elle ne redimensionne rien d'elle-meme : une image reprise
    /// ailleurs est celle que son auteur a choisie, et c'est a lui de dire de combien
    /// elle doit maigrir.
    /// </summary>
    public int Scale { get; set; } = 100;

    /// <summary>
    /// Pixels retires de chaque bord de l'image, comptes sur le FICHIER et donc
    /// avant la mise a l'echelle.
    ///
    /// Ce qui reste ne bouge pas : le rognage compense le decalage qu'il provoque.
    /// Rogner trois pixels a gauche retire une bordure sans emmener le personnage
    /// avec elle - sinon chaque rognage demanderait un recalage.
    /// </summary>
    public int CropLeft { get; set; }

    public int CropRight { get; set; }
    public int CropTop { get; set; }
    public int CropBottom { get; set; }

    /// <summary>
    /// Miroir gauche-droite de l'image, dans son propre cadre.
    ///
    /// Le premier usage n'est pas l'effet mais la reprise : les archers du jeu sont
    /// dessines tournes vers la DROITE, arc devant eux, et c'est le jeu qui retourne
    /// l'image pour l'autre sens. Une planche prise ailleurs et dessinee vers la
    /// gauche donne donc un archer qui court a reculons, et le seul remede etait de
    /// la retourner dans un editeur avant de la decouper.
    ///
    /// Retourne DANS SON CADRE, sans deplacer le cadre : c'est ce que fait tout
    /// editeur d'image, et cela reste previsible sur une pile de calques. Un
    /// personnage dessine de travers dans sa case se recale ensuite par CALQUE X.
    /// </summary>
    public bool FlipX { get; set; }

    /// <summary>Miroir haut-bas, meme regle.</summary>
    public bool FlipY { get; set; }

    /// <summary>
    /// Rotation de l'image, en degres, dans le sens des aiguilles d'une montre.
    ///
    /// Autour du centre de l'image, le centre restant en place : le cadre s'agrandit
    /// de ce qu'il faut pour que rien ne sorte, et le decalage suit tout seul.
    ///
    /// Les quarts de tour sont exacts - une simple transposition. Les autres angles
    /// passent par le plus proche voisin et abiment forcement un dessin au pixel :
    /// c'est visible, c'est assume, et cela reste utile pour incliner un bras ou
    /// coucher un cadavre.
    /// </summary>
    public int Rotation { get; set; }

    /// <summary>Vrai si l'image est posee telle qu'elle est dans le vivier.</summary>
    [JsonIgnore]
    public bool Untouched =>
        Scale == 100 && CropLeft == 0 && CropRight == 0 && CropTop == 0 && CropBottom == 0
        && !FlipX && !FlipY && Rotation == 0;

    [JsonIgnore]
    public ForgeCell Cell => new ForgeCell(File);

    public static ForgePick Of(string source, ForgeCell cell)
    {
      return new ForgePick { Source = source, File = cell.File };
    }

    public override string ToString()
    {
      return Source + "/" + Cell;
    }
  }

  /// <summary>
  /// Un decalage en pixels, dans le meme sens que <see cref="ForgePick.OffsetX"/>.
  ///
  /// Une classe et non un tuple : System.Text.Json ne serialise pas les champs des
  /// ValueTuple, et un tableau de deux entiers se relit sans dire lequel est lequel.
  /// </summary>
  public class ForgeNudge
  {
    public int X { get; set; }
    public int Y { get; set; }
  }

  /// <summary>
  /// Un archer en cours de forge, tel qu'il est serialise dans Profiles.forge.json.
  ///
  /// Le dessin ne contient aucune image : seulement d'ou chaque pose vient. Les
  /// planches se refabriquent a chaque chargement, ce qui parait couteux et ne l'est
  /// pas - seize decoupes de vingt-quatre pixels - et ce qui evite surtout d'avoir
  /// deux verites, le dessin et les PNG, qui finiraient par diverger. C'est le meme
  /// choix que la table de couleurs des profils, pour la meme raison.
  ///
  /// Meme format additif que ProfileData : System.Text.Json ignore ce qu'il ne
  /// connait pas, un fichier ecrit par une version plus recente reste donc lisible.
  /// </summary>
  public class ForgeDesign
  {
    /// <summary>Identifiant stable, independant du nom.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Nom court, sans espace : il sert d'identifiant de sprite et de nom de fichier
    /// a l'export. Le nom affiche dans le jeu, lui, tient sur les deux lignes.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>Premiere ligne du nom affiche - "INTREPID" chez Brones.</summary>
    public string Name0 { get; set; } = "";

    /// <summary>Seconde ligne du nom affiche - "ARCHAEOLOGIST" chez Brones.</summary>
    public string Name1 { get; set; } = "";

    /// <summary>Couleur principale, en hexadecimal RRGGBB. Elle porte le nom et les fleches.</summary>
    public string ColorA { get; set; } = "8B5A2B";

    /// <summary>Couleur secondaire, en hexadecimal RRGGBB. Elle porte la plupart des effets.</summary>
    public string ColorB { get; set; } = "C08040";

    /// <summary>
    /// Derniere planche dans laquelle on a pris une image, pour y rouvrir.
    ///
    /// Elle ne pre-remplit rien et ne fait foi de rien : c'est un simple raccourci de
    /// navigation. Le selecteur ouvre cette planche quand l'emplacement est encore
    /// vide, ce qui evite de retraverser la liste des planches a chacune des dix-neuf
    /// poses quand elles viennent toutes du meme personnage - le cas ordinaire.
    ///
    /// Le nom est conserve pour relire les dessins ecrits quand ce champ designait la
    /// planche de pre-remplissage : la valeur y a exactement le meme sens utile.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Fenetre de decoupe dans la case source, calee sur le personnage debout.
    ///
    /// Reglable parce que toutes les planches ne calent pas leur personnage au meme
    /// endroit, et parce qu'un pixel d'ecart se voit : le personnage flotte ou
    /// s'enfonce dans le sol.
    /// </summary>
    public int WindowX { get; set; } = ForgeSlots.WindowX;

    public int WindowY { get; set; } = ForgeSlots.WindowY;

    /// <summary>
    /// Recoloration des planches venues du vivier : remplacements et reglages.
    ///
    /// Un seul essai et non une liste nommee comme chez les profils. Les essais y
    /// existent parce qu'un profil change d'archer et que leurs cles sont les teintes
    /// d'origine de CE sprite ; un archer forge n'en a qu'un, le sien, et une liste
    /// n'ajouterait qu'un ecran a traverser.
    ///
    /// Ne touche pas les pieces reprises au jeu, qui gardent <see cref="Hue"/>.
    /// </summary>
    public ColorTrial Colors { get; set; } = new ColorTrial();

    /// <summary>
    /// Teinte appliquee aux pieces reprises du jeu - arc, viseur, ailes, gemmes -
    /// en degres. Broforce n'a pas d'arc, et un arc vert sur un archer brun se
    /// verrait immediatement.
    /// </summary>
    public float Hue { get; set; } = 30f;

    /// <summary>
    /// D'ou vient chaque pose, par cle d'emplacement.
    ///
    /// Forme ancienne, conservee pour relire les dessins faits avant les calques :
    /// une pose n'y a qu'une image. <see cref="Layers"/> la remplace, et
    /// <see cref="Migrate"/> transporte l'une dans l'autre au chargement.
    /// </summary>
    public Dictionary<string, ForgePick> Picks { get; set; } = new Dictionary<string, ForgePick>();

    /// <summary>
    /// Les images qui composent chaque pose, dans l'ordre ou elles se superposent.
    ///
    /// Une pose est rarement une seule image : Broforce dessine les bras sur une
    /// planche a part, parce qu'ils s'animent independamment du corps. Un archer
    /// fabrique a partir du seul corps sort donc manchot, et aucun reglage de
    /// fenetre n'y change quoi que ce soit - l'image n'existe pas dans cette case.
    ///
    /// D'ou l'empilement : la premiere image choisie est le fond, chaque suivante se
    /// pose par-dessus. L'ordre est celui de la selection, et il compte - un bras
    /// derriere le corps n'est pas un bras devant.
    /// </summary>
    public Dictionary<string, List<ForgePick>> Layers { get; set; }
        = new Dictionary<string, List<ForgePick>>();

    /// <summary>
    /// Recalage d'une planche entiere, par nom de planche source.
    ///
    /// Une planche dont le personnage n'est pas pose au meme endroit dans sa case
    /// decale ses dix-neuf poses du meme nombre de pixels. Les corriger une par une
    /// serait un travail de copiste, et le premier reglage de fenetre qu'on toucherait
    /// ensuite le referait entierement.
    ///
    /// Dans le dessin et non dans index.json du vivier : slice_sheets.py regenere ce
    /// fichier et effacerait le reglage. Deux archers peuvent d'ailleurs vouloir de la
    /// meme planche des cadrages differents.
    /// </summary>
    public Dictionary<string, ForgeNudge> SheetNudge { get; set; }
        = new Dictionary<string, ForgeNudge>();

    /// <summary>
    /// Identifiant du dessin dont celui-ci est le costume ALT, ou vide.
    ///
    /// Un costume ALT n'ajoute pas une case au rollcall : il occupe l'emplacement de
    /// son parent et se choisit avec la bascule ALT. C'est ainsi que sont faits les
    /// neuf archers du jeu, et c'est ce qui manquait le plus a la forge.
    ///
    /// Volontairement facultatif : un archer seul reste un archer normal, et rien ne
    /// force a en faire une paire.
    /// </summary>
    public string AltOf { get; set; } = "";

    /// <summary>
    /// De combien la tete descend sur chaque pose du corps, par cle d'emplacement.
    ///
    /// Le jeu accroche la tete a une hauteur qui depend de la pose : chez l'archer
    /// vert 19 debout, 20 sur la premiere image de course, 15 accroupi. C'est ce qui
    /// fait qu'une tete suit le corps au lieu de flotter au-dessus.
    ///
    /// Nos images de tete sont cadrees comme le corps et calees sur la pose debout :
    /// une seule image sert les dix poses. Ce sont donc ces ecarts, et eux seuls, qui
    /// rendent la difference - sans eux un personnage accroupi garde la tete quatre
    /// pixels trop haut.
    ///
    /// Rempli a l'import depuis les tableaux de l'archer repris. Vide, tout vaut
    /// zero : une tete choisie a la main dans le vivier ne bouge pas d'une pose a
    /// l'autre, ce qui est le comportement qu'avait la forge avant.
    /// </summary>
    public Dictionary<string, int> HeadOffsets { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Faut-il dessiner la tete pendant la glissade d'esquive ? Null pour laisser la
    /// forge decider.
    ///
    /// Les archers du jeu repondent non : leurs images de glissade portent deja la
    /// tete, et en poser une seconde par-dessus en ferait deux. Un archer forge
    /// repond oui, parce que sa tete est justement ce qui n'est pas dans son corps.
    /// Un archer importe garde la reponse de son mod - la deviner reviendrait a
    /// parier sur la facon dont ses images ont ete dessinees.
    /// </summary>
    public bool? SlideHead { get; set; }

    /// <summary>
    /// Voix de repli : l'archer du jeu dont les sons comblent ce qui n'est pas
    /// fourni. Zero est le vert - c'est la valeur que tous les archers forges
    /// avaient sans l'avoir choisie.
    /// </summary>
    public int VoiceFallback { get; set; }

    /// <summary>
    /// Fichier de la banque WAV assigne a chaque action, par cle d'action. Ce qui
    /// n'y figure pas est joue avec la voix de repli.
    /// </summary>
    public Dictionary<string, string> Voice { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Musique de victoire, ou vide pour la faire suivre la voix de repli.
    ///
    /// Facultative comme le reste, mais jamais absente a l'arrivee : le jeu lit ce
    /// champ sans le verifier, et vide il fait tomber la fin du round. Voir
    /// <see cref="ForgeMusic"/>.
    /// </summary>
    public string VictoryMusic { get; set; } = ForgeMusic.Auto;

    /// <summary>Vrai si ce dessin est le costume ALT d'un autre.</summary>
    [JsonIgnore]
    public bool IsAlt => !string.IsNullOrEmpty(AltOf);

    /// <summary>
    /// Compte des modifications depuis le chargement.
    ///
    /// Sert uniquement a l'apercu, qui garde les planches assemblees en memoire : il
    /// faut bien qu'il sache quand les refaire. Non serialise - un numero de version
    /// enregistre ne voudrait rien dire d'un lancement a l'autre.
    /// </summary>
    [JsonIgnore]
    public int Revision { get; private set; }

    /// <summary>
    /// A appeler apres toute modification qui change l'image de l'archer : les noms
    /// et les couleurs n'en font pas partie, la fenetre de decoupe et les poses si.
    /// </summary>
    public void Touch()
    {
      Revision++;
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Reprend les dessins ecrits avant les calques : chaque pose y devient un
    /// empilement d'une seule image. A appeler une fois apres la lecture du fichier.
    /// </summary>
    public void Migrate()
    {
      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (Picks == null)
      {
        return;
      }

      foreach (var pair in Picks)
      {
        if (pair.Value != null && !Layers.ContainsKey(pair.Key))
        {
          Layers[pair.Key] = new List<ForgePick> { pair.Value };
        }
      }

      Picks.Clear();
    }

    /// <summary>Les images d'une pose, du fond vers le dessus. Jamais null.</summary>
    public List<ForgePick> LayersOf(string slotKey)
    {
      if (slotKey == null || Layers == null)
      {
        return new List<ForgePick>();
      }

      return Layers.TryGetValue(slotKey, out List<ForgePick> stack) && stack != null
          ? stack
          : new List<ForgePick>();
    }

    /// <summary>
    /// Vrai si le sprite de tete montrera quelque chose.
    ///
    /// Pas seulement "une tete a ete choisie" : un chapeau ou une couronne seuls
    /// suffisent, et c'est meme le cas interessant - un personnage qui porte sa tete
    /// dans son corps ne peut pas perdre un chapeau dessine avec, alors qu'un chapeau
    /// sorti en sprite de tete s'envole.
    /// </summary>
    [JsonIgnore]
    public bool HasHeadArt
    {
      get
      {
        foreach (ForgeSlot slot in ForgeSlots.All)
        {
          bool head = slot.Sheet == ForgeSheet.Head
                      || slot.Sheet == ForgeSheet.HeadNormal
                      || slot.Sheet == ForgeSheet.HeadCrown
                      || slot.Sheet == ForgeSheet.Crown
                      || slot.Key == "hat_normal";

          if (head && PickOf(slot.Key) != null)
          {
            return true;
          }
        }

        return false;
      }
    }

    /// <summary>De combien la tete descend sur cette pose. Zero par defaut.</summary>
    public int HeadOffsetOf(string slotKey)
    {
      HeadOffsets ??= new Dictionary<string, int>();

      return slotKey != null && HeadOffsets.TryGetValue(slotKey, out int offset) ? offset : 0;
    }

    /// <summary>
    /// Fait descendre la tete de quelques pixels sur une pose du corps.
    ///
    /// Un ecart revenu a zero est retire plutot que laisse : le dictionnaire ne garde
    /// que ce qui a ete regle, et un dessin qui n'y a pas touche n'en porte aucune
    /// trace.
    /// </summary>
    public void MoveHead(string slotKey, int delta)
    {
      if (string.IsNullOrEmpty(slotKey))
      {
        return;
      }

      HeadOffsets ??= new Dictionary<string, int>();
      int value = HeadOffsetOf(slotKey) + delta;

      if (value == 0)
      {
        HeadOffsets.Remove(slotKey);
      }
      else
      {
        HeadOffsets[slotKey] = value;
      }

      Touch();
    }

    /// <summary>Le recalage d'une planche. Jamais null : une planche jamais reglee vaut zero.</summary>
    public ForgeNudge NudgeOf(string source)
    {
      SheetNudge ??= new Dictionary<string, ForgeNudge>();

      if (string.IsNullOrEmpty(source)
          || !SheetNudge.TryGetValue(source, out ForgeNudge nudge)
          || nudge == null)
      {
        return new ForgeNudge();
      }

      return nudge;
    }

    /// <summary>
    /// Deplace une planche entiere. Un reglage revenu a zero est retire plutot que
    /// laisse : un dictionnaire d'entrees nulles grossit a chaque planche essayee.
    /// </summary>
    public void Nudge(string source, int dx, int dy)
    {
      if (string.IsNullOrEmpty(source))
      {
        return;
      }

      SheetNudge ??= new Dictionary<string, ForgeNudge>();

      ForgeNudge nudge = NudgeOf(source);
      int x = nudge.X + dx;
      int y = nudge.Y + dy;

      if (x == 0 && y == 0)
      {
        SheetNudge.Remove(source);
      }
      else
      {
        SheetNudge[source] = new ForgeNudge { X = x, Y = y };
      }

      Touch();
    }

    /// <summary>
    /// Ou se pose un calque dans la pose assemblee : son propre decalage plus celui
    /// de sa planche. C'est le seul endroit ou les deux niveaux se rencontrent.
    /// </summary>
    public ForgeNudge PlacementOf(ForgePick pick)
    {
      if (pick == null)
      {
        return new ForgeNudge();
      }

      ForgeNudge sheet = NudgeOf(pick.Source);
      return new ForgeNudge { X = sheet.X + pick.OffsetX, Y = sheet.Y + pick.OffsetY };
    }

    /// <summary>
    /// La premiere image d'une pose, ou null.
    ///
    /// Ce que voient les vignettes et l'apercu de survol, qui n'ont pas besoin de
    /// l'empilement entier pour dire de quelle planche vient la pose.
    /// </summary>
    public ForgePick PickOf(string slotKey)
    {
      List<ForgePick> stack = LayersOf(slotKey);
      return stack.Count == 0 ? null : stack[0];
    }

    /// <summary>Remplace la pose par une seule image, ou la vide si null.</summary>
    public void Set(string slotKey, ForgePick pick)
    {
      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (pick == null)
      {
        Layers.Remove(slotKey);
      }
      else
      {
        Layers[slotKey] = new List<ForgePick> { pick };
      }

      Touch();
    }

    /// <summary>Ajoute une image par-dessus les precedentes.</summary>
    public void AddLayer(string slotKey, ForgePick pick)
    {
      if (pick == null)
      {
        return;
      }

      Layers ??= new Dictionary<string, List<ForgePick>>();

      if (!Layers.TryGetValue(slotKey, out List<ForgePick> stack) || stack == null)
      {
        stack = new List<ForgePick>();
        Layers[slotKey] = stack;
      }

      stack.Add(pick);
      Touch();
    }

    /// <summary>Retire la derniere image posee. La pose se vide avec la premiere.</summary>
    public void RemoveTopLayer(string slotKey)
    {
      if (Layers == null || !Layers.TryGetValue(slotKey, out List<ForgePick> stack) || stack == null)
      {
        return;
      }

      if (stack.Count > 0)
      {
        stack.RemoveAt(stack.Count - 1);
      }

      if (stack.Count == 0)
      {
        Layers.Remove(slotKey);
      }

      Touch();
    }

    /// <summary>Les emplacements encore vides, dans l'ordre de la liste.</summary>
    public List<ForgeSlot> Missing()
    {
      var missing = new List<ForgeSlot>();

      foreach (ForgeSlot slot in ForgeSlots.All)
      {
        if (PickOf(slot.Key) == null)
        {
          missing.Add(slot);
        }
      }

      return missing;
    }

    /// <summary>
    /// Vrai si l'archer peut etre fabrique.
    ///
    /// La pose debout est la seule reellement indispensable : elle sert de portrait,
    /// de statue et de repli pour toute pose manquante. Un archer sans esquive est
    /// laid, un archer sans pose debout n'existe pas.
    /// </summary>
    [JsonIgnore]
    public bool Buildable => !string.IsNullOrWhiteSpace(Name) && PickOf("stand") != null;
  }
}
