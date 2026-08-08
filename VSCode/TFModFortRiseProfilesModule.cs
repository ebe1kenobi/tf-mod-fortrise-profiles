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

      foreach (var hookable in Hookables)
      {
        hookable.GetMethod(nameof(IHookable.Load))!.Invoke(null, [context.Harmony]);
      }
    }
  }
}
