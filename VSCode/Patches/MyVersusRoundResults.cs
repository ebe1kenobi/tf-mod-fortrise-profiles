using System;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Noms sur l'ecran de resultats de manche.
  ///
  /// Repris du mod CustomName. L'ecran construit ses libelles "P1".."P8" lui-meme et
  /// n'expose pas de quoi les remplacer : on parcourt donc ses composants texte pour
  /// reconnaitre ce motif et y substituer le nom du profil.
  ///
  /// Le rapprochement se fait sur "P" suivi d'un chiffre. Les libelles de la forme
  /// "P 1", produits par d'autres mods pour distinguer humains et IA, sont laisses
  /// tels quels : le caractere suivant n'est pas un chiffre.
  /// </summary>
  public class MyVersusRoundResults : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(VersusRoundResults), nameof(VersusRoundResults.Update)),
          prefix: new HarmonyMethod(Update_patch)
      );
    }

    private static void Update_patch(VersusRoundResults __instance)
    {
      try
      {
        if (__instance.Components == null)
        {
          return;
        }

        for (int i = 0; i < __instance.Components.Count; i++)
        {
          if (__instance.Components[i] is not Text text)
          {
            continue;
          }

          using var data = DynamicData.For(text);

          string current = data.Get<string>("text");
          if (string.IsNullOrEmpty(current) || current.Length < 2)
          {
            continue;
          }

          if (current[0] != 'P' || !char.IsDigit(current[1]))
          {
            continue;
          }

          int playerIndex = current[1] - '1';
          if (playerIndex < 0 || playerIndex >= TFGame.Players.Length)
          {
            continue;
          }

          data.Set("text", PlayerNames.Of(playerIndex));

          // Meme teinte qu'au rollcall et au debut du round : le nom porte la couleur
          // du personnage, ce qui le rattache a un joueur quand deux partagent l'archer.
          Color? dominant = SpriteRecolor.DominantColor(ProfileAssignment.Get(playerIndex));
          if (dominant != null)
          {
            text.Color = dominant.Value;
          }

          // Un nom de profil est plus long que "P1" : sans recaler l'ancre a gauche,
          // il deborderait de la colonne.
          text.Position.X = 20;
        }
      }
      catch (Exception e)
      {
        Log.Error($"[VersusRoundResults] noms non appliques : {e}");
      }
    }
  }
}
