using System;
using FortRise;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Acces au mod Power, quand il est installe.
  ///
  /// Aura tient la liste des pouvoirs ; Profiles ne fait qu'enregistrer, par profil,
  /// lequel a ete choisi. Cette separation permet a Aura d'en ajouter sans que
  /// Profiles change, et evite d'ecrire ici des noms de pouvoirs en dur.
  ///
  /// La dependance est optionnelle : sans Aura installe, la rubrique POUVOIR
  /// n'apparait pas dans la fiche d'un profil et rien d'autre ne change.
  /// </summary>
  public static class PowerImport
  {
    private static readonly string[] None = new string[0];

    private static IModInterop interop;
    private static IPowerModApi api;

    /// <summary>
    /// Retient de quoi interroger les autres mods. On ne peut PAS resoudre l'API
    /// ici : ModuleManager n'inscrit un mod dans son annuaire qu'APRES avoir
    /// execute son constructeur, donc au moment ou celui de Profiles tourne, aucun
    /// autre mod n'est encore joignable - pas meme un mod charge avant lui.
    /// </summary>
    public static void Bind(IModInterop modInterop)
    {
      interop = modInterop;
    }

    /// <summary>
    /// Resolue au premier besoin, c'est-a-dire a l'ouverture d'une fiche de profil,
    /// longtemps apres que tous les mods sont charges. Mise en cache seulement en
    /// cas de succes : une tentative infructueuse ne coute qu'une recherche dans un
    /// dictionnaire et ne doit pas condamner les suivantes.
    /// </summary>
    private static IPowerModApi Api
    {
      get
      {
        if (api != null)
        {
          return api;
        }

        if (interop == null)
        {
          return null;
        }

        try
        {
          api = interop.GetApi<IPowerModApi>("Power");
        }
        catch (Exception)
        {
          // Mod absent, ou installe sans exposer cette interface : la rubrique
          // POWER n'apparait pas, ce qui est exactement le comportement voulu.
        }

        return api;
      }
    }

    /// <summary>Vrai quand Aura repond : c'est ce qui decide d'afficher la rubrique.</summary>
    public static bool Available => Api != null;

    /// <summary>Les pouvoirs proposables, ou un tableau vide sans Aura.</summary>
    public static string[] PowerIds()
    {
      IPowerModApi aura = Api;
      if (aura == null)
      {
        return None;
      }

      try
      {
        return aura.GetPowerIds() ?? None;
      }
      catch (Exception ex)
      {
        Log.Info($"[Aura] GetPowerIds a echoue : {ex.Message}");
        return None;
      }
    }

    /// <summary>
    /// Libelle d'un pouvoir. Rend l'identifiant tel quel si Aura ne le connait pas :
    /// un profil enregistre avec un pouvoir depuis retire doit rester lisible.
    /// </summary>
    public static string TitleOf(string powerId)
    {
      if (string.IsNullOrEmpty(powerId))
      {
        return "";
      }

      IPowerModApi aura = Api;
      if (aura == null)
      {
        return powerId;
      }

      try
      {
        string title = aura.GetPowerTitle(powerId);
        return string.IsNullOrEmpty(title) ? powerId : title;
      }
      catch (Exception ex)
      {
        Log.Info($"[Aura] GetPowerTitle a echoue : {ex.Message}");
        return powerId;
      }
    }
  }
}
