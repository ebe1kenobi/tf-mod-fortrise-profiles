using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Clavier virtuel de saisie d'un nom, repris du mod CustomName et adapte au menu
  /// principal.
  ///
  /// Deux traits tenus de l'original :
  ///
  /// - la grille est une vraie grille : haut/bas/gauche/droite suivent les lignes et
  ///   les colonnes, et le curseur defile tant qu'une direction est maintenue ;
  ///
  /// - la frappe passe par la saisie texte du systeme (TextInputEXT) et non par des
  ///   codes de touches. Keys.D8 designe la touche a la position du 8 en QWERTY : en
  ///   AZERTY elle produisait "8" au lieu du tiret bas. TextInputEXT rend le
  ///   caractere reellement tape, disposition clavier et Shift compris.
  ///
  /// Ce qui change par rapport au rollcall : il n'y a plus un joueur proprietaire de
  /// la saisie, la navigation est donc ouverte a toutes les manettes. Elle reste en
  /// revanche fermee aux entrees clavier, car TowerFall y mappe MenuConfirm sur la
  /// touche de saut et MenuBack sur celle de tir : taper ces lettres validerait ou
  /// fermerait l'ecran.
  ///
  /// Tant que ce clavier est affiche, le menu est neutralise (CanAct a false et
  /// element courant deselectionne), sans quoi les MenuItem sous-jacents liraient
  /// les memes touches.
  /// </summary>
  public class VirtualKeyboard : Entity
  {
    private const int Columns = 10;

    // Une case par caractere saisissable. Tout caractere tape qui n'y figure pas est
    // refuse : la police du jeu ne sait pas tout rendre.
    private const string Charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .'-_?!:()\\/";

    // Repetition quand on maintient une direction : une premiere pause pour
    // permettre le pas a pas, puis un defilement continu.
    private const float RepeatFirstDelay = 0.35f;
    private const float RepeatDelay = 0.07f;

    private static readonly Vector2 GridOrigin = new Vector2(70f, 92f);
    private const float CellW = 18f;
    private const float CellH = 16f;

    private readonly string title;
    private readonly Func<string, string> validate;
    private readonly Action<string> onConfirm;

    private string current;
    private int selected;
    private float repeatTimer;
    private int heldX, heldY;
    private float blink;
    private string error;

    private MainMenu menu;
    private MenuItem restoreSelection;
    private bool restoreCanAct;

    /// <param name="title">Ligne affichee en haut de l'ecran de saisie.</param>
    /// <param name="initial">Texte deja present dans le champ a l'ouverture.</param>
    /// <param name="validate">
    /// Rend null si le nom est acceptable, sinon le message d'erreur a afficher.
    /// La validation est laissee a l'appelant : le clavier ne connait ni les profils
    /// ni leurs regles d'unicite.
    /// </param>
    /// <param name="onConfirm">Appele avec le nom retenu, apres fermeture.</param>
    public VirtualKeyboard(string title, string initial, Func<string, string> validate, Action<string> onConfirm)
        : base(0)
    {
      this.title = title;
      this.current = initial ?? "";
      this.validate = validate;
      this.onConfirm = onConfirm;
      Depth = -100000;
    }

    public override void Added()
    {
      base.Added();

      menu = Scene as MainMenu;
      if (menu != null)
      {
        restoreCanAct = menu.CanAct;
        menu.CanAct = false;
        restoreSelection = SelectedItem(menu);
        if (restoreSelection != null)
        {
          restoreSelection.Selected = false;
        }
      }

      TextInputEXT.TextInput += HandleChar;
      TextInputEXT.StartTextInput();
      Sounds.ui_pause.Play(160f);
    }

    public override void Removed()
    {
      base.Removed();

      // L'evenement est statique : ne pas se desabonner laisserait cet ecran capter
      // la frappe pour toute la duree du jeu.
      TextInputEXT.TextInput -= HandleChar;
      TextInputEXT.StopTextInput();

      if (menu != null)
      {
        menu.CanAct = restoreCanAct;

        // La ligne n'est rendue que si l'ecran n'a pas change entre-temps : valider
        // un nom peut enchainer sur l'ecran d'edition, et le menu est alors en pleine
        // transition. Redonner le focus a une ligne sortante en laisserait deux
        // actives a l'arrivee, toutes deux reagissant aux memes touches.
        if (restoreSelection != null
            && restoreSelection.Scene != null
            && restoreSelection.CreatedState == menu.State)
        {
          restoreSelection.Selected = true;
        }

        // Le maintien d'une direction pendant la saisie ne doit pas continuer a
        // faire defiler le menu qu'on vient de retrouver.
        MenuInput.Clear();
      }
    }

    private static MenuItem SelectedItem(MainMenu menu)
    {
      if (!menu.Layers.TryGetValue(-1, out Layer layer) || layer == null)
      {
        return null;
      }

      foreach (Entity entity in layer.Entities)
      {
        if (entity is MenuItem item && item.Selected)
        {
          return item;
        }
      }

      return null;
    }

    /// <summary>
    /// Caractere reellement produit par le clavier (disposition et Shift compris).
    /// FNA fait passer par ce meme canal le retour arriere (8) et l'entree (10).
    /// </summary>
    private void HandleChar(char c)
    {
      if (c == 8)
      {
        Backspace();
        return;
      }

      if (c == 10 || c == 13)
      {
        Validate();
        return;
      }

      if (c == '\t')
      {
        return;
      }

      char upper = char.ToUpperInvariant(c);
      if (Charset.IndexOf(upper) < 0)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      Append(upper);
    }

    private void Append(char c)
    {
      if (current.Length >= ProfileData.MaxNameLength)
      {
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      current += c;
      error = null;
      Sounds.ui_move1.Play(160f, 1f);
    }

    private void Backspace()
    {
      if (current.Length == 0)
      {
        return;
      }

      current = current.Substring(0, current.Length - 1);
      error = null;
      Sounds.ui_move1.Play(160f, 1f);
    }

    private void Validate()
    {
      string name = ProfileStorage.Normalize(current);

      string rejected = validate?.Invoke(name);
      if (rejected != null)
      {
        error = rejected;
        Sounds.ui_invalid.Play(160f, 1f);
        return;
      }

      Sounds.ui_click.Play(160f, 1f);
      Close();
      onConfirm?.Invoke(name);
    }

    private void Cancel()
    {
      Sounds.ui_clickBack.Play(160f, 1f);
      Close();
    }

    private void Close()
    {
      RemoveSelf();
    }

    public override void Update()
    {
      base.Update();
      repeatTimer -= Engine.DeltaTime;
      blink += Engine.DeltaTime;

      // Seules les touches que la saisie texte ne transmet pas sont lues ici.
      if (MInput.Keyboard != null)
      {
        if (MInput.Keyboard.Pressed(Keys.Escape))
        {
          Cancel();
          return;
        }

        if (MInput.Keyboard.Pressed(Keys.Delete))
        {
          Backspace();
        }
      }

      UpdateGamepad();
    }

    /// <summary>
    /// Navigation a la manette. Les entrees clavier sont ecartees : c'est par la
    /// frappe que le joueur au clavier saisit son nom.
    /// </summary>
    private void UpdateGamepad()
    {
      int dx = 0;
      int dy = 0;
      bool confirm = false;
      bool backspace = false;
      bool submit = false;
      bool cancel = false;

      foreach (PlayerInput input in MenuInput.MenuInputs)
      {
        if (input == null || input is KeyboardInput)
        {
          continue;
        }

        // ...Check et non le pressed : c'est ce qui fait defiler le curseur tant que
        // la direction est maintenue.
        if (input.MenuRightCheck) dx = 1;
        else if (input.MenuLeftCheck) dx = -1;

        if (input.MenuDownCheck) dy = 1;
        else if (input.MenuUpCheck) dy = -1;

        confirm |= input.MenuConfirm;
        backspace |= input.MenuAlt;
        submit |= input.MenuStart;
        cancel |= input.MenuBack;
      }

      if (cancel)
      {
        Cancel();
        return;
      }

      if (submit)
      {
        Validate();
        return;
      }

      if (confirm)
      {
        Append(Charset[selected]);
      }

      if (backspace)
      {
        Backspace();
      }

      UpdateRepeat(dx, dy);
    }

    /// <summary>
    /// Deplacement au maintien : un pas immediat, une pause, puis un defilement
    /// continu. Le compteur repart des que la direction change ou est relachee, pour
    /// que le pas a pas reste possible.
    /// </summary>
    private void UpdateRepeat(int dx, int dy)
    {
      if (dx == 0 && dy == 0)
      {
        heldX = heldY = 0;
        repeatTimer = 0f;
        return;
      }

      if (dx != heldX || dy != heldY)
      {
        heldX = dx;
        heldY = dy;
        repeatTimer = RepeatFirstDelay;
        Move(dx, dy);
        return;
      }

      if (repeatTimer <= 0f)
      {
        repeatTimer = RepeatDelay;
        Move(dx, dy);
      }
    }

    private void Move(int dx, int dy)
    {
      int index = selected + dx + dy * Columns;

      // On borne au lieu de boucler : sauter d'un bout a l'autre de la grille sur une
      // simple pression est desorientant.
      if (index < 0 || index >= Charset.Length)
      {
        return;
      }

      selected = index;
      Sounds.ui_move1.Play(160f, 1f);
    }

    public override void Render()
    {
      Draw.Rect(0f, 0f, 320f, 240f, new Color(0, 0, 0, 200));

      Draw.OutlineTextCentered(TFGame.Font, title, new Vector2(160f, 26f), Color.White, 1.2f);

      // Curseur clignotant : montre qu'on peut taper directement.
      string shown = current + (((int)(blink * 2f) % 2) == 0 ? "_" : " ");
      Draw.OutlineTextCentered(TFGame.Font, shown, new Vector2(160f, 52f), Calc.HexToColor("FFEC5E"), 1.4f);

      if (error != null)
      {
        Draw.TextCentered(TFGame.Font, error, new Vector2(160f, 70f), Color.Red);
      }

      for (int i = 0; i < Charset.Length; i++)
      {
        int col = i % Columns;
        int row = i / Columns;
        Vector2 pos = GridOrigin + new Vector2(col * CellW, row * CellH);

        bool active = i == selected;

        // "SP" et non "_" : le tiret bas est un caractere a part entiere de la
        // grille, les confondre rendrait l'un des deux introuvable.
        string label = Charset[i] == ' ' ? "SP" : Charset[i].ToString();

        if (active)
        {
          Draw.Rect(pos.X - 8f, pos.Y - 7f, 16f, 14f, Color.White * 0.25f);
        }

        Draw.TextCentered(TFGame.Font, label, pos, active ? Calc.HexToColor("FFEC5E") : Color.White);
      }

      Draw.TextCentered(TFGame.Font, "TYPE ON KEYBOARD OR PICK A LETTER",
          new Vector2(160f, 196f), Color.Gray * 0.8f);
      Draw.TextCentered(TFGame.Font, "A: LETTER  RB: DELETE  START/ENTER: OK  B/ESC: CANCEL",
          new Vector2(160f, 210f), Color.Gray * 0.8f);
    }
  }
}
