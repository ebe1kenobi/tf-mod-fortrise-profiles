using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Socle commun aux deux ecrans de remappage d'un profil.
  ///
  /// Les lignes de saisie sont celles de FortRise (GamepadInputOptionsButton,
  /// KeyboardInputOptionsButton) : elles savent deja ecouter une touche, dessiner
  /// l'icone du bouton et renseigner le guide en bas d'ecran. Elles passent par
  /// QueueToApply, si bien que les changements ne sont ecrits qu'apres le "APPLY ALL
  /// THE CHANGES ?" pose au retour - c'est la convention de l'ecran d'options du jeu,
  /// autant la garder ici plutot que d'inventer un autre geste.
  ///
  /// Manette et clavier ont chacun leur ecran : les deux reunis feraient dix-huit
  /// lignes, soit plus haut que l'ecran, alors que le menu principal ne fait pas
  /// defiler ce genre de liste.
  /// </summary>
  public abstract class UIProfileControlsBase : CustomMenuState
  {
    private const float RowX = 200f;
    private const float FirstRowY = 50f;
    private const float RowStep = 13f;

    protected ProfileData Profile;

    protected UIProfileControlsBase(MainMenu main) : base(main)
    {
    }

    protected abstract string Title { get; }

    protected abstract void ClearCustom();

    protected abstract void AddBindings(List<OptionsButton> buttons);

    public override void Create()
    {
      MainMenu.MenuState editState = ModRegisters.MenuState<UIProfileEdit>();

      Profile = UIProfilesMenu.Editing;
      if (Profile == null)
      {
        Main.State = ModRegisters.MenuState<UIProfilesMenu>();
        return;
      }

      ScreenTitles.Apply(Main, CurrentState());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      var buttons = new List<OptionsButton>
      {
        new OptionsButtonHeader(Title)
      };

      AddBindings(buttons);

      var reset = new OptionsButton("USE GLOBAL CONFIG");
      reset.SetCallbacks(() =>
      {
        ClearCustom();

        // Les lignes de saisie gardent une copie du tableau de touches : les
        // reconstruire est le seul moyen de faire reapparaitre les valeurs globales.
        Main.State = CurrentState();
      });
      buttons.Add(reset);

      Layout(buttons);
      Main.Add(buttons);

      Main.ToStartSelected = buttons.Count > 1 ? buttons[1] : reset;
    }

    public override void Destroy()
    {
      ProfileStorage.Save();
    }

    protected abstract MainMenu.MenuState CurrentState();

    /// <summary>
    /// Positionne les lignes et les relie entre elles. Les entetes sont sautes dans
    /// le chainage : ils s'affichent mais ne se selectionnent pas, comme dans l'ecran
    /// d'options du jeu.
    /// </summary>
    private static void Layout(List<OptionsButton> buttons)
    {
      for (int i = 0; i < buttons.Count; i++)
      {
        OptionsButton button = buttons[i];
        float y = FirstRowY + i * RowStep;

        button.TweenTo = new Vector2(RowX, y);
        button.Position = button.TweenFrom = new Vector2(i % 2 == 0 ? -160f : 480f, y);
      }

      OptionsButton previous = null;
      foreach (OptionsButton button in buttons)
      {
        if (button is OptionsButtonHeader)
        {
          continue;
        }

        if (previous != null)
        {
          previous.DownItem = button;
          button.UpItem = previous;
        }

        previous = button;
      }
    }
  }

  /// <summary>Remappage manette d'un profil.</summary>
  public class UIProfileGamepad : UIProfileControlsBase
  {
    public UIProfileGamepad(MainMenu main) : base(main)
    {
    }

    protected override string Title => "GAMEPAD";

    protected override void ClearCustom()
    {
      Profile.Gamepad = null;
    }

    protected override MainMenu.MenuState CurrentState()
    {
      return ModRegisters.MenuState<UIProfileGamepad>();
    }

    /// <summary>
    /// Le mappage du profil s'il en a un, sinon celui du jeu : l'ecran montre donc
    /// toujours ce qui s'applique reellement, et la premiere modification materialise
    /// une configuration propre au profil.
    /// </summary>
    private GamepadConfig Shown()
    {
      return Profile.Gamepad ?? ProfileControls.GlobalGamepad();
    }

    private GamepadConfig Editable()
    {
      Profile.Gamepad ??= ProfileControls.Clone(ProfileControls.GlobalGamepad());
      return Profile.Gamepad;
    }

    protected override void AddBindings(List<OptionsButton> buttons)
    {
      GamepadConfig shown = Shown();

      Add(buttons, "JUMP", shown.Jump, (config, value) => config.Jump = value);
      Add(buttons, "SHOOT", shown.Shoot, (config, value) => config.Shoot = value);
      Add(buttons, "ALT SHOOT", shown.AltShoot, (config, value) => config.AltShoot = value);
      Add(buttons, "ARROWS SWAP", shown.Arrows, (config, value) => config.Arrows = value);
      Add(buttons, "DODGE", shown.Dodge, (config, value) => config.Dodge = value);
      Add(buttons, "ALT DODGE", shown.MenuAlt, (config, value) => config.MenuAlt = value);
      Add(buttons, "START", shown.Start, (config, value) => config.Start = value);
    }

    private void Add(List<OptionsButton> buttons, string title, Buttons[] current, Action<GamepadConfig, Buttons[]> setter)
    {
      buttons.Add(new GamepadInputOptionsButton(title, current ?? Array.Empty<Buttons>(), value =>
      {
        setter(Editable(), value);
        ProfileStorage.Save();
      }));
    }
  }

  /// <summary>Remappage clavier d'un profil.</summary>
  public class UIProfileKeyboard : UIProfileControlsBase
  {
    public UIProfileKeyboard(MainMenu main) : base(main)
    {
    }

    protected override string Title => "KEYBOARD";

    protected override void ClearCustom()
    {
      Profile.Keyboard = null;
    }

    protected override MainMenu.MenuState CurrentState()
    {
      return ModRegisters.MenuState<UIProfileKeyboard>();
    }

    private KeyboardConfig Shown()
    {
      return Profile.Keyboard ?? ProfileControls.GlobalKeyboard();
    }

    private KeyboardConfig Editable()
    {
      Profile.Keyboard ??= ProfileControls.Clone(ProfileControls.GlobalKeyboard()) ?? new KeyboardConfig();
      return Profile.Keyboard;
    }

    protected override void AddBindings(List<OptionsButton> buttons)
    {
      KeyboardConfig shown = Shown();

      // Le jeu peut n'avoir aucune configuration clavier enregistree : on affiche
      // alors des lignes vides plutot que de refuser l'ecran.
      Add(buttons, "JUMP", shown?.Jump, (config, value) => config.Jump = value);
      Add(buttons, "SHOOT", shown?.Shoot, (config, value) => config.Shoot = value);
      Add(buttons, "ALT SHOOT", shown?.AltShoot, (config, value) => config.AltShoot = value);
      Add(buttons, "ARROWS SWAP", shown?.Arrows, (config, value) => config.Arrows = value);
      Add(buttons, "DODGE", shown?.Dodge, (config, value) => config.Dodge = value);
      Add(buttons, "ALT DODGE", shown?.MenuAlt, (config, value) => config.MenuAlt = value);
      Add(buttons, "START", shown?.Start, (config, value) => config.Start = value);
    }

    private void Add(List<OptionsButton> buttons, string title, Keys[] current, Action<KeyboardConfig, Keys[]> setter)
    {
      buttons.Add(new KeyboardInputOptionsButton(title, current ?? Array.Empty<Keys>(), value =>
      {
        setter(Editable(), value);
        ProfileStorage.Save();
      }));
    }
  }
}
