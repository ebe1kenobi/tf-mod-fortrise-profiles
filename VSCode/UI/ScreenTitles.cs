using System;
using System.Collections.Generic;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Donne un bandeau de titre a un etat de menu ajoute par un mod.
  ///
  /// ScreenTitle associe une texture a chaque MenuState via un dictionnaire prive
  /// rempli dans son constructeur ; FortRise y ajoute ses propres etats de la meme
  /// facon. Un etat absent du dictionnaire ne fait pas planter ChangeState, mais
  /// laisse a l'ecran le titre de l'ecran precedent, ce qui est pire qu'un titre
  /// generique.
  ///
  /// L'inscription se fait depuis Create() et non une fois pour toutes : le
  /// ScreenTitle est reconstruit avec chaque MainMenu. MainMenu.Update() appelle
  /// ChangeState apres CallStateFunc("Create"), le titre est donc pris en compte des
  /// la transition en cours.
  /// </summary>
  internal static class ScreenTitles
  {
    public static void Apply(MainMenu main, MainMenu.MenuState state, string atlasKey = "menuTitles/options")
    {
      try
      {
        ScreenTitle screenTitle = main?.ScreenTitle;
        if (screenTitle == null)
        {
          return;
        }

        using (var data = DynamicData.For(screenTitle))
        {
          var textures = data.Get<Dictionary<MainMenu.MenuState, Subtexture>>("textures");
          if (textures == null)
          {
            return;
          }

          textures[state] = TFGame.MenuAtlas[atlasKey];
        }
      }
      catch (Exception e)
      {
        // Un titre manquant est un defaut cosmetique : il ne doit pas empecher
        // l'ecran de s'ouvrir.
        Log.Error($"[ScreenTitles] titre non enregistre : {e.Message}");
      }
    }
  }
}
