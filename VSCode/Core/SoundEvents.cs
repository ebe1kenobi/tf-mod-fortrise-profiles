using System;
using System.Collections.Generic;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Les evenements auxquels un profil peut attacher des sons.
  ///
  /// Un nom d'evenement est aussi un nom de dossier sur le disque : il doit donc
  /// rester un identifiant simple, en majuscules et sans separateur exotique.
  ///
  /// KILLED_BY_&lt;PROFIL&gt; n'est pas dans la liste fixe : il en existe un par autre
  /// profil, et la liste bouge avec eux.
  /// </summary>
  public static class SoundEvents
  {
    public const string Die = "DIE";
    public const string DieBomb = "DIE_BOMB";
    public const string DieEnv = "DIE_ENV";
    public const string DieLaser = "DIE_LASER";
    public const string DieStomp = "DIE_STOMP";
    public const string FireArrow = "FIRE_ARROW";

    // Les sons de tous les jours. Ils ne passent pas par une action encadree comme
    // les morts et le tir : ils sont reconnus a l'instance SFX que le jeu joue (voir
    // MyProfileSfx.FromArcherSound).
    public const string Jump = "JUMP";
    public const string Land = "LAND";
    public const string Duck = "DUCK";
    public const string Aim = "AIM";
    public const string AimCancel = "AIM_CANCEL";
    public const string Grab = "GRAB";
    public const string ArrowGrab = "ARROW_GRAB";
    public const string ArrowRecover = "ARROW_RECOVER";
    public const string NoFire = "NOFIRE";

    public const string Win = "WIN";

    public const string KilledByPrefix = "KILLED_BY_";

    /// <summary>Evenements communs a tous les profils, dans l'ordre d'affichage.</summary>
    public static readonly string[] Fixed =
    {
      Die, DieBomb, DieEnv, DieLaser, DieStomp,
      FireArrow, Jump, Land, Duck, Aim, AimCancel,
      Grab, ArrowGrab, ArrowRecover, NoFire,
      Win
    };

    public static string KilledBy(string killerProfileName)
    {
      return KilledByPrefix + Sanitize(killerProfileName);
    }

    /// <summary>
    /// Libelle lisible pour le menu : "KILLED BY ERIC" plutot que "KILLED_BY_ERIC".
    /// </summary>
    public static string Label(string soundEvent)
    {
      return soundEvent.Replace('_', ' ');
    }

    /// <summary>
    /// Evenement de mort correspondant a la cause. Reprend la correspondance du mod
    /// Customize, pour que des fichiers deja tries par evenement restent valables.
    /// </summary>
    public static string ForDeath(DeathCause cause, bool laser)
    {
      switch (cause)
      {
        case DeathCause.Arrow:
          return laser ? DieLaser : Die;
        case DeathCause.Explosion:
          return DieBomb;
        case DeathCause.JumpedOn:
          return DieStomp;
        default:
          return DieEnv;
      }
    }

    /// <summary>
    /// Rend un nom utilisable comme nom de dossier. Les noms de profil viennent d'un
    /// clavier virtuel qui autorise des caracteres interdits par le systeme de
    /// fichiers ("?", ":", "/", "\"), et un nom refuse par le disque ferait echouer
    /// silencieusement l'affectation d'un son.
    /// </summary>
    public static string Sanitize(string name)
    {
      if (string.IsNullOrEmpty(name))
      {
        return "";
      }

      var invalid = new HashSet<char>(System.IO.Path.GetInvalidFileNameChars());
      var builder = new System.Text.StringBuilder(name.Length);

      foreach (char c in name.ToUpperInvariant())
      {
        builder.Append(invalid.Contains(c) || c == ' ' ? '_' : c);
      }

      return builder.ToString();
    }
  }
}
