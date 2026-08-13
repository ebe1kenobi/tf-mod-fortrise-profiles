namespace TFModFortRiseArcher;

/// <summary>
/// La liste des profils, pour les mods qui composent leur propre roster.
///
/// Volontairement separee de <see cref="IProfilesModApi"/> plutot qu'ajoutee dedans :
/// l'interop construit son proxy sur la forme des membres, et un appelant qui declare
/// un membre absent de la version installee n'obtient plus rien du tout. Un mod qui
/// n'utilise que les noms de joueurs continue donc de fonctionner avec un Profiles
/// anterieur, et celui qui veut le roster demande cette interface a part - null si la
/// version installee ne la connait pas encore.
/// </summary>
public partial interface IProfilesRosterApi
{
  /// <summary>
  /// Noms des profils enregistres, dans l'ordre du fichier. Jamais null : une
  /// installation sans profil rend un tableau vide.
  /// </summary>
  string[] GetProfileNames();

  /// <summary>
  /// Rattache un profil a un emplacement joueur, comme le fait l'ecran de selection
  /// des archers. C'est ce rattachement, et lui seul, qui fait suivre les couleurs,
  /// les sons et les images : imposer le nom ne suffit pas.
  ///
  /// Un nom vide ou inconnu detache l'emplacement et rend false - indispensable pour
  /// qu'un profil ne survive pas au joueur du match precedent.
  /// </summary>
  bool AssignProfile(int playerIndex, string profileName);

  /// <summary>
  /// Archer prefere d'un profil, ou -1 si le nom est inconnu.
  ///
  /// Un mod qui choisit lui-meme les archers en a besoin : les couleurs d'un profil
  /// sont faites pour un archer donne et ne veulent rien dire sur un autre. Le
  /// proposer par defaut evite d'avoir a l'imposer.
  /// </summary>
  int GetProfileArcher(string profileName);

  /// <summary>
  /// Vrai si le profil joue la tenue alternative. Les couleurs sont enregistrees par
  /// archer ET par tenue : les deux vont ensemble.
  /// </summary>
  bool IsProfileAlt(string profileName);
}
