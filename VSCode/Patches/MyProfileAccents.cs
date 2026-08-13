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
  /// Etend la couleur dominante du profil a ce que le jeu teinte avec la couleur de
  /// l'archer : le nom affiche au debut du round, et la brume qui entoure les archers
  /// dits "a particules violettes".
  ///
  /// Ces deux endroits ne dessinent pas une image : ils **teintent**, en lisant une
  /// couleur dans des donnees partagees par tous les joueurs du meme archer.
  ///
  /// Les deux appellent une reponse differente. Pour le nom, la couleur est lue au
  /// moment du rendu : la preter le temps de l'appel suffit. Pour la brume, non - une
  /// particule relit les couleurs de son type pendant toute sa vie, bien apres
  /// l'emission. Il lui faut donc un type a elle, durable.
  /// </summary>
  public class MyProfileAccents : IHookable
  {
    private static Color? savedColorA;
    private static Color? savedColorB;
    private static ArcherData borrowedArcher;

    /// <summary>
    /// Type de brume propre a chaque profil, garde d'une emission a l'autre. La
    /// couleur retenue permet de le refabriquer quand le profil est retouche.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, (Color Color, ParticleType Type)> ambience =
        new System.Collections.Generic.Dictionary<string, (Color, ParticleType)>();

    /// <summary>
    /// Brume a emettre pour le joueur dont la mise a jour est en cours, ou null. Sert
    /// a savoir a qui attribuer une emission, ParticleSystem.Emit ne le disant pas.
    /// </summary>
    private static ParticleType pendingAmbience;

    public static void Load(IHarmony harmony)
    {
      var indicator = AccessTools.DeclaredMethod(typeof(PlayerIndicator), nameof(PlayerIndicator.Render));
      if (indicator != null)
      {
        harmony.Patch(indicator,
            prefix: new HarmonyMethod(Indicator_prefix),
            postfix: new HarmonyMethod(Indicator_postfix));
      }

      var update = AccessTools.DeclaredMethod(typeof(Player), nameof(Player.Update));
      if (update != null)
      {
        harmony.Patch(update,
            prefix: new HarmonyMethod(PlayerUpdate_prefix),
            postfix: new HarmonyMethod(PlayerUpdate_postfix));
      }

      // C'est ici que la brume du joueur courant est substituee a celle du jeu.
      var emit = AccessTools.DeclaredMethod(typeof(ParticleSystem), nameof(ParticleSystem.Emit), [
                                                    typeof(ParticleType),
                                                    typeof(int),
                                                    typeof(Vector2),
                                                    typeof(Vector2)
                                                         ]);

      if (emit != null)
      {
        harmony.Patch(emit, prefix: new HarmonyMethod(Emit_prefix));
      }
      else
      {
        Log.Error("[Accents] ParticleSystem.Emit introuvable, la brume garde sa couleur");
      }

      // Poussiere, esquive et saut hyper passent par trois proprietes de Player.
      // Les intercepter la couvre toutes leurs emissions d'un coup, ou qu'elles se
      // fassent, sans avoir a poser un contexte autour de chaque site.
      foreach (string name in new[] { "DustParticleType", "DashParticleType", "HyperJumpParticleType" })
      {
        var getter = AccessTools.PropertyGetter(typeof(Player), name);
        if (getter != null)
        {
          harmony.Patch(getter, postfix: new HarmonyMethod(ParticleType_postfix));
        }
        else
        {
          Log.Error($"[Accents] Player.{name} introuvable");
        }
      }

      var shield = AccessTools.DeclaredConstructor(typeof(PlayerShield), [typeof(LevelEntity)]);
      if (shield != null)
      {
        harmony.Patch(shield, postfix: new HarmonyMethod(Shield_ctor_postfix));
      }

      var ghostDash = AccessTools.PropertyGetter(typeof(PlayerGhost), "DashParticleType");
      if (ghostDash != null)
      {
        harmony.Patch(ghostDash, postfix: new HarmonyMethod(GhostDash_postfix));
      }
    }

    /// <summary>
    /// Le bouclier et le fantome prennent eux aussi le nuage d'esquive de l'archer.
    ///
    /// Aucun des deux n'expose son camp de la meme facon que Player : plutot que
    /// d'aller le chercher, on verifie que le type rendu est bien celui de l'archer.
    /// S'il s'agit d'un type d'equipe, il ne correspondra pas et on ne touchera a rien.
    /// </summary>
    private static void Shield_ctor_postfix(PlayerShield __instance, LevelEntity owner)
    {
      try
      {
        if (owner is not Player player)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(player.PlayerIndex);
        Color? dominant = SpriteRecolor.DominantColor(profile);
        if (dominant == null)
        {
          return;
        }

        using var data = DynamicData.For(__instance);

        ParticleType current = data.Get<ParticleType>("particleType");
        if (IsArcherDash(current, player.CharacterIndex))
        {
          data.Set("particleType", ProfileParticles.For(profile, current, player.CharacterIndex));
        }

        // Le bouclier lui-meme est teinte par la couleur de l'archer.
        object sprite = data.Get<object>("sprite");
        if (sprite != null)
        {
          using var spriteData = DynamicData.For(sprite);
          spriteData.Set("Color", dominant.Value);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Accents] bouclier non recolore : {e.Message}");
      }
    }

    private static void GhostDash_postfix(PlayerGhost __instance, ref ParticleType __result)
    {
      try
      {
        if (__result == null || __instance.PlayerIndex < 0)
        {
          return;
        }

        int characterIndex = TFGame.Characters[__instance.PlayerIndex];
        if (!IsArcherDash(__result, characterIndex))
        {
          return;
        }

        __result = ProfileParticles.For(ProfileAssignment.Get(__instance.PlayerIndex), __result, characterIndex);
      }
      catch (Exception e)
      {
        Log.Error($"[Accents] fantome non recolore : {e.Message}");
      }
    }

    /// <summary>Vrai si ce type est bien le nuage d'esquive de cet archer.</summary>
    private static bool IsArcherDash(ParticleType type, int characterIndex)
    {
      return type != null
          && Particles.Dash != null
          && characterIndex >= 0
          && characterIndex < Particles.Dash.Length
          && ReferenceEquals(type, Particles.Dash[characterIndex]);
    }

    /// <summary>
    /// Substitue au type partage celui du profil. Les trois proprietes rendent deja le
    /// type d'equipe quand il y en a une : on ne touche donc qu'au cas neutre, ou la
    /// couleur designe le joueur et non son camp.
    /// </summary>
    private static void ParticleType_postfix(Player __instance, ref ParticleType __result)
    {
      try
      {
        if (__result == null || __instance.TeamColor != Allegiance.Neutral)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(__instance.PlayerIndex);
        __result = ProfileParticles.For(profile, __result, __instance.CharacterIndex);
      }
      catch (Exception e)
      {
        Log.Error($"[Accents] type de particule : {e.Message}");
      }
    }

    // ------------------------------------------------------------------
    // Nom affiche au debut du round
    // ------------------------------------------------------------------

    /// <summary>
    /// PlayerIndicator.Render lit <c>ArcherData.Archers[characterIndex]</c> et alterne
    /// entre ses deux couleurs pour faire clignoter le nom. Rien n'y est parametrable,
    /// et reecrire ce rendu obligerait a recopier le sien - couronne, sinusoide et
    /// mesure de texte comprises. On prete donc les couleurs de l'archer le temps de
    /// l'appel.
    /// </summary>
    private static void Indicator_prefix(PlayerIndicator __instance)
    {
      borrowedArcher = null;

      try
      {
        using var data = DynamicData.For(__instance);

        int playerIndex = data.Get<int>("playerIndex");
        Color? dominant = SpriteRecolor.DominantColor(ProfileAssignment.Get(playerIndex));
        if (dominant == null)
        {
          return;
        }

        int characterIndex = data.Get<int>("characterIndex");
        if (!ArcherCatalog.Ready || characterIndex < 0 || characterIndex >= ArcherData.Archers.Length)
        {
          return;
        }

        ArcherData archer = ArcherData.Archers[characterIndex];
        if (archer == null)
        {
          return;
        }

        borrowedArcher = archer;
        savedColorA = archer.ColorA;
        savedColorB = archer.ColorB;

        // Le clignotement est conserve : une teinte et sa version eclaircie, la ou le
        // jeu alternait entre les deux couleurs de l'archer.
        archer.ColorA = dominant.Value;
        archer.ColorB = Lighten(dominant.Value, 1.35f);
      }
      catch (Exception e)
      {
        Log.Error($"[Accents] couleur du nom : {e.Message}");
        Restore();
      }
    }

    private static void Indicator_postfix()
    {
      Restore();
    }

    private static void Restore()
    {
      if (borrowedArcher == null)
      {
        return;
      }

      if (savedColorA.HasValue) borrowedArcher.ColorA = savedColorA.Value;
      if (savedColorB.HasValue) borrowedArcher.ColorB = savedColorB.Value;

      borrowedArcher = null;
      savedColorA = null;
      savedColorB = null;
    }

    // ------------------------------------------------------------------
    // Brume des archers a particules
    // ------------------------------------------------------------------

    /// <summary>
    /// Les archers marques PurpleParticles emettent en continu
    /// <c>Particles.PurpleAmbience</c>, un type statique partage par tous les joueurs
    /// qui ont pris cet archer.
    ///
    /// On ne peut pas se contenter d'en changer la couleur le temps de la mise a jour :
    /// une particule ne fige pas sa teinte a la naissance, <c>Particle.Update</c> relit
    /// <c>Type.Color</c> et <c>Type.Color2</c> a chaque bascule pour les faire alterner
    /// pendant toute sa vie. Une couleur rendue apres coup ressort donc sur la moitie
    /// des particules deja en vol.
    ///
    /// Chaque profil recoit donc son propre type, durable, et c'est lui qu'on emet.
    /// </summary>
    private static void PlayerUpdate_prefix(Player __instance)
    {
      pendingAmbience = null;

      try
      {
        if (__instance.ArcherData == null
            || !__instance.ArcherData.PurpleParticles
            || __instance.TeamColor != Allegiance.Neutral)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(__instance.PlayerIndex);
        Color? dominant = SpriteRecolor.DominantColor(profile);
        if (dominant == null)
        {
          return;
        }

        pendingAmbience = AmbienceFor(profile, dominant.Value);
      }
      catch (Exception e)
      {
        Log.Error($"[Accents] couleur de la brume : {e.Message}");
        pendingAmbience = null;
      }
    }

    private static void PlayerUpdate_postfix()
    {
      pendingAmbience = null;
    }

    /// <summary>
    /// Substitue la brume du profil a celle du jeu, pendant la mise a jour du joueur
    /// concerne. ParticleSystem.Emit ne dit pas qui emet : c'est le contexte pose par
    /// PlayerUpdate_prefix qui l'indique.
    /// </summary>
    private static void Emit_prefix(ref ParticleType type)
    {
      if (pendingAmbience != null && ReferenceEquals(type, Particles.PurpleAmbience))
      {
        type = pendingAmbience;
      }
    }

    private static ParticleType AmbienceFor(ProfileData profile, Color dominant)
    {
      if (ambience.TryGetValue(profile.Id, out var cached) && cached.Color == dominant)
      {
        return cached.Type;
      }

      // Les memes rapports que le jeu applique a la couleur d'archer : la brume reste
      // en retrait du personnage au lieu de rivaliser avec lui.
      var built = new ParticleType(Particles.PurpleAmbience)
      {
        Color = dominant * 0.8f,
        Color2 = dominant * 0.4f
      };

      ambience[profile.Id] = (dominant, built);
      return built;
    }
    /// <summary>Meme teinte, plus ou moins lumineuse. Au-dela de 1, on eclaircit.</summary>
    private static Color Lighten(Color color, float factor)
    {
      ColorFamilies.ToHsv(color, out float h, out float s, out float v);
      return ColorFamilies.FromHsv(h, s, v * factor);
    }
  }
}
