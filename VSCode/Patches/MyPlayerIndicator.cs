using System;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Nom du joueur sous la fleche qui le designe au debut d'une manche.
  ///
  /// Repris du mod CustomName, la source du nom en moins : Profiles la fournit
  /// desormais lui-meme.
  /// </summary>
  public class MyPlayerIndicator : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredConstructor(typeof(PlayerIndicator), [
                                                                        typeof(Vector2),
                                                                        typeof(int),
                                                                        typeof(bool)
                                                                    ]),
          postfix: new HarmonyMethod(ctor_patch)
      );
    }

    private static void ctor_patch(PlayerIndicator __instance, Vector2 offset, int playerIndex, bool crown)
    {
      try
      {
        using var data = DynamicData.For(__instance);
        data.Set("text", PlayerNames.Of(playerIndex));
      }
      catch (Exception e)
      {
        Log.Error($"[PlayerIndicator] nom non applique : {e}");
      }
    }
  }
}
