using System;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Substitue en jeu les textures recolorees du profil a celles de l'archer.
  ///
  /// On passe par SwapSubtexture, la meme porte que le jeu emprunte pour les couleurs
  /// d'equipe : l'atlas partage n'est pas touche, et deux joueurs sur le meme archer
  /// gardent chacun ses couleurs.
  ///
  /// Trois points d'accroche, parce que les sprites d'un joueur ne sont pas tous
  /// crees au meme moment : le constructeur pose le corps et l'arc, le sprite de tete
  /// est reconstruit a chaque changement de coiffe (chapeau perdu, couronne), et le
  /// chapeau qui s'envole est une entite creee au moment du coup.
  ///
  /// En mode par equipes le jeu bascule sur ses textures bleue et rouge : on ne les
  /// remplace pas. Les couleurs d'equipe sont ce qui permet de distinguer les camps,
  /// les recolorer reviendrait a supprimer l'information.
  /// </summary>
  public class MyPlayerSprites : IHookable
  {
    public static void Load(IHarmony harmony)
    {
      var constructor = AccessTools.DeclaredConstructor(typeof(Player), [
                                                    typeof(int),
                                                    typeof(Microsoft.Xna.Framework.Vector2),
                                                    typeof(Allegiance),
                                                    typeof(Allegiance),
                                                    typeof(PlayerInventory),
                                                    typeof(Player.HatStates),
                                                    typeof(bool),
                                                    typeof(bool),
                                                    typeof(bool)
                                                         ]);

      // InitHead reconstruit le sprite de tete a chaque changement de coiffe : sans
      // ce second point d'accroche, perdre son chapeau rendrait sa couleur d'origine.
      var initHead = AccessTools.DeclaredMethod(typeof(Player), "InitHead");

      // Une signature qui ne se retrouve plus doit desactiver la recoloration, pas
      // empecher le mod de se charger : Harmony leve sur une methode nulle.
      if (constructor == null || initHead == null)
      {
        Log.Error("[Sprite] Player introuvable sous la forme attendue, recoloration desactivee");
        return;
      }

      harmony.Patch(constructor, postfix: new HarmonyMethod(Player_ctor_postfix));
      harmony.Patch(initHead, postfix: new HarmonyMethod(InitHead_postfix));

      // Le cadavre est une entite a part, construite au moment de la mort. Toutes les
      // surcharges publiques passent par ce constructeur prive, il suffit donc de
      // l'attraper lui.
      var corpse = AccessTools.DeclaredConstructor(typeof(PlayerCorpse), [
                                                    typeof(string),
                                                    typeof(Allegiance),
                                                    typeof(Microsoft.Xna.Framework.Vector2),
                                                    typeof(Facing),
                                                    typeof(int),
                                                    typeof(int)
                                                         ]);

      if (corpse == null)
      {
        Log.Error("[Sprite] PlayerCorpse introuvable, son cadavre gardera ses couleurs");
        return;
      }

      harmony.Patch(corpse, postfix: new HarmonyMethod(PlayerCorpse_ctor_postfix));

      // L'empennage de la fleche ordinaire est une texture unique teintee par la
      // couleur d'archer : c'est donc une teinte a remplacer, pas une image.
      var initGraphics = AccessTools.DeclaredMethod(typeof(DefaultArrow), "InitGraphics");
      if (initGraphics != null)
      {
        harmony.Patch(initGraphics, postfix: new HarmonyMethod(DefaultArrow_InitGraphics_postfix));
      }

      // La fleche plume et la fleche laser choisissent leurs particules dans le meme
      // InitGraphics : un seul point d'accroche par type de fleche.
      var feather = AccessTools.DeclaredMethod(typeof(FeatherArrow), "InitGraphics");
      if (feather != null)
      {
        harmony.Patch(feather, postfix: new HarmonyMethod(FeatherArrow_InitGraphics_postfix));
      }

      var laser = AccessTools.DeclaredMethod(typeof(LaserArrow), "InitGraphics");
      if (laser != null)
      {
        harmony.Patch(laser, postfix: new HarmonyMethod(LaserArrow_InitGraphics_postfix));
      }
    }

    /// <summary>
    /// Profil du tireur d'une fleche, s'il en a un et si sa recoloration s'applique.
    /// Les fleches d'ennemi portent un index de personnage a -1, et les modes par
    /// equipes gardent les couleurs du camp.
    /// </summary>
    private static ProfileData ArrowProfile(Arrow arrow)
    {
      if (arrow == null
          || arrow.PlayerIndex < 0
          || arrow.CharacterIndex < 0
          || arrow.TeamColor != Allegiance.Neutral)
      {
        return null;
      }

      ProfileData profile = ProfileAssignment.Get(arrow.PlayerIndex);
      return SpriteRecolor.HasSwaps(profile) ? profile : null;
    }

    private static void FeatherArrow_InitGraphics_postfix(FeatherArrow __instance)
    {
      try
      {
        ProfileData profile = ArrowProfile(__instance);
        if (profile == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);

        data.Set("particleType", ProfileParticles.For(
            profile, data.Get<ParticleType>("particleType"), __instance.CharacterIndex));

        // La plume n'est pas seulement suivie de particules : son image est elle aussi
        // teintee par la couleur de l'archer, comme l'empennage de la fleche ordinaire.
        Color? dominant = SpriteRecolor.DominantColor(profile);
        if (dominant != null)
        {
          Tint(data, "image", dominant.Value);
          Tint(data, "buriedImage", dominant.Value);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] fleche plume non recoloree : {e.Message}");
      }
    }

    private static void LaserArrow_InitGraphics_postfix(LaserArrow __instance)
    {
      try
      {
        ProfileData profile = ArrowProfile(__instance);
        if (profile == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);

        data.Set("glowParticleType", ProfileParticles.For(
            profile, data.Get<ParticleType>("glowParticleType"), __instance.CharacterIndex));

        data.Set("trailParticleType", ProfileParticles.For(
            profile, data.Get<ParticleType>("trailParticleType"), __instance.CharacterIndex));
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] fleche laser non recoloree : {e.Message}");
      }
    }

    private static void Tint(DynamicData data, string field, Color color)
    {
      var image = data.Get<Image>(field);
      if (image != null)
      {
        image.Color = color;
      }
    }

    /// <summary>
    /// Donne a l'empennage la couleur dominante du profil.
    ///
    /// InitGraphics est rappele a chaque tir - les fleches sont recyclees dans un
    /// cache et reinitialisees - la teinte suit donc les retouches sans qu'il y ait
    /// rien a invalider.
    /// </summary>
    private static void DefaultArrow_InitGraphics_postfix(DefaultArrow __instance)
    {
      try
      {
        // Les fleches sans tireur, et celles des modes par equipes ou la couleur
        // designe le camp, gardent celle du jeu.
        if (__instance.PlayerIndex < 0 || __instance.TeamColor != Allegiance.Neutral)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(__instance.PlayerIndex);
        Color? dominant = SpriteRecolor.DominantColor(profile);
        if (dominant == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);
        var feather = data.Get<Image>("featherImage");
        if (feather != null)
        {
          feather.Color = dominant.Value;
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] empennage non recolore : {e.Message}");
      }
    }

    private static void PlayerCorpse_ctor_postfix(PlayerCorpse __instance)
    {
      try
      {
        // Un cadavre d'ennemi n'appartient a aucun joueur : son index vaut -1.
        if (__instance.PlayerIndex < 0)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(__instance.PlayerIndex);
        if (!SpriteRecolor.HasSwaps(profile))
        {
          return;
        }

        using var data = DynamicData.For(__instance);
        Swap(data.Get<Sprite<string>>("sprite"), profile, SpriteRecolor.Corpse);
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] recoloration du cadavre impossible : {e}");
      }
    }

    private static void Player_ctor_postfix(Player __instance)
    {
      try
      {
        ProfileData profile = ProfileFor(__instance);
        if (profile == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);

        Swap(data.Get<Sprite<string>>("bodySprite"), profile, SpriteRecolor.Body);
        Swap(data.Get<Sprite<string>>("bowSprite"), profile, SpriteRecolor.Bow);
        Swap(data.Get<Sprite<string>>("headBackSprite"), profile, SpriteRecolor.HeadBack);
        Swap(data.Get<Sprite<string>>("headSprite"), profile, HeadPart(__instance));

        // La fleche encochee est une Image et non un Sprite : meme substitution, mais
        // par la methode de l'Image. Son origine est posee juste apres la construction
        // et SwapSubtexture n'y touche pas, la fleche reste donc calee sur la corde.
        SwapImage(data.Get<Image>("aimer"), profile, SpriteRecolor.Aim);
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] recoloration du joueur impossible : {e}");
      }
    }

    private static void InitHead_postfix(Player __instance)
    {
      try
      {
        ProfileData profile = ProfileFor(__instance);
        if (profile == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);
        Swap(data.Get<Sprite<string>>("headSprite"), profile, HeadPart(__instance));
      }
      catch (Exception e)
      {
        Log.Error($"[Sprite] recoloration de la tete impossible : {e}");
      }
    }

    /// <summary>
    /// Quelle planche de tete est en place : le jeu en a une par etat de coiffe, et
    /// chacune a sa propre texture a recolorer.
    /// </summary>
    private static string HeadPart(Player player)
    {
      return player.HatState switch
      {
        Player.HatStates.NoHat => SpriteRecolor.HeadNoHat,
        Player.HatStates.Crown => SpriteRecolor.HeadCrown,
        _ => SpriteRecolor.Head
      };
    }

    private static void SwapImage(Image image, ProfileData profile, string part)
    {
      if (image == null)
      {
        return;
      }

      Subtexture recolored = SpriteRecolor.Baked(profile, part);
      if (recolored != null)
      {
        image.SwapSubtexture(recolored);
      }
    }

    private static void Swap(Sprite<string> sprite, ProfileData profile, string part)
    {
      if (sprite == null)
      {
        return;
      }

      Subtexture recolored = SpriteRecolor.Baked(profile, part);
      if (recolored != null)
      {
        sprite.SwapSubtexture(recolored);
      }
    }

    /// <summary>
    /// Profil du joueur, s'il en a un et si sa recoloration doit s'appliquer.
    /// </summary>
    private static ProfileData ProfileFor(Player player)
    {
      if (player == null || player.TeamColor != Allegiance.Neutral)
      {
        return null;
      }

      ProfileData profile = ProfileAssignment.Get(player.PlayerIndex);
      return SpriteRecolor.HasSwaps(profile) ? profile : null;
    }
  }
}
