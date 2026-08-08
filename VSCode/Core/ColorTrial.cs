using System;
using System.Collections.Generic;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Un essai de couleurs : une tentative nommee, gardee a cote des autres.
  ///
  /// Un essai est attache a un archer et a un costume precis, parce que ses cles sont
  /// les teintes d'origine de ce sprite : les memes valeurs hexadecimales n'existent
  /// pas sur un autre personnage. C'est aussi ce qui permet de changer d'archer sans
  /// rien perdre - les essais de l'ancien restent la, simplement inactifs.
  /// </summary>
  public class ColorTrial
  {
    public string Name { get; set; } = "";

    /// <summary>Nom de l'archer, sous la forme "NAME0 NAME1".</summary>
    public string Archer { get; set; } = "";

    /// <summary><see cref="ProfileCostumes.Normal"/> ou <see cref="ProfileCostumes.Alt"/>.</summary>
    public string Costume { get; set; } = ProfileCostumes.Normal;

    public List<ColorSwap> Palette { get; set; }

    // Reglages d'ensemble, appliques apres le remplacement des couleurs et sur tout
    // le sprite : ils se jugent sur la silhouette entiere, les restreindre a une
    // partie donnerait un personnage incoherent.

    /// <summary>1 = inchange. En dessous, delave ; au-dessus, plus vif.</summary>
    public float Saturation { get; set; } = 1f;

    /// <summary>Decalage de teinte en degres, de -180 a 180. 0 = inchange.</summary>
    public float Hue { get; set; }

    /// <summary>1 = inchange.</summary>
    public float Brightness { get; set; } = 1f;

    /// <summary>1 = inchange. Ecarte ou rapproche les valeurs du gris moyen.</summary>
    public float Contrast { get; set; } = 1f;

    /// <summary>
    /// Vrai si l'essai ne change rien du tout : ni couleur remplacee, ni reglage
    /// d'ensemble. Sert a eviter de fabriquer des textures identiques a l'original.
    /// </summary>
    public bool IsEmpty =>
        (Palette == null || Palette.Count == 0) && !HasAdjustments;

    public bool HasAdjustments =>
        Math.Abs(Saturation - 1f) > 0.001f
        || Math.Abs(Hue) > 0.001f
        || Math.Abs(Brightness - 1f) > 0.001f
        || Math.Abs(Contrast - 1f) > 0.001f;

    public void ResetAdjustments()
    {
      Saturation = 1f;
      Hue = 0f;
      Brightness = 1f;
      Contrast = 1f;
    }
  }

  /// <summary>Quel essai est actif pour un couple archer / costume donne.</summary>
  public class ActiveTrial
  {
    public string Archer { get; set; }
    public string Costume { get; set; }
    public string Name { get; set; }
  }
}
