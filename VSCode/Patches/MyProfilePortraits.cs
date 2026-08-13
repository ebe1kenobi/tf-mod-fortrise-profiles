using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FortRise;
using HarmonyLib;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Remplace les portraits de l'archer par les images du profil : sur l'ecran de
  /// selection, et sur celui des resultats de match.
  ///
  /// Le portrait de selection est repose par le jeu a chaque evenement - rejoindre,
  /// quitter, changer d'archer remettent tous celui de l'ArcherData. Il ne suffit donc
  /// pas de le substituer une fois : il faut le refaire apres chacun de ces gestes.
  ///
  /// ArcherPortrait ne sait pas a quel joueur il appartient : c'est le RollcallElement
  /// qui porte l'index. D'ou la table d'appartenance, remplie depuis le rollcall et
  /// consultee par les correctifs du portrait.
  /// </summary>
  public class MyProfilePortraits : IHookable
  {
    // Table faible : un portrait abandonne avec son ecran ne doit pas etre retenu ici.
    private static readonly ConditionalWeakTable<ArcherPortrait, object> owners = new();

    public static void Load(IHarmony harmony)
    {
      foreach (string name in new[] { "StartJoined", "Leave", "SetCharacter" })
      {
        var method = AccessTools.DeclaredMethod(typeof(ArcherPortrait), name);
        if (method != null)
        {
          harmony.Patch(method, postfix: new HarmonyMethod(Portrait_postfix));
        }
      }

      var results = AccessTools.DeclaredConstructor(typeof(VersusPlayerMatchResults), [
                                                    typeof(Session),
                                                    typeof(VersusMatchResults),
                                                    typeof(int),
                                                    typeof(Microsoft.Xna.Framework.Vector2),
                                                    typeof(Microsoft.Xna.Framework.Vector2),
                                                    typeof(List<AwardInfo>)
                                                         ]);

      if (results != null)
      {
        harmony.Patch(results, postfix: new HarmonyMethod(MatchResults_postfix));
      }
      else
      {
        Log.Error("[Portrait] VersusPlayerMatchResults introuvable, les images de resultat sont ignorees");
      }
    }

    /// <summary>
    /// Rattache un portrait a un joueur, et lui applique aussitot l'image du profil.
    /// Appele depuis le rollcall, seul endroit qui connaisse l'index.
    /// </summary>
    public static void Claim(ArcherPortrait portrait, int playerIndex)
    {
      if (portrait == null)
      {
        return;
      }

      owners.Remove(portrait);
      owners.Add(portrait, playerIndex);
      Apply(portrait);
    }

    private static void Portrait_postfix(ArcherPortrait __instance)
    {
      Apply(__instance);
    }

    private static void Apply(ArcherPortrait portrait)
    {
      try
      {
        if (portrait == null || !owners.TryGetValue(portrait, out object owner))
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get((int)owner);
        Subtexture image = ProfileImages.ForArcher(profile);
        if (image == null)
        {
          return;
        }

        using var data = DynamicData.For(portrait);

        // Les deux faces sont remplacees : portraitAlt est celle qu'on voit pendant
        // le retournement quand on bascule de costume.
        Swap(data.Get<Image>("portrait"), image);
        Swap(data.Get<Image>("portraitAlt"), image);
      }
      catch (Exception e)
      {
        Log.Error($"[Portrait] image de selection non appliquee : {e.Message}");
      }
    }

    private static void Swap(Image target, Subtexture image)
    {
      if (target == null)
      {
        return;
      }

      target.SwapSubtexture(image);

      // L'origine est recalculee : une image personnelle n'a aucune raison d'avoir la
      // taille du portrait d'origine, et le jeu centre le sien a la construction.
      target.CenterOrigin();
    }

    private static void MatchResults_postfix(VersusPlayerMatchResults __instance)
    {
      try
      {
        using var data = DynamicData.For(__instance);

        int playerIndex = data.Get<int>("playerIndex");
        ProfileData profile = ProfileAssignment.Get(playerIndex);
        if (profile == null)
        {
          return;
        }

        Subtexture image = ProfileImages.ForResult(profile, data.Get<bool>("won"));
        if (image == null)
        {
          return;
        }

        var portrait = data.Get<Image>("portrait");
        if (portrait == null)
        {
          return;
        }

        portrait.SwapSubtexture(image);
        portrait.CenterOrigin();
      }
      catch (Exception e)
      {
        Log.Error($"[Portrait] image de resultat non appliquee : {e.Message}");
      }
    }
  }
}
