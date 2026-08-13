using System.Collections.Generic;

namespace TFModFortRiseArcher;

public class ApiImplementation : IProfilesModApi, IProfilesRosterApi, IProfilesPowerApi, IProfilesPower2Api, IProfilesPowerKeysApi
{
  /// <summary>
  /// Les touches choisies pour le pouvoir de cet emplacement. Tableau vide quand le
  /// profil n'en impose aucune : l'appelant garde alors les siennes.
  /// </summary>
  public int[] GetPlayerPowerButtons(int playerIndex, int slot)
  {
    ProfileData profile = ProfileAssignment.Get(playerIndex);
    if (profile == null)
    {
      return System.Array.Empty<int>();
    }

    return (slot == 0 ? profile.PadPowerLeft : profile.PadPowerRight) ?? System.Array.Empty<int>();
  }

  public int[] GetPlayerPowerKeys(int playerIndex, int slot)
  {
    ProfileData profile = ProfileAssignment.Get(playerIndex);
    if (profile == null)
    {
      return System.Array.Empty<int>();
    }

    return (slot == 0 ? profile.KeyPowerLeft : profile.KeyPowerRight) ?? System.Array.Empty<int>();
  }

  /// <summary>
  /// Relu a chaque appel : Power le demande en jeu, et le profil rattache a
  /// l'emplacement a pu changer entre deux manches.
  /// </summary>
  public string GetPlayerPower(int playerIndex)
  {
    return ProfileAssignment.Get(playerIndex)?.Power ?? "";
  }

  /// <summary>Le pouvoir de la seconde gachette. Meme lecture, autre rubrique.</summary>
  public string GetPlayerPower2(int playerIndex)
  {
    return ProfileAssignment.Get(playerIndex)?.Power2 ?? "";
  }

  // En dur pour l'instant : des que le mod est charge, il tient le rollcall. Le jour
  // ou cela devient une option, c'est cette propriete qui la refletera, sans que les
  // mods appelants aient a changer.
  public bool HandlesRollcall => true;

  public string GetPlayerName(int playerIndex)
  {
    return PlayerNames.Of(playerIndex);
  }

  public void SetPlayerName(int playerIndex, string playerName)
  {
    PlayerNames.SetOverride(playerIndex, playerName);
  }

  public string GetProfileName(int playerIndex)
  {
    return ProfileAssignment.NameOf(playerIndex);
  }

  /// <summary>
  /// Relu a chaque appel, sans cache : l'appelant typique demande la liste au moment
  /// d'ouvrir son ecran, et un profil cree entre-temps doit y figurer.
  /// </summary>
  public string[] GetProfileNames()
  {
    var names = new List<string>();

    foreach (var profile in ProfileStorage.Profiles)
    {
      // Un profil sans nom n'a rien a faire dans une liste de joueurs : il ne serait
      // ni lisible ni distinguable d'un autre.
      if (!string.IsNullOrWhiteSpace(profile.Name))
      {
        names.Add(profile.Name);
      }
    }

    return names.ToArray();
  }

  public bool AssignProfile(int playerIndex, string profileName)
  {
    ProfileData profile = Find(profileName);

    // Set(index, null) detache : un emplacement sans profil ne doit pas garder celui
    // du match precedent.
    ProfileAssignment.Set(playerIndex, profile);

    // Le mappage de touches suit le profil, comme a la selection des archers.
    ProfileControls.Apply(playerIndex);

    return profile != null;
  }

  public int GetProfileArcher(string profileName)
  {
    ProfileData profile = Find(profileName);
    return profile == null ? -1 : ArcherCatalog.IndexOf(profile);
  }

  public bool IsProfileAlt(string profileName)
  {
    return Find(profileName)?.IsAlt ?? false;
  }

  /// <summary>
  /// Profil portant ce nom. La comparaison ignore la casse et les espaces de bord :
  /// les appelants normalisent leurs listes chacun a leur facon.
  /// </summary>
  private static ProfileData Find(string profileName)
  {
    if (string.IsNullOrWhiteSpace(profileName))
    {
      return null;
    }

    string wanted = profileName.Trim();

    foreach (var profile in ProfileStorage.Profiles)
    {
      if (string.Equals(profile.Name, wanted, System.StringComparison.OrdinalIgnoreCase))
      {
        return profile;
      }
    }

    return null;
  }
}
