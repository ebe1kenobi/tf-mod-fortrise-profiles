namespace TFModFortRiseArcher;

/// <summary>
/// Le SECOND pouvoir choisi dans un profil, pour le mod Power.
///
/// Interface a part, et non un membre de plus sur <see cref="IProfilesPowerApi"/> :
/// l'interop construit son proxy sur la forme des membres, et un appelant qui
/// declare un membre absent de la version installee n'obtient plus rien du tout. Un
/// mod Power anterieur, qui ne connait que le premier pouvoir, continue donc de
/// fonctionner ; celui qui veut le second demande cette interface a part.
/// </summary>
public partial interface IProfilesPower2Api
{
  /// <summary>
  /// Identifiant du second pouvoir du profil rattache a cet emplacement, ou la
  /// chaine vide s'il n'y a pas de profil ou qu'aucun pouvoir n'a ete choisi.
  ///
  /// Ne rend jamais null : l'appelant retombe alors sur son propre defaut.
  /// </summary>
  string GetPlayerPower2(int playerIndex);
}
