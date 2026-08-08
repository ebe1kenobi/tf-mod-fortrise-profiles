using System;
using FortRise;
using HarmonyLib;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Autorise plusieurs joueurs a prendre le meme archer.
  ///
  /// Le jeu l'interdit pour une raison de lisibilite : deux personnages identiques a
  /// l'ecran seraient impossibles a distinguer. Cette raison tombe des lors que chaque
  /// profil recolore son sprite - c'est meme l'usage principal de la recoloration.
  ///
  /// Tout repose sur <c>TFGame.CharacterTaken</c>, que le jeu ne consulte qu'a quatre
  /// endroits, tous dans RollcallElement : le defilement gauche et droite, qui saute
  /// les archers pris ; EnforceCharacterLock, qui pousse un joueur hors de l'archer
  /// qu'un autre vient de prendre ; et la validation, qui refuse de rejoindre sur un
  /// archer occupe. La rendre toujours fausse leve les quatre d'un coup, sans toucher
  /// a la logique du rollcall.
  /// </summary>
  public class MySameArcher : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      var taken = AccessTools.DeclaredMethod(typeof(TFGame), nameof(TFGame.CharacterTaken));
      if (taken != null)
      {
        harmony.Patch(taken, prefix: new HarmonyMethod(CharacterTaken_prefix));
      }
      else
      {
        Log.Error("[Rollcall] TFGame.CharacterTaken introuvable, le partage d'archer reste interdit");
      }

      // Sans ce second correctif, les fleches cesseraient de repondre des que le
      // nombre d'archers debloques descend sous le nombre de joueurs : voir plus bas.
      var canChange = AccessTools.PropertyGetter(typeof(RollcallElement), "CanChangeSelection");
      if (canChange != null)
      {
        harmony.Patch(canChange, prefix: new HarmonyMethod(CanChangeSelection_prefix));
      }
    }

    private static bool CharacterTaken_prefix(ref bool __result)
    {
      __result = false;
      return false;
    }

    /// <summary>
    /// Le jeu n'autorise a changer d'archer que s'il en reste plus que de joueurs :
    /// <c>TotalUnlockedArchers &gt; PlayerAmount + 1</c>. La regle existait pour eviter
    /// une impasse ou personne ne trouverait d'archer libre.
    ///
    /// Les archers n'etant plus exclusifs, cette impasse n'existe plus : il suffit
    /// d'en avoir deux pour qu'il y ait un choix a faire.
    /// </summary>
    private static bool CanChangeSelection_prefix(ref bool __result)
    {
      try
      {
        __result = SaveData.Instance?.Unlocks != null
                && SaveData.Instance.Unlocks.TotalUnlockedArchers > 1;
        return false;
      }
      catch (Exception e)
      {
        Log.Error($"[Rollcall] choix d'archer : {e.Message}");
        return true;
      }
    }
  }
}
