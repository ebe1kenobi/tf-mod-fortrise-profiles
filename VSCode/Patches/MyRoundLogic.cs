using System;
using FortRise;
using HarmonyLib;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Son de victoire de match, joue sur le coup final.
  ///
  /// FinalKillNoSpotlight est le moment ou le jeu ralentit et coupe la musique pour
  /// la derniere elimination : c'est la que le son du vainqueur doit tomber. On
  /// remplace la sequence d'origine seulement si un son est effectivement joue, sinon
  /// la fin de match perdrait son traitement habituel.
  /// </summary>
  public class MyRoundLogic : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(RoundLogic), "FinalKillNoSpotlight"),
          prefix: new HarmonyMethod(FinalKillNoSpotlight_prefix)
      );
    }

    private static bool FinalKillNoSpotlight_prefix(RoundLogic __instance)
    {
      try
      {
        int winners = 0;
        int winner = -1;

        for (int i = 0; i < TFGame.Players.Length; i++)
        {
          if (!TFGame.Players[i])
          {
            continue;
          }

          if (__instance.Session.MatchStats[i].GotWin)
          {
            winners++;
            winner = i;
          }
        }

        // Une victoire partagee n'a pas de vainqueur unique a faire parler.
        if (winner < 0 || winners != 1)
        {
          return true;
        }

        ProfileData profile = ProfileAssignment.Get(winner);
        if (profile == null || !ProfileSfx.Has(profile, SoundEvents.Win))
        {
          return true;
        }

        __instance.Session.CurrentLevel.OrbLogic.DoSlowMoKill();
        __instance.Session.MatchSettings.LevelSystem.StopVersusMusic();

        if (!ProfileSfx.TryPlay(profile, SoundEvents.Win, 1f))
        {
          // Le son a disparu entre la verification et la lecture : on a deja coupe la
          // musique, mieux vaut laisser la sequence d'origine reprendre la main.
          return true;
        }

        return false;
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] son de victoire : {e.Message}");
        return true;
      }
    }
  }
}
