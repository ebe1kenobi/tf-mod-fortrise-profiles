using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TowerFall;

namespace TFModFortRiseArcher
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

    /// <summary>
    /// Les lignes affichees, pour pouvoir les retirer et les refaire sans changer
    /// d'ecran : elles gardent une copie du tableau de touches, donc revenir au
    /// mappage du jeu n'a d'effet visible qu'a la reconstruction.
    /// </summary>
    private readonly List<OptionsButton> shown = new List<OptionsButton>();

    protected UIProfileControlsBase(MainMenu main) : base(main)
    {
    }

    protected abstract string Title { get; }

    protected abstract void ClearCustom();

    protected abstract void AddBindings(List<OptionsButton> buttons);

    public override void Create()
    {
      MainMenu.MenuState editState = MenuNav.Arrive(Main, ModRegisters.MenuState<UIProfileEdit>());

      Profile = UIProfilesMenu.Editing;
      if (Profile == null)
      {
        MenuNav.Switch(Main, ModRegisters.MenuState<UIProfilesMenu>());
        return;
      }

      ScreenTitles.Apply(Main, CurrentState());
      Main.BackState = editState;
      Main.TweenBGCameraToY(2);

      Build(true);
    }

    /// <summary>
    /// (Re)pose les lignes de l'ecran.
    ///
    /// La reconstruction se fait ici et non par un changement d'etat : reaffecter
    /// l'etat courant ne declenche rien - MainMenu ne compare que switchTo a state -
    /// et "USE GLOBAL CONFIG" restait donc sans effet visible.
    /// </summary>
    private void Build(bool first)
    {
      foreach (OptionsButton button in shown)
      {
        Main.Remove(button);
      }

      shown.Clear();

      var buttons = new List<OptionsButton>
      {
        new OptionsButtonHeader(Title)
      };

      AddBindings(buttons);

      var reset = new OptionsButton("USE GLOBAL CONFIG");
      reset.SetCallbacks(() =>
      {
        ClearCustom();
        ProfileStorage.Save();
        Build(false);
      });
      buttons.Add(reset);

      Layout(buttons);
      Main.Add(buttons);
      shown.AddRange(buttons);

      // Le rang 0 est l'entete, qui ne se selectionne pas : c'est donc 1 le defaut,
      // et non zero. Au retour, la ligne qu'on avait quittee.
      MenuNav.Track(Main, buttons);
      int resume = first ? Math.Max(1, MenuNav.Resume(Main, buttons.Count)) : 1;

      MenuItem select = buttons.Count > 1 ? buttons[resume] : reset;
      Main.ToStartSelected = select;

      // Au premier affichage MainMenu rend le focus lui-meme a la fin de la
      // transition ; une reconstruction en place n'en a pas, il faut le poser.
      if (!first && !Main.Transitioning)
      {
        select.Selected = true;
      }
    }

    public override void Destroy()
    {
      shown.Clear();
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
      // L'esquive du jeu est une action UNIQUE : ces deux lignes la nourrissent
      // ensemble (voir ProfileControls.SyncDodge), au lieu d'une seule ligne a
      // laquelle on assignait une liste de boutons.
      AddExtra(buttons, "DASH LEFT",
          Profile.PadDashLeft, v => { Profile.PadDashLeft = v; SyncDodge(); });
      AddExtra(buttons, "DASH RIGHT",
          Profile.PadDashRight, v => { Profile.PadDashRight = v; SyncDodge(); });

      // Les deux pouvoirs. Le jeu ne les connait pas : c'est le mod Power qui les
      // lit, par interop, au lieu de ses LB/RB codes en dur.
      AddExtra(buttons, "POWER LEFT", Profile.PadPowerLeft, v => Profile.PadPowerLeft = v);
      AddExtra(buttons, "POWER RIGHT", Profile.PadPowerRight, v => Profile.PadPowerRight = v);

      // Pas de ligne pour MenuAlt. Le jeu la nomme "ALT DODGE" et elle merite son
      // nom : XGamepadInput la verse dans DodgeCheck a cote de Dodge, c'est donc une
      // TROISIEME touche d'esquive, en doublon des deux ci-dessus. Elle sert encore a
      // naviguer dans les menus (MenuAlt2), et garde pour cela la valeur du jeu - on
      // la retire du reglage, pas de la configuration.
      Add(buttons, "START", shown.Start, (config, value) => config.Start = value);
    }

    /// <summary>
    /// Une ligne dont la valeur n'appartient pas a GamepadConfig mais au profil.
    /// </summary>
    private void AddExtra(List<OptionsButton> buttons, string title, int[] current, Action<int[]> setter)
    {
      buttons.Add(new GamepadInputOptionsButton(title, ProfileControls.ToButtons(current), value =>
      {
        Editable();
        setter(ProfileControls.ToInts(value));
        ProfileStorage.Save();
      }));
    }

    /// <summary>
    /// Reverse les deux touches de dash dans l'esquive du jeu.
    ///
    /// Sans ce report, couper l'esquive en deux la supprimerait : TowerFall ne lit
    /// que son propre Dodge, il ignore tout de nos deux lignes.
    /// </summary>
    private void SyncDodge()
    {
      GamepadConfig config = Editable();
      config.Dodge = ProfileControls.ToButtons(
          ProfileControls.Union(Profile.PadDashLeft, Profile.PadDashRight));
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
      AddExtra(buttons, "DASH LEFT",
          Profile.KeyDashLeft, v => { Profile.KeyDashLeft = v; SyncDodge(); });
      AddExtra(buttons, "DASH RIGHT",
          Profile.KeyDashRight, v => { Profile.KeyDashRight = v; SyncDodge(); });

      AddExtra(buttons, "POWER LEFT", Profile.KeyPowerLeft, v => Profile.KeyPowerLeft = v);
      AddExtra(buttons, "POWER RIGHT", Profile.KeyPowerRight, v => Profile.KeyPowerRight = v);

      // Voir l'ecran manette : MenuAlt est une seconde touche d'esquive, retiree du
      // reglage pour cette raison. Au clavier elle ouvre en plus les listes des menus,
      // et garde donc la valeur du jeu.
      Add(buttons, "START", shown?.Start, (config, value) => config.Start = value);
    }

    /// <summary>Une ligne dont la valeur appartient au profil et non au jeu.</summary>
    private void AddExtra(List<OptionsButton> buttons, string title, int[] current, Action<int[]> setter)
    {
      buttons.Add(new KeyboardInputOptionsButton(title, ProfileControls.ToKeys(current), value =>
      {
        Editable();
        setter(ProfileControls.ToInts(value));
        ProfileStorage.Save();
      }));
    }

    /// <summary>Reverse les deux touches de dash dans l'esquive du jeu.</summary>
    private void SyncDodge()
    {
      KeyboardConfig config = Editable();
      config.Dodge = ProfileControls.ToKeys(
          ProfileControls.Union(Profile.KeyDashLeft, Profile.KeyDashRight));
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
