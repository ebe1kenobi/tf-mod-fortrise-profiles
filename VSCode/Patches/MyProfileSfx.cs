using System;
using FortRise;
using HarmonyLib;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Remplace les sons du jeu par ceux du profil, quand le profil en a.
  ///
  /// Le jeu ne joue pas ses effets par un point d'entree nomme : chaque action
  /// appelle Play() sur l'objet SFX que porte son ArcherData. Tout passe donc par un
  /// seul interception, celle de SFX.Play - et l'evenement s'y reconnait de deux
  /// facons.
  ///
  /// A L'ARRIVEE, pour presque tout : l'instance jouee appartient a un champ precis
  /// du CharacterSounds de l'archer, ce qui dit QUOI, et le panoramique - l'abscisse
  /// de l'entite - dit QUI. Aucune methode du joueur n'a besoin d'etre patchee.
  ///
  /// PAR L'ACTION, pour les seules morts : le son ne dit pas qui a tue, et
  /// KILLED_BY_&lt;PROFIL&gt; en depend. Le prefix de Die retient donc la victime, le
  /// tueur et la cause, et SFX.Play s'en sert quand l'instance correspond.
  ///
  /// Passer par SFX.Play plutot que par un remplacement de l'ArcherData evite de
  /// modifier des donnees partagees par tous les joueurs qui ont choisi le meme
  /// archer.
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
        // Action encadree : la mort et le tir. Le prefix de l'action sait dire QUEL
        // joueur agit, ce que le seul son ne dirait pas - et c'est ce qu'il faut pour
        // KILLED_BY_<PROFIL>, qui depend du tueur.
        if (Pending.Vanilla != null && Pending.Profile != null && ReferenceEquals(__instance, Pending.Vanilla))
        {
          return !ProfileSfx.TryPlay(Pending.Profile, Pending.Event, volume);
        }

        // Tous les autres sons de l'archer. Ils partent d'endroits disperses - le
        // setter Aiming, EnterDucking, le saut, l'atterrissage au milieu de
        // NormalUpdate, la collecte de fleches - dont plusieurs sont prives ou trop
        // courts pour etre patches un a un. On les reconnait donc a l'ARRIVEE.
        return !FromArcherSound(__instance, panX, volume);
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] lecture : {e.Message}");
        return true;
      }
    }

    /// <summary>
    /// Les sons que porte le CharacterSounds d'un archer, et l'evenement de profil
    /// qui leur correspond.
    ///
    /// Les MORTS n'y figurent pas, et c'est le seul cas ou la reconnaissance a
    /// l'arrivee ne suffirait pas : elle saurait dire de quelle mort il s'agit - le
    /// jeu a une instance distincte par cause - mais pas QUI a tue, et c'est ce qu'il
    /// faut pour KILLED_BY_&lt;PROFIL&gt;. Le tueur ne se lit que dans les arguments
    /// de Die, donc les morts gardent leur action encadree.
    /// </summary>
    private static readonly (string Event, Func<CharacterSounds, Monocle.SFX> Sound)[] Watched =
    {
      (SoundEvents.FireArrow, s => s.FireArrow),
      (SoundEvents.Jump, s => s.Jump),
      (SoundEvents.Land, s => s.Land),
      (SoundEvents.Duck, s => s.Duck),
      (SoundEvents.Aim, s => s.Aim),
      (SoundEvents.AimCancel, s => s.AimCancel),
      (SoundEvents.Grab, s => s.Grab),
      (SoundEvents.ArrowGrab, s => s.ArrowGrab),
      (SoundEvents.ArrowRecover, s => s.ArrowRecover),
      (SoundEvents.NoFire, s => s.NoFire),
    };

    /// <summary>
    /// Remonte du son joue au joueur et a l'evenement.
    ///
    /// Chaque appel du jeu a la forme <c>ArcherData.SFX.X.Play(base.X, 1f)</c> : le
    /// son dit QUOI, et le panoramique - qui n'est autre que l'abscisse de l'entite -
    /// dit QUI. C'est ce second point qui compte : deux joueurs ayant choisi le meme
    /// archer partagent le meme CharacterSounds, l'instance seule ne les distinguerait
    /// pas.
    ///
    /// Consequence assumee : une entite qui jouerait un son d'archer depuis la position
    /// exacte d'un joueur - un squelette qui ramasse une fleche a son abscisse - lui
    /// emprunterait sa voix. Il faudrait une coincidence au demi-pixel.
    /// </summary>
    private static bool FromArcherSound(Monocle.SFX sfx, float panX, float volume)
    {
      Level level = Monocle.Engine.Instance?.Scene as Level;
      if (level == null)
      {
        return false;
      }

      foreach (Monocle.Entity entity in level[Monocle.GameTags.Player])
      {
        Player player = entity as Player;
        CharacterSounds sounds = player?.ArcherData?.SFX;
        if (sounds == null || Math.Abs(player.X - panX) > 0.5f)
        {
          continue;
        }

        foreach (var watched in Watched)
        {
          if (!ReferenceEquals(watched.Sound(sounds), sfx))
          {
            continue;
          }

          ProfileData profile = ProfileAssignment.Get(player.PlayerIndex);
          return profile != null && ProfileSfx.TryPlay(profile, watched.Event, volume);
        }
      }

      return false;
    }
  }
}
