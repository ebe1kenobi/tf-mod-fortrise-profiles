using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Applique aux entrees du jeu la configuration de touches d'un profil, et la
  /// retire proprement.
  ///
  /// Le point delicat est que <c>XGamepadInput.Config</c> n'est pas une copie mais
  /// une <b>reference</b> vers <c>SaveData.Instance.Gamepad[index]</c>. Y placer
  /// directement l'objet du profil aurait deux effets indesirables : l'ecran Options
  /// du jeu, qui edite <c>Config</c> en place, ecrirait dans le profil sans le dire ;
  /// et le profil se retrouverait a servir de configuration globale une fois le
  /// rollcall quitte.
  ///
  /// D'ou la regle tenue ici : on installe toujours une <b>copie</b>, jamais l'objet
  /// du profil. Le profil ne se modifie que par son propre ecran. Et comme la
  /// configuration d'origine reste intacte dans SaveData, la restauration consiste
  /// simplement a remettre la reference d'avant.
  /// </summary>
  public static class ProfileControls
  {
    /// <summary>
    /// Conversions entre les enumerations d'entree du jeu et les entiers stockes
    /// dans le profil.
    ///
    /// Le fichier de profil ne contient que des nombres : il reste lisible, et il ne
    /// depend pas des valeurs d'une enumeration de XNA ou du jeu, qu'une mise a jour
    /// pourrait renumeroter.
    /// </summary>
    public static int[] ToInts(Buttons[] buttons)
    {
      if (buttons == null)
      {
        return null;
      }

      var values = new int[buttons.Length];
      for (int i = 0; i < buttons.Length; i++)
      {
        values[i] = (int)buttons[i];
      }

      return values;
    }

    public static int[] ToInts(Keys[] keys)
    {
      if (keys == null)
      {
        return null;
      }

      var values = new int[keys.Length];
      for (int i = 0; i < keys.Length; i++)
      {
        values[i] = (int)keys[i];
      }

      return values;
    }

    public static Buttons[] ToButtons(int[] values)
    {
      if (values == null)
      {
        return System.Array.Empty<Buttons>();
      }

      var buttons = new Buttons[values.Length];
      for (int i = 0; i < values.Length; i++)
      {
        buttons[i] = (Buttons)values[i];
      }

      return buttons;
    }

    public static Keys[] ToKeys(int[] values)
    {
      if (values == null)
      {
        return System.Array.Empty<Keys>();
      }

      var keys = new Keys[values.Length];
      for (int i = 0; i < values.Length; i++)
      {
        keys[i] = (Keys)values[i];
      }

      return keys;
    }

    /// <summary>Les deux listes bout a bout, sans doublon ni valeur nulle.</summary>
    public static int[] Union(int[] first, int[] second)
    {
      var all = new System.Collections.Generic.List<int>();

      if (first != null)
      {
        all.AddRange(first);
      }

      if (second != null)
      {
        foreach (int value in second)
        {
          if (!all.Contains(value))
          {
            all.Add(value);
          }
        }
      }

      return all.ToArray();
    }

    private sealed class Original
    {
      public PlayerInput Input;
      public object Config;
    }

    private static readonly Dictionary<int, Original> originals = new Dictionary<int, Original>();

    // ------------------------------------------------------------------
    // Copies
    // ------------------------------------------------------------------

    private static T[] CopyOf<T>(T[] source)
    {
      if (source == null)
      {
        return null;
      }

      var copy = new T[source.Length];
      Array.Copy(source, copy, source.Length);
      return copy;
    }

    public static GamepadConfig Clone(GamepadConfig source)
    {
      if (source == null)
      {
        return GamepadConfig.GetDefault();
      }

      return new GamepadConfig
      {
        ButtonSet = source.ButtonSet,
        Left = CopyOf(source.Left),
        Right = CopyOf(source.Right),
        Up = CopyOf(source.Up),
        Down = CopyOf(source.Down),
        Jump = CopyOf(source.Jump),
        Shoot = CopyOf(source.Shoot),
        AltShoot = CopyOf(source.AltShoot),
        Dodge = CopyOf(source.Dodge),
        Arrows = CopyOf(source.Arrows),
        MenuAlt = CopyOf(source.MenuAlt),
        Start = CopyOf(source.Start),
        MoveXDeadzone = source.MoveXDeadzone,
        MoveYDeadzone = source.MoveYDeadzone
      };
    }

    public static KeyboardConfig Clone(KeyboardConfig source)
    {
      if (source == null)
      {
        return null;
      }

      return new KeyboardConfig
      {
        Left = CopyOf(source.Left),
        Right = CopyOf(source.Right),
        Up = CopyOf(source.Up),
        Down = CopyOf(source.Down),
        Jump = CopyOf(source.Jump),
        Shoot = CopyOf(source.Shoot),
        AltShoot = CopyOf(source.AltShoot),
        Dodge = CopyOf(source.Dodge),
        Arrows = CopyOf(source.Arrows),
        MenuAlt = CopyOf(source.MenuAlt),
        Start = CopyOf(source.Start)
      };
    }

    // ------------------------------------------------------------------
    // Configurations globales, servant de modele a un profil qui n'en a pas
    // ------------------------------------------------------------------

    public static GamepadConfig GlobalGamepad()
    {
      var saveData = SaveData.Instance;
      if (saveData?.Gamepad != null && saveData.Gamepad.Length > 0 && saveData.Gamepad[0] != null)
      {
        return saveData.Gamepad[0];
      }

      return GamepadConfig.GetDefault();
    }

    public static KeyboardConfig GlobalKeyboard()
    {
      var saveData = SaveData.Instance;
      if (saveData?.Keyboard != null && saveData.Keyboard.Length > 0)
      {
        return saveData.Keyboard[0];
      }

      return null;
    }

    // ------------------------------------------------------------------
    // Application
    // ------------------------------------------------------------------

    /// <summary>
    /// Installe sur l'entree de ce joueur la configuration de son profil. Sans profil,
    /// ou avec un profil qui n'a pas de configuration propre, remet la configuration
    /// globale.
    /// </summary>
    public static void Apply(int playerIndex)
    {
      try
      {
        if (TFGame.PlayerInputs == null || playerIndex < 0 || playerIndex >= TFGame.PlayerInputs.Length)
        {
          return;
        }

        PlayerInput input = TFGame.PlayerInputs[playerIndex];
        if (input == null)
        {
          return;
        }

        ProfileData profile = ProfileAssignment.Get(playerIndex);

        if (input is XGamepadInput pad)
        {
          if (profile?.Gamepad == null)
          {
            Restore(playerIndex);
            return;
          }

          Remember(playerIndex, input, pad.Config);
          pad.Config = Clone(profile.Gamepad);
          pad.RefreshButton();
          return;
        }

        if (input is KeyboardInput keyboard)
        {
          if (profile?.Keyboard == null)
          {
            Restore(playerIndex);
            return;
          }

          Remember(playerIndex, input, keyboard.Config);
          keyboard.Config = Clone(profile.Keyboard);
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Controls] application impossible pour P{playerIndex + 1} : {e.Message}");
      }
    }

    /// <summary>
    /// Remet la configuration qui etait en place avant l'arrivee du profil.
    /// </summary>
    public static void Restore(int playerIndex)
    {
      try
      {
        if (!originals.TryGetValue(playerIndex, out Original saved))
        {
          return;
        }

        originals.Remove(playerIndex);

        PlayerInput input = TFGame.PlayerInputs != null && playerIndex < TFGame.PlayerInputs.Length
            ? TFGame.PlayerInputs[playerIndex]
            : null;

        // Les entrees sont reconstruites quand les manettes changent : rendre sa
        // configuration a une instance qui n'est plus branchee sur ce joueur
        // toucherait quelqu'un d'autre.
        if (input == null || !ReferenceEquals(input, saved.Input))
        {
          return;
        }

        if (input is XGamepadInput pad && saved.Config is GamepadConfig gamepadConfig)
        {
          pad.Config = gamepadConfig;
          pad.RefreshButton();
          return;
        }

        if (input is KeyboardInput keyboard && saved.Config is KeyboardConfig keyboardConfig)
        {
          keyboard.Config = keyboardConfig;
        }
      }
      catch (Exception e)
      {
        Log.Error($"[Controls] restauration impossible pour P{playerIndex + 1} : {e.Message}");
      }
    }

    public static void RestoreAll()
    {
      foreach (int playerIndex in new List<int>(originals.Keys))
      {
        Restore(playerIndex);
      }
    }

    private static void Remember(int playerIndex, PlayerInput input, object config)
    {
      // Seulement la premiere fois : au deuxieme passage la valeur en place est deja
      // celle d'un profil, la memoriser perdrait la configuration d'origine.
      if (originals.ContainsKey(playerIndex))
      {
        return;
      }

      originals[playerIndex] = new Original { Input = input, Config = config };
    }
  }
}
