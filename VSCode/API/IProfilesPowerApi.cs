namespace TFModFortRiseArcher;

/// <summary>
/// Le pouvoir choisi dans un profil, pour le mod Aura.
///
/// Interface a part, et non un membre de plus sur <see cref="IProfilesModApi"/> :
/// l'interop construit son proxy sur la forme des membres, et un appelant qui
/// declare un membre absent de la version installee n'obtient plus rien du tout. Un
/// mod qui ne lit que les noms de joueurs continue donc de fonctionner avec un
/// Profiles anterieur, et celui qui veut les pouvoirs demande cette interface a part
/// - null si la version installee ne la connait pas encore.
/// </summary>
public partial interface IProfilesPowerApi
{
  /// <summary>
  /// Identifiant du pouvoir choisi dans le profil rattache a cet emplacement, ou la
  /// chaine vide s'il n'y a pas de profil ou qu'aucun pouvoir n'a ete choisi.
  ///
  /// Ne rend jamais null : l'appelant retombe alors sur son propre defaut.
  /// </summary>
  string GetPlayerPower(int playerIndex);
}
