using System;
using System.Collections.Generic;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Choix du profil sur l'ecran de selection des archers.
  ///
  /// Le bouton Y correspond a l'action "arrows" du jeu, donc a
  /// <c>InputState.ArrowsPressed</c>.
  ///
  /// Le cycle compte un cran de plus que la liste : ...profil N, puis "aucun profil"
  /// ou le joueur redevient P1/P2, puis retour au premier. Sans ce cran on ne
  /// pourrait plus se detacher d'un profil ni revenir en arriere autrement qu'en
  /// refaisant tout le tour, et le nom affiche est ce qui rend le passage visible.
  ///
  /// Choisir un profil applique son archer et sa tenue, puis verrouille les fleches :
  /// tant qu'un profil est actif, l'archer est celui du profil.
  /// </summary>
  public class MyRollcallElement : IHookable
  {
    /// <summary>Hauteur du nom au-dessus du centre de l'element.</summary>
    private const float NameOffsetY = -72f;

    /// <summary>
    /// En mode 8 joueurs les elements sont plus serres : le nom doit descendre pour
    /// ne pas sortir de l'ecran sur la rangee du haut.
    /// </summary>
    private const float NameOffsetYWide = -52f;

    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(RollcallElement), "NotJoinedUpdate"),
          prefix: new HarmonyMethod(NotJoinedUpdate_prefix),
          postfix: new HarmonyMethod(NotJoinedUpdate_postfix)
      );

      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(RollcallElement), nameof(RollcallElement.Render)),
          postfix: new HarmonyMethod(Render_postfix)
      );

      harmony.Patch(
          AccessTools.DeclaredConstructor(typeof(RollcallElement), [typeof(int)]),
          postfix: new HarmonyMethod(ctor_postfix)
      );
    }

    /// <summary>
    /// Reinstalle le mappage de touches en revenant sur l'ecran de selection.
    ///
    /// Le rattachement des profils survit d'une partie a l'autre, mais pas les objets
    /// PlayerInput : ils sont reconstruits des que les manettes changent, et
    /// reprennent alors la configuration globale. Sans ce rappel, un profil resterait
    /// affiche avec un mappage qui n'est plus le sien.
    /// </summary>
    private static void ctor_postfix(RollcallElement __instance, int playerIndex)
    {
      try
      {
        using var data = DynamicData.For(__instance);
        ClaimPortrait(__instance, data, playerIndex);

        if (ProfileAssignment.Get(playerIndex) != null)
        {
          ProfileControls.Apply(playerIndex);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Rollcall] mappage non reapplique pour P{playerIndex + 1} : {e}");
      }
    }

    /// <summary>
    /// Verrouille le changement d'archer a la fleche tant qu'un profil impose le sien.
    ///
    /// On rend false plutot que de corriger apres coup : laisser le jeu changer
    /// d'archer pour le remettre ensuite declencherait a chaque pression l'animation
    /// de retournement du portrait.
    ///
    /// Le verrou ne s'applique que si l'archer affiche est bien celui du profil. S'il
    /// n'a pas pu etre applique - un autre joueur l'avait deja pris - bloquer les
    /// fleches enfermerait le joueur sur un archer qu'il n'a pas choisi.
    /// </summary>
    private static bool NotJoinedUpdate_prefix(RollcallElement __instance)
    {
      try
      {
        using var data = DynamicData.For(__instance);

        int playerIndex = data.Get<int>("playerIndex");
        ProfileData profile = ProfileAssignment.Get(playerIndex);
        if (profile == null)
        {
          return true;
        }

        var input = data.Get<PlayerInput>("input");
        if (input == null || (!input.MenuLeft && !input.MenuRight))
        {
          return true;
        }

        if (__instance.CharacterIndex != ArcherCatalog.IndexOf(profile))
        {
          return true;
        }

        // Meme condition que CanChangeSelection, qui est privee : sans elle le jeu
        // ne changerait rien de toute facon et laisserait la main aux autres
        // branches de NotJoinedUpdate. Les court-circuiter serait une regression.
        if (SaveData.Instance.Unlocks.TotalUnlockedArchers <= TFGame.PlayerAmount + 1)
        {
          return true;
        }

        Sounds.ui_invalid.Play(__instance.X, 1f);
        return false;
      }
      catch (Exception e)
      {
        Log.Error($"[Rollcall] verrou d'archer impossible : {e}");
        return true;
      }
    }

    private static void NotJoinedUpdate_postfix(RollcallElement __instance)
    {
      try
      {
        using var data = DynamicData.For(__instance);

        var input = data.Get<PlayerInput>("input");
        if (input == null)
        {
          return;
        }

        int playerIndex = data.Get<int>("playerIndex");
        InputState state = DynamicData.For(input).Invoke<InputState>("GetState");

        if (!state.ArrowsPressed)
        {
          return;
        }

        ProfileData next = NextInCycle(playerIndex);
        ProfileAssignment.Set(playerIndex, next);

        // Le geste du joueur prime sur un nom impose par un autre mod.
        PlayerNames.ClearOverride(playerIndex);

        // Le mappage de touches suit le profil : il est installe a la selection et
        // retire des qu'on repasse par le cran "aucun profil".
        ProfileControls.Apply(playerIndex);

        if (next != null)
        {
          ApplyArcher(__instance, data, playerIndex, next);
        }

        // Le portrait suit le profil : changer de profil change l'image personnelle,
        // ou la retire au cran "aucun profil".
        ClaimPortrait(__instance, data, playerIndex);

        Sounds.ui_move2.Play(__instance.X, 1f);
      }
      catch (Exception e)
      {
        // L'ecran de selection doit rester utilisable quoi qu'il arrive : sans lui on
        // ne peut plus lancer de partie du tout.
        Log.Error($"[Rollcall] selection de profil impossible : {e}");
      }
    }

    /// <summary>
    /// Cran suivant du cycle : les profils dans l'ordre, puis null pour "aucun
    /// profil", puis on repart du debut. Les profils qu'un autre joueur a deja pris
    /// sont sautes.
    /// </summary>
    private static ProfileData NextInCycle(int playerIndex)
    {
      List<ProfileData> profiles = ProfileStorage.Profiles;
      if (profiles.Count == 0)
      {
        return null;
      }

      // Le cycle a Count + 1 positions : 0..Count-1 pour les profils, Count pour le
      // cran "aucun profil". Le modulo est ce qui fait la boucle.
      int slots = profiles.Count + 1;
      int none = profiles.Count;

      ProfileData current = ProfileAssignment.Get(playerIndex);
      int position = current == null ? none : profiles.IndexOf(current);
      if (position < 0)
      {
        position = none;
      }

      for (int step = 1; step <= slots; step++)
      {
        int next = (position + step) % slots;

        if (next == none)
        {
          return null;
        }

        ProfileData candidate = profiles[next];
        if (!ProfileAssignment.TakenByOther(candidate, playerIndex))
        {
          return candidate;
        }
      }

      return null;
    }

    /// <summary>
    /// Applique l'archer et la tenue du profil a l'element de rollcall.
    ///
    /// Un archer deja pris par un autre joueur, ou non debloque, est laisse de cote :
    /// le profil est quand meme retenu, seul le portrait ne suit pas. Forcer le choix
    /// ici casserait l'invariant que le jeu maintient - deux joueurs ne peuvent pas
    /// avoir le meme archer - et deux profils ont le droit d'aimer le meme personnage.
    /// </summary>
    private static void ApplyArcher(RollcallElement element, DynamicData data, int playerIndex, ProfileData profile)
    {
      int wanted = ArcherCatalog.IndexOf(profile);
      if (!ArcherCatalog.Ready || wanted < 0 || wanted >= ArcherData.Amount)
      {
        return;
      }

      if (element.CharacterIndex != wanted)
      {
        if (TFGame.CharacterTaken(wanted) || !SaveData.Instance.Unlocks.GetArcherUnlocked(wanted))
        {
          return;
        }

        element.CharacterIndex = wanted;
      }

      var archerType = ArcherCatalog.TypeOf(profile);
      data.Set("archerType", archerType);
      TFGame.AltSelect[playerIndex] = archerType;

      var portrait = data.Get<ArcherPortrait>("portrait");
      portrait?.SetCharacter(wanted, archerType, 1);
    }

    /// <summary>
    /// Rattache le portrait de cet element a son joueur, pour que l'image personnelle
    /// du profil puisse s'y substituer. ArcherPortrait ne connait pas l'index du
    /// joueur : seul le rollcall le porte.
    /// </summary>
    private static void ClaimPortrait(RollcallElement element, DynamicData data, int playerIndex)
    {
      try
      {
        MyProfilePortraits.Claim(data.Get<ArcherPortrait>("portrait"), playerIndex);
      }
      catch (Exception e)
      {
        Log.Error($"[Rollcall] portrait non rattache pour P{playerIndex + 1} : {e.Message}");
      }
    }

    private static void Render_postfix(RollcallElement __instance)
    {
      try
      {
        using var data = DynamicData.For(__instance);

        int playerIndex = data.Get<int>("playerIndex");

        // Toujours un nom, y compris le "P1" du cran sans profil : c'est ce qui rend
        // le cycle lisible.
        string name = PlayerNames.Of(playerIndex);
        if (string.IsNullOrEmpty(name))
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(playerIndex);
        float offsetY = TFGame.Players.Length > 4 ? NameOffsetYWide : NameOffsetY;

        // Le nom prend la couleur dominante du profil quand il en a une : c'est ce qui
        // relie le libelle au personnage, surtout depuis que deux joueurs peuvent
        // partager un archer. Le jaune reste le repli d'un profil sans recoloration.
        Color color = profile == null
            ? Color.White
            : SpriteRecolor.DominantColor(profile) ?? Calc.HexToColor("FFEC5E");

        // Au-dessus du portrait : le bas de l'element porte deja l'icone de manette,
        // le nom de l'entree et, quand le joueur a rejoint, la gemme.
        Draw.OutlineTextCentered(TFGame.Font, name,
            __instance.Position + new Vector2(0f, offsetY),
            color,
            Color.Black);
      }
      catch (Exception e)
      {
        Log.Error($"[Rollcall] affichage du profil impossible : {e}");
      }
    }
  }
}
