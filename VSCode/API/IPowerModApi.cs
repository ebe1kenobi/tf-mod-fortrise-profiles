namespace TFModFortRiseArcher;

/// <summary>
/// Copie locale de l'interface publiee par le mod Aura.
///
/// Elle n'a pas a etre partagee : l'interop de FortRise construit son proxy sur la
/// FORME des membres, pas sur le type. Il suffit que les signatures correspondent a
/// celles de <c>TFModFortRiseAura.IPowerModApi</c>.
///
/// Corollaire a ne pas oublier : declarer ici un membre absent de la version
/// installee d'Aura ferait echouer TOUT le proxy, pas seulement ce membre. On s'en
/// tient donc au strict necessaire, et une addition future ira dans une interface
/// separee.
/// </summary>
public partial interface IPowerModApi
{
  /// <summary>Identifiants des pouvoirs disponibles, dans l'ordre ou les proposer.</summary>
  string[] GetPowerIds();

  /// <summary>Libelle affichable d'un pouvoir.</summary>
  string GetPowerTitle(string powerId);
}
