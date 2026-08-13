namespace TFModFortRiseArcher;

/// <summary>
/// Ce qu'un autre mod peut demander a Profiles.
///
/// <see cref="GetPlayerName"/> et <see cref="SetPlayerName"/> reprennent exactement
/// les signatures de l'ancien <c>ICustomNameModApi</c>. Un mod qui lisait les noms
/// via CustomName n'a donc que le nom du mod a changer dans son appel a
/// <c>Interop.GetApi</c> : l'interop de FortRise construit son proxy sur la forme
/// des membres, l'interface declaree cote appelant peut rester telle quelle.
/// </summary>
public partial interface IProfilesModApi
{
  /// <summary>
  /// Vrai quand Profiles pilote la selection sur l'ecran des archers. Un mod qui lit
  /// le meme bouton doit alors s'abstenir.
  /// </summary>
  bool HandlesRollcall { get; }

  /// <summary>
  /// Nom affiche pour ce joueur : son profil, ou "P1".."P8" a defaut. Ne rend jamais
  /// la chaine vide, pour que l'appelant n'ait pas de repli a prevoir.
  /// </summary>
  string GetPlayerName(int playerIndex);

  /// <summary>
  /// Impose un nom pour ce joueur. Le choix d'un profil sur l'ecran de selection
  /// reprend la main dessus.
  /// </summary>
  void SetPlayerName(int playerIndex, string playerName);

  /// <summary>
  /// Nom du profil rattache a ce joueur, ou la chaine vide s'il n'y en a pas.
  /// A la difference de <see cref="GetPlayerName"/>, ne remplace pas par "P1".
  /// </summary>
  string GetProfileName(int playerIndex);
}
