using System.Collections.Generic;

namespace TFModFortRiseProfiles
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
    Hat
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

    public static readonly ForgeSlot[] Body =
    {
      new ForgeSlot("stand", ForgeSheet.Body, 0, "DEBOUT"),
      new ForgeSlot("run1", ForgeSheet.Body, 1, "COURSE 1"),
      new ForgeSlot("run2", ForgeSheet.Body, 2, "COURSE 2"),
      new ForgeSlot("run3", ForgeSheet.Body, 3, "COURSE 3"),
      new ForgeSlot("ledge", ForgeSheet.Body, 4, "REBORD"),
      new ForgeSlot("jump", ForgeSheet.Body, 5, "SAUT"),
      new ForgeSlot("fall", ForgeSheet.Body, 6, "CHUTE"),
      new ForgeSlot("dodge", ForgeSheet.Body, 7, "ESQUIVE"),
      new ForgeSlot("slide", ForgeSheet.Body, 8, "GLISSADE"),
      new ForgeSlot("duck", ForgeSheet.Body, 9, "ACCROUPI")
    };

    public static readonly ForgeSlot[] Corpse =
    {
      new ForgeSlot("corpse_ground", ForgeSheet.Corpse, 0, "MORT AU SOL"),
      new ForgeSlot("corpse_fall", ForgeSheet.Corpse, 1, "MORT EN CHUTE"),
      new ForgeSlot("corpse_pinned", ForgeSheet.Corpse, 2, "MORT CLOUE"),
      new ForgeSlot("corpse_slouched", ForgeSheet.Corpse, 3, "MORT AFFAISSE"),
      new ForgeSlot("corpse_flying", ForgeSheet.Corpse, 4, "MORT PROJETE"),
      new ForgeSlot("corpse_ledge", ForgeSheet.Corpse, 5, "MORT SUR REBORD")
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
      new ForgeSlot("hat_normal", ForgeSheet.Hat, 0, "CHAPEAU"),
      new ForgeSlot("hat_blue", ForgeSheet.Hat, 1, "CHAPEAU BLEU"),
      new ForgeSlot("hat_red", ForgeSheet.Hat, 2, "CHAPEAU ROUGE")
    };

    public static readonly ForgeSlot[] All = Concat(Concat(Body, Corpse), Hat);

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
    public static int[] HeadYOrigins()
    {
      // Mesures relevees sur les planches Broforce assemblees : la tete y est deja
      // dessinee dans le corps, ces valeurs ne servent donc qu'a poser la couronne.
      int[] measured = { 21, 21, 20, 21, 23, 21, 21, 18, 21, 18 };

      var origins = new int[Body.Length];

      for (int i = 0; i < origins.Length; i++)
      {
        origins[i] = i < measured.Length ? measured[i] : measured[0];
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
