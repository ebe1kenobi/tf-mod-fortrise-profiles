using System;
using FortRise;
using Microsoft.Extensions.Logging;

namespace TFModFortRiseProfiles
{
  public class TFModFortRiseProfilesModule : Mod
  {
    public static TFModFortRiseProfilesModule Instance;

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

    public TFModFortRiseProfilesModule(IModContent content, IModuleContext context, ILogger logger)
        : base(content, context, logger)
    {
      Instance = this;
      Log.Backend = logger;

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

      // La forge d'archers. Cinq ecrans, atteints depuis la liste des profils : le
      // menu principal n'a plus de place pour une lame, ses lames se suivent sans
      // interstice et QUIT touche deja le bas de l'ecran.
      context.Registry.MenuStates.RegisterMenuState("ForgeList",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeList) });
      context.Registry.MenuStates.RegisterMenuState("ForgeEdit",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeEdit) });
      context.Registry.MenuStates.RegisterMenuState("ForgeFrames",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeFrames) });
      context.Registry.MenuStates.RegisterMenuState("ForgeFramePicker",
          new MenuStateConfiguration { MenuStateType = typeof(UIForgeFramePicker) });
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
    }
  }
}
