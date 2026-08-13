using System.Collections.Generic;

namespace TFModFortRiseArcher
{
  /// <summary>La planche de destination d'un emplacement.</summary>
  public enum ForgeSheet
  {
    Body,
    Corpse,

    /// <summary>
    /// Le chapeau qui s'envole. Ce n'est pas une planche mais trois images
    /// independantes, une par camp.
    /// </summary>
    Hat,

    /// <summary>
    /// Tete posee par-dessus le corps, sans couvre-chef. Facultative : voir
    /// ForgeSlots.Head.
    /// </summary>
    Head,

    /// <summary>La meme tete, coiffee. Deduite de la precedente si on ne la choisit pas.</summary>
    HeadNormal,

    /// <summary>La meme tete, couronnee. Le jeu la montre au meneur de la manche.</summary>
    HeadCrown,

    /// <summary>
    /// La couronne, en une image.
    ///
    /// Ce n'est pas une pose mais un ORNEMENT : la forge la pose elle-meme sur les
    /// images de tete et, quand la tete est cachee, sur la glissade. Une image a
    /// fournir au lieu de cinq a redessiner.
    /// </summary>
    Crown
  }

  /// <summary>Un emplacement a remplir, et sa place dans la planche assemblee.</summary>
  public sealed class ForgeSlot
  {
    public readonly string Key;
    public readonly ForgeSheet Sheet;

    /// <summary>Rang dans la planche accolee. C'est l'index que citent les animations.</summary>
    public readonly int Index;

    public readonly string Label;

    internal ForgeSlot(string key, ForgeSheet sheet, int index, string label)
    {
      Key = key;
      Sheet = sheet;
      Index = index;
      Label = label;
    }
  }

  /// <summary>
  /// Les seize images qu'il faut choisir pour faire un archer.
  ///
  /// Un archer TowerFall n'est pas une image mais une vingtaine de planches. La
  /// plupart se deduisent : les variantes d'equipe sont des deplacements de teinte,
  /// le flash une silhouette, les portraits et la statue des agrandissements de la
  /// pose debout. Ne restent a choisir que les poses elles-memes - dix pour le
  /// corps, six pour le cadavre. C'est ce rapport qui rend la forge utilisable :
  /// on ne demande a personne de dessiner un buste de 60x120.
  ///
  /// L'ordre de cette liste est celui que les animations indexent. Le changer
  /// decale tout : "run" pointe sur les images 2,1,2,3 et rien ne le rappelle
  /// ailleurs. Ajouter une pose se fait donc a la fin, jamais au milieu.
  /// </summary>
  public static class ForgeSlots
  {
    // Image TowerFall. 24 et non les 20 des archers d'origine : un personnage
    // Broforce fait 13 pixels de large debout, mais ses bras tendus vont jusqu'a
    // 16, et une planche de 20 les rognait.
    public const int Frame = 24;

    // Case Broforce. Sert de valeur par defaut : le vivier porte la vraie taille
    // dans son index.json, planche par planche.
    public const int SourceCell = 32;

    // Fenetre de decoupe dans la case source, calee sur le personnage debout :
    // pieds a y=30, sommet du chapeau a y=11.
    //
    // Elle reste commune a toutes les poses, et c'est elle qui tient l'alignement
    // d'ensemble : ce sont les CALQUES qui se deplacent sous elle, pas elle qui se
    // deplace d'une pose a l'autre. La distinction n'est pas theorique - une fenetre
    // reglee pose par pose ferait sautiller le personnage au lieu de le faire
    // marcher, alors qu'un calque recale corrige justement ce sautillement.
    public const int WindowX = 3;
    public const int WindowY = 7;

    /// <summary>
    /// Le point d'ancrage DANS la fenetre : celui que le jeu pose sur la position
    /// de l'entite. Toutes les poses s'alignent dessus, et c'est lui, desormais,
    /// que la fenetre sert a reperer - elle ne decoupe plus rien.
    ///
    /// AnchorY au bas de la fenetre : les pieds au sol, donc un personnage plus
    /// grand pousse vers le haut au lieu de s'enfoncer dans le decor.
    ///
    /// AnchorX a deux pixels a droite du centre, et ce n'est pas arbitraire :
    /// l'archer du jeu a son ancre a 8 sur un cadre de 12, soit le centre plus
    /// deux. L'archer est dessine tourne vers la droite, arc devant lui, et le jeu
    /// retourne l'image pour l'autre sens - l'ancre est calee sur le corps et non
    /// sur l'arc, sinon le personnage sauterait de cote a chaque demi-tour.
    /// </summary>
    public const int AnchorX = Frame / 2 + 2;
    public const int AnchorY = Frame;

    /// <summary>
    /// Le cadre d'un archer du JEU, a titre de comparaison.
    ///
    /// C'est ce rectangle que les ecrans dessinent en orange autour d'une pose. Il ne
    /// decoupe rien - le cadre reel se mesure sur les images choisies - il repond a
    /// une seule question, mais la plus utile : de combien ce personnage depasse-t-il
    /// un archer d'origine ? Un dessin deux fois trop grand ne se voit pas autrement
    /// avant d'entrer en partie.
    ///
    /// 12x20 : la taille des neuf archers du jeu. Le rouge fait 14 de large, le seul
    /// a s'en ecarter. L'ancre suit la meme regle que la notre - le bas, et deux
    /// pixels a droite du centre - ce qui donne 8 sur 12, exactement ce que declarent
    /// le vert et le jaune.
    /// </summary>
    public const int VanillaWidth = 12;

    public const int VanillaHeight = 20;
    public const int VanillaAnchorX = VanillaWidth / 2 + 2;
    public const int VanillaAnchorY = VanillaHeight;

    public static readonly ForgeSlot[] Body =
    {
      new ForgeSlot("stand", ForgeSheet.Body, 0, "STAND"),
      new ForgeSlot("run1", ForgeSheet.Body, 1, "RUN 1"),
      new ForgeSlot("run2", ForgeSheet.Body, 2, "RUN 2"),
      new ForgeSlot("run3", ForgeSheet.Body, 3, "RUN 3"),
      new ForgeSlot("ledge", ForgeSheet.Body, 4, "LEDGE"),
      new ForgeSlot("jump", ForgeSheet.Body, 5, "JUMP"),
      new ForgeSlot("fall", ForgeSheet.Body, 6, "FALL"),
      new ForgeSlot("dodge", ForgeSheet.Body, 7, "ESQUIVE"),
      new ForgeSlot("slide", ForgeSheet.Body, 8, "WALLSLIDE LOOP"),
      new ForgeSlot("duck", ForgeSheet.Body, 9, "DUCK"),

      // Le jeu choisit l'image de glissade selon le couvre-chef, et c'est le SEUL
      // endroit ou l'image du corps depend de lui : partout ailleurs, seule la tete
      // change. Ajoutees a la fin, jamais au milieu - les rangs de cette liste sont
      // ceux que les animations citent.
      //
      // Elles ne servent qu'aux archers dont la tete est cachee pendant la glissade,
      // ce que dit SlideHead. Les autres montrent leur tete, donc leur couvre-chef,
      // et les trois images sont alors la meme.
      new ForgeSlot("slide_normal", ForgeSheet.Body, 10, "SLIDE HAT"),
      new ForgeSlot("slide_crown", ForgeSheet.Body, 11, "SLIDE CROWN")
    };

    public static readonly ForgeSlot[] Corpse =
    {
      new ForgeSlot("corpse_ground", ForgeSheet.Corpse, 0, "CORPSE GROUND"),
      new ForgeSlot("corpse_fall", ForgeSheet.Corpse, 1, "CORPSE FALL"),
      new ForgeSlot("corpse_pinned", ForgeSheet.Corpse, 2, "CORPSE PINNED"),
      new ForgeSlot("corpse_slouched", ForgeSheet.Corpse, 3, "CORPSE SLOUCHED"),
      new ForgeSlot("corpse_flying", ForgeSheet.Corpse, 4, "CORPSE FLYING"),
      new ForgeSlot("corpse_ledge", ForgeSheet.Corpse, 5, "CORPSE LEDGE")
    };

    /// <summary>
    /// Le chapeau qui s'envole quand on est touche.
    ///
    /// Entierement facultatif : sans image, l'archer part tete nue et se comporte
    /// comme tous ceux que la forge a produits jusqu'ici. Avec, il retrouve le seul
    /// effet de jeu que les neuf archers du jeu ont et que nous n'avions pas.
    ///
    /// Rien n'est emprunte automatiquement, contrairement a l'arc ou au viseur : un
    /// chapeau se voit, et celui d'un autre archer se reconnait. Qui veut celui du
    /// vert depose ses images dans le vivier et les choisit ici.
    ///
    /// Les variantes d'equipe sont facultatives elles aussi : sans elles, le
    /// chapeau normal est reteinte comme le reste du personnage.
    /// </summary>
    public static readonly ForgeSlot[] Hat =
    {
      new ForgeSlot("hat_normal", ForgeSheet.Hat, 0, "HAT"),
      new ForgeSlot("hat_blue", ForgeSheet.Hat, 1, "HAT BLUE"),
      new ForgeSlot("hat_red", ForgeSheet.Hat, 2, "HAT RED")
    };

    /// <summary>
    /// Les cinq images de tete, dans l'ordre ou le sprite les indexe.
    ///
    /// Facultatives, et c'est tout l'interet : une planche Broforce dessine deja la
    /// tete dans le corps, et un archer forge a partir d'elle n'en veut pas une
    /// seconde. D'autres sources la separent, et sans ces emplacements il n'y avait
    /// aucun moyen de s'en servir - la tete etait cablee vide.
    ///
    /// Cinq et non dix-sept. Les archers du jeu en ont dix-sept : les cinq de base,
    /// puis des variantes de chute - qui alternent sur deux images, la tete ballotte -
    /// et de saut. La forge fait tenir les treize animations sur les cinq de base : on
    /// perd le ballottement, on ne perd pas la tete.
    /// </summary>
    public static readonly ForgeSlot[] Head =
    {
      new ForgeSlot("head_idle", ForgeSheet.Head, 0, "HEAD"),
      new ForgeSlot("head_up", ForgeSheet.Head, 1, "HEAD UP"),
      new ForgeSlot("head_down", ForgeSheet.Head, 2, "HEAD DOWN"),
      new ForgeSlot("head_back", ForgeSheet.Head, 3, "HEAD BACK"),
      new ForgeSlot("head_duck", ForgeSheet.Head, 4, "HEAD DUCK")
    };

    /// <summary>
    /// La tete coiffee, et la tete couronnee.
    ///
    /// Le jeu en veut trois jeux : nue, coiffee, couronnee. Les remplir a la main
    /// ferait quinze images a choisir la ou le dessin n'en change que le sommet,
    /// alors la forge les DEDUIT - la tete nue, plus le chapeau ou la couronne posee
    /// dessus. Ces emplacements ne servent qu'a reprendre la main quand le deduit ne
    /// convient pas : une casquette qui change la silhouette, un archer repris d'un
    /// mod qui fournit ses trois planches.
    ///
    /// Meme rang que la tete nue, image par image : c'est ce qui permet de savoir de
    /// laquelle chacune derive sans table de correspondance.
    /// </summary>
    public static readonly ForgeSlot[] HeadNormal =
    {
      new ForgeSlot("head_hat_idle", ForgeSheet.HeadNormal, 0, "HAT HEAD"),
      new ForgeSlot("head_hat_up", ForgeSheet.HeadNormal, 1, "HAT HEAD UP"),
      new ForgeSlot("head_hat_down", ForgeSheet.HeadNormal, 2, "HAT HEAD DOWN"),
      new ForgeSlot("head_hat_back", ForgeSheet.HeadNormal, 3, "HAT HEAD BACK"),
      new ForgeSlot("head_hat_duck", ForgeSheet.HeadNormal, 4, "HAT HEAD DUCK")
    };

    public static readonly ForgeSlot[] HeadCrown =
    {
      new ForgeSlot("head_crown_idle", ForgeSheet.HeadCrown, 0, "CROWN HEAD"),
      new ForgeSlot("head_crown_up", ForgeSheet.HeadCrown, 1, "CROWN HEAD UP"),
      new ForgeSlot("head_crown_down", ForgeSheet.HeadCrown, 2, "CROWN HEAD DOWN"),
      new ForgeSlot("head_crown_back", ForgeSheet.HeadCrown, 3, "CROWN HEAD BACK"),
      new ForgeSlot("head_crown_duck", ForgeSheet.HeadCrown, 4, "CROWN HEAD DUCK")
    };

    /// <summary>
    /// La couronne, une image, posee par la forge la ou il faut.
    ///
    /// Le chapeau, lui, existe deja : c'est celui qui s'envole. La meme image sert
    /// donc deux fois - portee sur la tete, et lancee en l'air quand on est touche -
    /// ce qui evite d'avoir a la choisir deux fois et de les voir diverger.
    /// </summary>
    public static readonly ForgeSlot[] Crown =
    {
      new ForgeSlot("crown", ForgeSheet.Crown, 0, "CROWN")
    };

    public static readonly ForgeSlot[] All = Concat(Concat(Concat(Concat(Concat(Concat(
        Body, Corpse), Hat), Head), HeadNormal), HeadCrown), Crown);

    /// <summary>
    /// La tete nue dont un etat derive, ou null.
    ///
    /// Par le RANG et non par le nom : les trois jeux sont ranges dans le meme ordre,
    /// et un decoupage de chaine se casserait au premier emplacement renomme.
    /// </summary>
    public static ForgeSlot BaseHeadOf(ForgeSlot slot)
    {
      if (slot == null || (slot.Sheet != ForgeSheet.HeadNormal && slot.Sheet != ForgeSheet.HeadCrown))
      {
        return null;
      }

      return slot.Index >= 0 && slot.Index < Head.Length ? Head[slot.Index] : null;
    }

    /// <summary>L'ornement que porte un etat de tete : le chapeau, la couronne, ou rien.</summary>
    public static string OrnamentOf(ForgeSheet sheet)
    {
      return sheet switch
      {
        ForgeSheet.HeadNormal => "hat_normal",
        ForgeSheet.HeadCrown => "crown",
        _ => null
      };
    }

    /// <summary>
    /// Hauteur a laquelle le jeu accroche la tete, une valeur par image du corps.
    ///
    /// Ce tableau est obligatoire et dangereux, ce qui merite d'etre dit ensemble.
    ///
    /// Obligatoire : Player lit <c>HeadYOrigins</c> sans verifier qu'il existe. Un
    /// sprite de corps qui ne le declare pas fait tomber le jeu a la construction du
    /// joueur, pas plus tard.
    ///
    /// Dangereux : il est indexe par l'image courante du corps, <c>headYOrigins[
    /// bodySprite.CurrentFrame]</c>, sans borne. Son voisin HeadXOrigins, lui, est
    /// garde sur la longueur - pas celui-ci. Un tableau plus court que le nombre
    /// d'images du corps fait donc tomber le jeu pendant le rendu, au moment ou
    /// l'animation atteint l'image en trop : en plein match, sur un saut ou une
    /// esquive, et pas a la selection.
    ///
    /// D'ou ce calcul plutot qu'une liste ecrite en dur a deux endroits : ajouter une
    /// pose au corps allonge le tableau tout seul.
    /// </summary>
    /// <param name="hasHead">
    /// Vrai si le dessin fournit ses propres images de tete. Elles sont cadrees comme
    /// les poses du corps, dans la meme fenetre : pour qu'elles se superposent
    /// exactement, l'origine de la tete doit valoir celle du corps et non la hauteur
    /// relevee d'une nuque. Les autres dessins gardent les mesures, qui n'y servent
    /// qu'a poser la couronne.
    /// </param>
    /// <param name="originY">
    /// L'ancre du cadre, quand le dessin porte sa propre tete.
    ///
    /// Elle vaut Frame par defaut, ce qu'elle a valu tant que toutes les planches
    /// faisaient 24 de haut. Depuis que le cadre est mesure sur les poses choisies,
    /// la constante decalerait la tete de tout l'ecart - et cet appel est le SEUL
    /// endroit ou l'ancre du corps se retrouve ecrite en dur, parce que
    /// <c>Player.UpdateHead</c> ecrase l'origine du sprite de tete a chaque image
    /// avec ce tableau, sans la lire.
    /// </param>
    public static int[] HeadYOrigins(bool hasHead = false, int originY = Frame)
    {
      // Mesures relevees sur les planches Broforce assemblees : la tete y est deja
      // dessinee dans le corps, ces valeurs ne servent donc qu'a poser la couronne.
      int[] measured = hasHead
          ? new[] { originY }
          : new[] { 21, 21, 20, 21, 23, 21, 21, 18, 21, 18 };

      var origins = new int[Body.Length];

      for (int i = 0; i < origins.Length; i++)
      {
        origins[i] = i < measured.Length ? measured[i] : measured[0];
      }

      return origins;
    }

    /// <summary>
    /// La meme chose, en tenant compte de ce que le dessin sait de SA tete.
    ///
    /// Une hauteur unique pour les dix poses donne une tete rigide, qui reste ou elle
    /// est pendant que le corps s'accroupit. Les ecarts releves a l'import la font
    /// suivre - c'est le seul endroit ou ils servent, et c'est pour cela qu'ils sont
    /// gardes.
    /// </summary>
    public static int[] HeadYOrigins(ForgeDesign design, bool hasHead, int originY)
    {
      int[] origins = HeadYOrigins(hasHead, originY);

      if (!hasHead || design == null)
      {
        return origins;
      }

      for (int i = 0; i < origins.Length && i < Body.Length; i++)
      {
        // Une ancre plus BASSE dessine la tete plus haut : on retranche donc l'ecart
        // pour la faire descendre, ce que fait deja l'archer d'origine en declarant
        // 15 accroupi la ou il declare 19 debout.
        origins[i] -= design.HeadOffsetOf(Body[i].Key);
      }

      return origins;
    }

    private static readonly Dictionary<string, ForgeSlot> byKey = BuildIndex();

    public static ForgeSlot Get(string key)
    {
      if (key == null)
      {
        return null;
      }

      return byKey.TryGetValue(key, out ForgeSlot slot) ? slot : null;
    }

    public static ForgeSlot[] Of(ForgeSheet sheet)
    {
      return sheet switch
      {
        ForgeSheet.Body => Body,
        ForgeSheet.Hat => Hat,
        ForgeSheet.Head => Head,
        ForgeSheet.HeadNormal => HeadNormal,
        ForgeSheet.HeadCrown => HeadCrown,
        ForgeSheet.Crown => Crown,
        _ => Corpse
      };
    }

    /// <summary>Largeur de la planche assemblee d'une destination.</summary>
    public static int WidthOf(ForgeSheet sheet)
    {
      return Of(sheet).Length * Frame;
    }

    private static ForgeSlot[] Concat(ForgeSlot[] first, ForgeSlot[] second)
    {
      var all = new ForgeSlot[first.Length + second.Length];
      first.CopyTo(all, 0);
      second.CopyTo(all, first.Length);
      return all;
    }

    private static Dictionary<string, ForgeSlot> BuildIndex()
    {
      var index = new Dictionary<string, ForgeSlot>();

      foreach (ForgeSlot slot in All)
      {
        index[slot.Key] = slot;
      }

      return index;
    }
  }
}
