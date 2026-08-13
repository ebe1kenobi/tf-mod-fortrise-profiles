using System;
using System.Collections.Generic;
using FortRise;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Insere une lame ARCHER dans le menu principal, entre MODS et OPTIONS.
  ///
  /// FortRise remplace deja CreateMain() en entier (MonoModReplace) pour y glisser
  /// sa lame MODS. Le repatcher par transpileur reviendrait a parier sur son IL, qui
  /// n'est pas du code du jeu mais du code de mod, susceptible de bouger a chaque
  /// version de FortRise. On se contente donc d'un postfix qui relit ce que
  /// CreateMain vient de poser et le reagence.
  ///
  /// A cet instant les boutons ne sont pas encore dans Layer.Entities :
  /// MainMenu.Update() n'appelle UpdateEntityLists() qu'apres le retour de
  /// CallStateFunc("Create"). D'ou la lecture de la file d'attente du calque en plus
  /// de sa liste d'entites.
  ///
  /// Les lames occupent le bas de l'ecran de 18 en 18 pixels, sans intervalle libre.
  /// Pour inserer la notre on remonte MODS d'un cran et on prend sa place : OPTIONS,
  /// CREDITS et QUIT ne bougent pas.
  /// </summary>
  public class MyMainMenu : IHookable
  {
    private const float BladeSpacing = 18f;
    private const string ProfilesLabel = "ARCHER";
    private const string ModsLabel = "MODS";
    private const string OptionsLabel = "OPTIONS";

    public static void Load(IHarmony harmony)
    {
      harmony.Patch(
          AccessTools.DeclaredMethod(typeof(MainMenu), nameof(MainMenu.CreateMain)),
          postfix: new HarmonyMethod(CreateMain_postfix)
      );
    }

    private static void CreateMain_postfix(MainMenu __instance)
    {
      try
      {
        InsertProfilesBlade(__instance);
      }
      catch (Exception e)
      {
        // Le menu principal reste jouable sans notre lame ; il ne l'est pas du tout
        // si on laisse remonter une exception depuis la construction d'un etat.
        Log.Error($"[MainMenu] insertion de la lame {ProfilesLabel} impossible : {e}");
      }
    }

    private static void InsertProfilesBlade(MainMenu menu)
    {
      List<BladeButton> blades = PendingBlades(menu);

      BladeButton mods = FindBlade(blades, ModsLabel);
      BladeButton options = FindBlade(blades, OptionsLabel);

      // Disposition inattendue (autre version de FortRise, autre mod passe avant) :
      // mieux vaut ne pas ajouter de lame que d'en poser une par-dessus une autre.
      if (mods == null || options == null)
      {
        Log.Info($"[MainMenu] lames {ModsLabel}/{OptionsLabel} introuvables, {ProfilesLabel} n'est pas ajoutee");
        return;
      }

      if (FindBlade(blades, ProfilesLabel) != null)
      {
        return;
      }

      // Effet de bord assume : FortRise dessine sa pastille "NEW!" de mises a jour a
      // une ordonnee fixe, calee sur la lame la plus haute telle qu'il l'a posee.
      // Elle designe donc ARCHER et non MODS quand des mises a jour attendent. Les
      // lames se suivent sans interstice et QUIT touche deja le bas de l'ecran : il
      // n'y a pas de place pour s'inserer sans deplacer MODS. Le choix est entre
      // cette pastille mal calee de temps en temps et une lame ailleurs qu'entre
      // MODS et OPTIONS ; pour ce dernier cas, poser ARCHER en profilesY -
      // BladeSpacing et ne pas deplacer MODS.
      float profilesY = mods.Position.Y;
      MoveBladeToY(mods, profilesY - BladeSpacing);

      var profiles = new BladeButton(profilesY, ProfilesLabel, () =>
      {
        MenuNav.Open(menu, ModRegisters.MenuState<UIProfilesMenu>());
      });

      // Abscisse d'origine, et non le retrait de 20 pixels que FortRise applique a sa
      // lame MODS : le libelle est aligne a droite depuis Position.X + 85, si bien
      // que decaler la lame decale la fin du texte. "MODS" y survit, un libelle
      // plus long deborde par la gauche et se fait rogner par le bord de l'ecran.
      menu.Add(profiles);

      mods.DownItem = profiles;
      profiles.UpItem = mods;
      profiles.DownItem = options;
      profiles.RightItem = mods.RightItem;
      options.UpItem = profiles;

      // Au retour de l'ecran des profils, c'est la lame ARCHER qui doit etre
      // surlignee. FortRise ne connait pas notre etat et retombe sinon sur FIGHT.
      MainMenu.MenuState profilesState = ModRegisters.MenuState<UIProfilesMenu>();
      if (profilesState != MainMenu.MenuState.PressStart && menu.OldState == profilesState)
      {
        menu.ToStartSelected = profiles;
      }
    }

    /// <summary>
    /// Les lames que CreateMain vient d'ajouter, qu'elles soient deja publiees dans
    /// la liste d'entites du calque ou encore dans sa file d'attente.
    /// </summary>
    private static List<BladeButton> PendingBlades(MainMenu menu)
    {
      var blades = new List<BladeButton>();

      // -1 est le calque des MenuItem : c'est celui que MenuItem impose a toutes ses
      // sous-classes, BladeButton compris.
      if (!menu.Layers.TryGetValue(-1, out Layer layer) || layer == null)
      {
        return blades;
      }

      Collect(layer.Entities, blades);

      using (var data = DynamicData.For(layer))
      {
        Collect(data.Get<List<Entity>>("toAdd"), blades);
      }

      return blades;
    }

    private static void Collect(List<Entity> source, List<BladeButton> into)
    {
      if (source == null)
      {
        return;
      }

      foreach (Entity entity in source)
      {
        if (entity is BladeButton blade && !into.Contains(blade))
        {
          into.Add(blade);
        }
      }
    }

    private static BladeButton FindBlade(List<BladeButton> blades, string label)
    {
      foreach (BladeButton blade in blades)
      {
        if (string.Equals(LabelOf(blade), label, StringComparison.OrdinalIgnoreCase))
        {
          return blade;
        }
      }

      return null;
    }

    /// <summary>
    /// Le libelle d'une lame n'est expose par aucun accesseur : BladeButton le garde
    /// dans un champ prive dont il ne se sert que pour le rendu.
    /// </summary>
    private static string LabelOf(BladeButton blade)
    {
      using (var data = DynamicData.For(blade))
      {
        return data.Get<string>("name");
      }
    }

    /// <summary>
    /// Deplace une lame verticalement.
    ///
    /// La position visible ne suffit pas : BladeButton rejoue en permanence des
    /// interpolations entre trois positions memorisees a la construction (au repos,
    /// selectionnee, hors ecran). Ne changer que Position ferait remonter la lame a
    /// sa hauteur d'origine des qu'on la survole.
    /// </summary>
    private static void MoveBladeToY(BladeButton blade, float y)
    {
      blade.Position.Y = y;

      using (var data = DynamicData.For(blade))
      {
        SetFieldY(data, "tweenTo", y);
        SetFieldY(data, "tweenFrom", y);
        SetFieldY(data, "selected", y);
      }
    }

    private static void SetFieldY(DynamicData data, string field, float y)
    {
      var value = data.Get<Vector2>(field);
      value.Y = y;
      data.Set(field, value);
    }
  }
}
