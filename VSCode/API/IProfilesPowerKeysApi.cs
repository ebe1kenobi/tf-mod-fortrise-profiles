namespace TFModFortRiseArcher;

/// <summary>
/// Les touches des deux pouvoirs, telles que le profil les a definies.
///
/// Interface a part, comme les deux precedentes : l'interop construit son proxy sur
/// la FORME des membres, donc un mod Power plus ancien - qui n'en connait que le
/// pouvoir et le second pouvoir - continue de fonctionner. Il gardera ses gachettes
/// codees en dur, ce qui etait le comportement d'avant.
///
/// Les valeurs sont des ENTIERS, pas des Buttons ou des Keys : l'interop n'a ainsi
/// aucun type de XNA a faire correspondre entre les deux mods.
/// </summary>
public partial interface IProfilesPowerKeysApi
{
  /// <summary>
  /// Boutons de manette du pouvoir de cet emplacement (0 = gauche, 1 = droite),
  /// ou un tableau vide si le profil n'en impose aucun.
  /// </summary>
  int[] GetPlayerPowerButtons(int playerIndex, int slot);

  /// <summary>Touches clavier du meme pouvoir, memes regles.</summary>
  int[] GetPlayerPowerKeys(int playerIndex, int slot);
}
