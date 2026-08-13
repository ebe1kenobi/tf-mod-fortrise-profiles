using System;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Une image du vivier, designee par son NOM DE FICHIER sans extension.
  ///
  /// Auparavant c'etait un couple ligne/colonne, herite d'un decoupage en grille.
  /// Ce couple n'etait qu'un detour : il servait a reconstruire un nom de fichier,
  /// et obligeait chaque planche a declarer sa grille dans un index.json. Deux
  /// sources de verite pour une meme chose - le fichier et l'index - qui finissaient
  /// par diverger des qu'on relancait un decoupage.
  ///
  /// Le nom du fichier suffit, et il ne peut pas se desynchroniser de lui-meme. Le
  /// vivier se lit donc en listant un repertoire, sans rien d'autre.
  /// </summary>
  public struct ForgeCell : IEquatable<ForgeCell>
  {
    public string File;

    public ForgeCell(string file)
    {
      File = file ?? "";
    }

    public override string ToString()
    {
      return File ?? "";
    }

    /// <summary>
    /// Comparaison insensible a la casse : Windows ne distingue pas deux fichiers
    /// qui ne different que par elle, et un dessin enregistre ne doit pas cesser de
    /// retrouver son image parce qu'on a renomme le repertoire.
    /// </summary>
    public bool Equals(ForgeCell other)
    {
      return string.Equals(File ?? "", other.File ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object obj)
    {
      return obj is ForgeCell other && Equals(other);
    }

    public override int GetHashCode()
    {
      return (File ?? "").ToLowerInvariant().GetHashCode();
    }
  }
}
