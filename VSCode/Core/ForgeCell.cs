namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Une case de la planche source : colonne et ligne.
  ///
  /// Le rendu en chaine - "r04c21" - est le nom de fichier que le decoupage donne
  /// aux cases, et non un simple affichage : c'est lui qui retrouve l'image sur le
  /// disque. La ligne avant la colonne, et deux chiffres chacune, parce que c'est la
  /// convention de slice_sheets.py.
  /// </summary>
  public struct ForgeCell
  {
    public int Col;
    public int Row;

    public ForgeCell(int col, int row)
    {
      Col = col;
      Row = row;
    }

    public override string ToString()
    {
      return "r" + Row.ToString("00") + "c" + Col.ToString("00");
    }
  }
}
