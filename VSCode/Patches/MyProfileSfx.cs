using System;
using FortRise;
using HarmonyLib;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Remplace les sons du jeu par ceux du profil, quand le profil en a.
  ///
  /// Le jeu ne joue pas ses effets par un point d'entree nomme : chaque action
  /// appelle Play() sur l'objet SFX que porte son ArcherData. On encadre donc
  /// l'action (Die, ShootArrow) pour retenir quel SFX elle s'apprete a jouer et pour
  /// quel joueur, puis on intercepte SFX.Play : si l'instance qui se declenche est
  /// bien celle qu'on attendait, on joue le son du profil et on annule l'original.
  ///
  /// C'est la methode du mod Customize. Passer par SFX.Play plutot que par un
  /// remplacement de l'ArcherData evite de modifier des donnees partagees par tous
  /// les joueurs qui ont choisi le meme archer.
  /// </summary>
  public class MyProfileSfx : IHookable
  {
    /// <summary>
    /// Ce que l'action en cours s'apprete a jouer. Valable seulement entre le prefix
    /// et le postfix de cette action, d'ou le nettoyage systematique.
    /// </summary>
    private static class Pending
    {
      public static Monocle.SFX Vanilla;
      public static ProfileData Profile;
      public static string Event;

      public static void Clear()
      {
        Vanilla = null;
        Profile = null;
        Event = null;
      }
    }

    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(Monocle.SFX), nameof(Monocle.SFX.Play)),
          prefix: new HarmonyMethod(SFX_Play_prefix)
      );

      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(Player), "ShootArrow"),
          prefix: new HarmonyMethod(ShootArrow_prefix),
          postfix: new HarmonyMethod(Action_postfix)
      );

      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(Player), nameof(Player.Die), [
                                                             typeof(DeathCause),
                                                             typeof(int),
                                                             typeof(bool),
                                                             typeof(bool)
                                                                        ]),
          prefix: new HarmonyMethod(Die_prefix),
          postfix: new HarmonyMethod(Action_postfix)
      );
    }

    private static void ShootArrow_prefix(Player __instance)
    {
      try
      {
        ProfileData profile = ProfileAssignment.Get(__instance.PlayerIndex);
        if (profile == null)
        {
          return;
        }

        Pending.Vanilla = __instance.ArcherData.SFX.FireArrow;
        Pending.Profile = profile;
        Pending.Event = SoundEvents.FireArrow;
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] tir : {e.Message}");
        Pending.Clear();
      }
    }

    private static void Die_prefix(Player __instance, DeathCause deathCause, int killerIndex, bool brambled, bool laser)
    {
      try
      {
        ProfileData victim = ProfileAssignment.Get(__instance.PlayerIndex);
        if (victim == null)
        {
          return;
        }

        Pending.Vanilla = __instance.ArcherData.SFX.GetDeath(deathCause, laser);
        Pending.Profile = victim;
        Pending.Event = SoundEvents.ForDeath(deathCause, laser);

        // Un son "tue par untel" l'emporte sur le son de mort generique : c'est le
        // plus specifique des deux, et c'est celui que le joueur a pris la peine de
        // choisir pour cet adversaire precis.
        if (killerIndex >= 0 && killerIndex != __instance.PlayerIndex)
        {
          ProfileData killer = ProfileAssignment.Get(killerIndex);
          if (killer != null && !string.IsNullOrEmpty(killer.Name))
          {
            string killedBy = SoundEvents.KilledBy(killer.Name);
            if (ProfileSfx.Has(victim, killedBy))
            {
              Pending.Event = killedBy;
            }
          }
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] mort : {e.Message}");
        Pending.Clear();
      }
    }

    private static void Action_postfix()
    {
      Pending.Clear();
    }

    private static bool SFX_Play_prefix(Monocle.SFX __instance, float panX, float volume)
    {
      try
      {
        if (Pending.Vanilla == null || Pending.Profile == null || !ReferenceEquals(__instance, Pending.Vanilla))
        {
          return true;
        }

        return !ProfileSfx.TryPlay(Pending.Profile, Pending.Event, volume);
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] lecture : {e.Message}");
        return true;
      }
    }
  }
}
