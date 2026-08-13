using System;
using FortRise;
using HarmonyLib;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Fait gagner un joueur sur SA musique plutot que sur celle de son personnage.
  ///
  /// Le jeu ne demande jamais "quelle musique pour le gagnant ?" : il appelle
  /// PlayVictoryMusic sur l'ArcherData du vainqueur, qui ne sait rien du joueur.
  /// C'est donc ici qu'il faut retrouver DE QUI il s'agit, et le point d'entree est
  /// commun aux trois ecrans de fin - manche versus, quete, monde sombre - ce qui
  /// evite d'aller patcher trois coroutines.
  ///
  /// Deux facons de reconnaitre le joueur, dans cet ordre :
  ///
  /// - l'ecran de resultats de manche connait son vainqueur, et c'est la seule reponse
  ///   sure quand deux joueurs ont pris le meme archer ;
  /// - sinon, l'ArcherData qui joue appartient a un seul joueur de la partie, et cela
  ///   suffit. C'est le cas des fins de quete et de monde sombre.
  ///
  /// En equipes, la musique est celle de l'equipe et non d'un archer : aucun profil ne
  /// la reclame, elle est laissee telle quelle.
  /// </summary>
  public class MyVictoryMusic : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(ArcherData), nameof(ArcherData.PlayVictoryMusic)),
          prefix: new HarmonyMethod(PlayVictoryMusic_prefix)
      );
    }

    /// <summary>
    /// Rend faux pour sauter la musique du jeu quand le profil en impose une autre.
    /// </summary>
    private static bool PlayVictoryMusic_prefix(ArcherData __instance)
    {
      try
      {
        int player = Winner(__instance);

        if (player < 0)
        {
          return true;
        }

        return !ProfileMusic.Play(ProfileAssignment.Get(player));
      }
      catch (Exception e)
      {
        Log.Error($"[Music] vainqueur non identifie : {e.Message}");
        return true;
      }
    }

    private static int Winner(ArcherData data)
    {
      // L'ecran de resultats est un HUD pose sur le niveau, pas une scene : c'est donc
      // le niveau qu'on trouve sous la main, et sa partie sait qui a gagne.
      if (Engine.Instance?.Scene is Level level && level.Session != null)
      {
        return level.Session.MatchSettings.TeamMode ? -1 : level.Session.GetWinner();
      }

      return Owner(data);
    }

    /// <summary>
    /// Le seul joueur de la partie qui joue cet archer, ou -1 s'ils sont plusieurs.
    ///
    /// Plusieurs, c'est deux joueurs sur le meme personnage : l'ArcherData ne les
    /// distingue pas, et deviner lequel a gagne serait pile ou face. La musique de
    /// l'archer reste alors, ce qui est faux pour personne.
    /// </summary>
    private static int Owner(ArcherData data)
    {
      int found = -1;

      for (int i = 0; i < TFGame.Players.Length; i++)
      {
        if (!TFGame.Players[i])
        {
          continue;
        }

        if (!ReferenceEquals(ArcherData.Get(TFGame.Characters[i], TFGame.AltSelect[i]), data))
        {
          continue;
        }

        if (found >= 0)
        {
          return -1;
        }

        found = i;
      }

      return found;
    }
  }
}
