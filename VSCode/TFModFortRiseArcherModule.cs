using System;
using FortRise;
using Microsoft.Extensions.Logging;

namespace TFModFortRiseArcher
{
  public class TFModFortRiseArcherModule : Mod
  {
    public static TFModFortRiseArcherModule Instance;

    internal Type[] Hookables = [
        typeof(MyMainMenu),
        typeof(MyRollcallElement),
        typeof(MyPlayerIndicator),
        typeof(MyVersusRoundResults),
        typeof(MyProfileSfx),
        typeof(MyRoundLogic),
        typeof(MyPlayerSprites),
        typeof(MyProfilePortraits),
        typeof(MySameArcher),
        typeof(MyProfileAccents),
    ];

    // Permet a CustomName de constater que Profiles tient le rollcall et de laisser
    // le bouton Y tranquille.
    public override object GetApi()
    {
      return new ApiImplementation();
    }

    public TFModFortRiseArcherModule(IModContent content, IModuleContext context, ILogger logger)
        : base(content, context, logger)
    {
      Instance = this;
      Log.Backend = logger;

      // Avant TOUT le reste : le mod s'appelait "Ebe1.Profiles", et son dossier de
      // sauvegarde a change de nom avec lui. Rien ne doit lire un profil ni un
      // reglage avant que les anciens fichiers soient a leur nouvelle place.
      StorageMigration.Run(context.Storage.StoragePath, content.Metadata.Name);

      // Les deux ecrans sont declares comme des etats du menu principal : c'est ce
      // que MainMenu sait faire transitionner, titrer et sortir au bouton retour.
      // L'enregistrement passe par une file interne a FortRise, l'identifiant final
      // n'est donc pas forcement resolu ici ; ModRegisters.MenuState<T>() est relu a
      // chaque fois qu'on en a besoin plutot que memorise.
      context.Registry.MenuStates.RegisterMenuState("ProfilesList",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfilesMenu) });
      context.Registry.MenuStates.RegisterMenuState("ProfileEdit",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileEdit) });
      context.Registry.MenuStates.RegisterMenuState("ProfileSounds",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileSounds) });
      context.Registry.MenuStates.RegisterMenuState("ProfileSoundPicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileSoundPicker) });
      context.Registry.MenuStates.RegisterMenuState("ProfileGamepad",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileGamepad) });
      context.Registry.MenuStates.RegisterMenuState("ProfileKeyboard",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileKeyboard) });
      context.Registry.MenuStates.RegisterMenuState("ProfileColors",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileColors) });
      context.Registry.MenuStates.RegisterMenuState("ProfileColorGroups",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileColorGroups) });
      context.Registry.MenuStates.RegisterMenuState("ProfileTrials",

          new MenuStateConfiguration { MenuStateType = typeof(UIProfileTrials) });

      context.Registry.MenuStates.RegisterMenuState("ProfileTrialImport",

          new MenuStateConfiguration { MenuStateType = typeof(UIProfileTrialImport) });

      context.Registry.MenuStates.RegisterMenuState("ProfileAdjust",

          new MenuStateConfiguration { MenuStateType = typeof(UIProfileAdjust) });

      context.Registry.MenuStates.RegisterMenuState("ProfileImages",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileImages) });
      context.Registry.MenuStates.RegisterMenuState("ProfileImagePicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIProfileImagePicker) });

      // Partage par la fiche d'un profil et par celle d'un archer forge : les deux y
      // choisissent un fichier de musique de victoire, dans la meme banque.
      context.Registry.MenuStates.RegisterMenuState("MusicPicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIMusicPicker) });

      // La forge d'archers. Cinq ecrans, atteints depuis la liste des profils : le
      // menu principal n'a plus de place pour une lame, ses lames se suivent sans
      // interstice et QUIT touche deja le bas de l'ecran.
      context.Registry.MenuStates.RegisterMenuState("ForgeList",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeList) });
      context.Registry.MenuStates.RegisterMenuState("ForgeImport",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeImport) });
      context.Registry.MenuStates.RegisterMenuState("ForgeEdit",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeEdit) });
      context.Registry.MenuStates.RegisterMenuState("ForgeFrames",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeFrames) });
      context.Registry.MenuStates.RegisterMenuState("ForgeFramePicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeFramePicker) });
      context.Registry.MenuStates.RegisterMenuState("ForgeAdjust",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeAdjust) });
      context.Registry.MenuStates.RegisterMenuState("ForgeLayers",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeLayers) });
      context.Registry.MenuStates.RegisterMenuState("ForgeVoice",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeVoice) });
      context.Registry.MenuStates.RegisterMenuState("ForgeVoicePicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeVoicePicker) });

      foreach (var hookable in Hookables)
      {
        hookable.GetMethod(nameof(IHookable.Load))!.Invoke(null, [context.Harmony]);
      }

      // On retient l'interop sans interroger : voir PowerImport.Bind. La resolution
      // a lieu a l'ouverture d'une fiche, quand tous les mods sont charges.
      PowerImport.Bind(context.Interop);
    }
  }
}
