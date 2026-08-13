using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace TFModFortRiseArcher
{
  /// <summary>
  /// Choix des WAV attaches a un evenement d'un profil.
  ///
  /// Chaque ligne est un fichier du vivier. Valider coche ou decoche : cocher copie
  /// le fichier dans le dossier du profil, decocher supprime la copie. L'ecriture est
  /// immediate et non differee a la sortie de l'ecran, pour que ce qu'affiche la case
  /// soit toujours l'etat reel du disque.
  ///
  /// Le bouton Alt joue le fichier survole, pour choisir a l'oreille plutot que sur
  /// un nom de fichier.
  /// </summary>
  public class UIProfileSoundPicker : CustomMenuState
  {
    private const float FirstRowY = 52f;
    private const float RowStep = 15f;
    private const float RowX = 20f;

    private ProfileData profile;
    private string soundEvent;

    public UIProfileSoundPicker(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState soundsState = ModRegisters.MenuState<UIProfileSounds>();

      profile = UIProfilesMenu.Editing;
      soundEvent = UIProfileSounds.EditingEvent;

      if (profile == null || string.IsNullOrEmpty(soundEvent))
      {
        MenuNav.Switch(Main, soundsState);
        return;
      }

      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileSoundPicker>());
      Main.BackState = MenuNav.Arrive(Main, soundsState);
      Main.TweenBGCameraToY(2);

      Main.Add(new UIPickerHeader(new Vector2(160f, 34f), profile.Name, soundEvent));

      IReadOnlyList<SoundFile> files = ProfileSfx.Pool;

      if (files.Count == 0)
      {
        Main.Add(new UIPoolHint(new Vector2(160f, 120f)));
        Main.MaxUICameraY = 0f;
        Main.ToStartSelected = null;
        return;
      }

      var rows = new List<UIMenuRow>();

      for (int i = 0; i < files.Count; i++)
      {
        SoundFile file = files[i];
        float y = FirstRowY + i * RowStep;
        var from = new Vector2(i % 2 == 0 ? -260f : 580f, y);

        var row = new UIMenuRow(new Vector2(RowX, y), from, Label(file))
        {
          ContentWidth = 285f,
          RightText = () =>
          {
            if (!ProfileSfx.IsAssigned(profile.Name, soundEvent, file.Name))
            {
              return file.Source == "MOD" ? "MOD [ ]" : "[ ]";
            }

            string mode = ProfileSfx.IsOccasional(profile, soundEvent, file.Name) ? "SOMETIMES" : "ALWAYS";
            return (file.Source == "MOD" ? "MOD [X] " : "[X] ") + mode;
          },
          OnConfirmed = () => Toggle(file),
          // La frequence se change a la fleche : le bouton Alt fait deja ecouter, et
          // un reglage a deux valeurs se prete bien au va-et-vient gauche/droite.
          OnLeft = () => ToggleMode(file),
          OnRight = () => ToggleMode(file),
          OnAlt = () => Preview(file),
          AltGuide = "PLAY"
        };

        rows.Add(row);
      }

      for (int i = 0; i < rows.Count; i++)
      {
        if (i > 0)
        {
          rows[i].UpItem = rows[i - 1];
        }

        if (i + 1 < rows.Count)
        {
          rows[i].DownItem = rows[i + 1];
        }
      }

      Main.Add(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 180f);
      // Le curseur retrouve la ligne qu'on avait quittee, et non la premiere :
      // sans cela, chaque aller-retour dans un sous-ecran oblige a redescendre.
      MenuNav.Track(Main, rows);
      Main.ToStartSelected = rows[MenuNav.Resume(Main, rows.Count)];
    }

    public override void Destroy()
    {
    }

    /// <summary>
    /// Nom du fichier, sans extension et tronque pour tenir dans la colonne.
    ///
    /// Mis en majuscules parce que la police du jeu ne dessine lisiblement que
    /// celles-ci : un nom de fichier tape en minuscules y est illisible. La
    /// provenance du fichier n'est pas marquee ici mais dans la colonne de droite,
    /// ou elle ne rogne pas la place du nom.
    /// </summary>
    private static string Label(SoundFile file)
    {
      string name = file.Name;
      if (name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
      {
        name = name.Substring(0, name.Length - 4);
      }

      name = name.ToUpperInvariant();

      if (name.Length > 16)
      {
        name = name.Substring(0, 15) + ".";
      }

      return name;
    }

    /// <summary>
    /// Bascule entre "a chaque fois" et "de temps en temps". Sans effet sur un fichier
    /// qui n'est pas affecte : il n'y aurait rien a regler.
    /// </summary>
    private void ToggleMode(SoundFile file)
    {
      if (!ProfileSfx.IsAssigned(profile.Name, soundEvent, file.Name))
      {
        return;
      }

      bool occasional = ProfileSfx.IsOccasional(profile, soundEvent, file.Name);
      ProfileSfx.SetOccasional(profile, soundEvent, file.Name, !occasional);
      ProfileStorage.Save();
    }

    private void Toggle(SoundFile file)
    {
      if (ProfileSfx.IsAssigned(profile.Name, soundEvent, file.Name))
      {
        ProfileSfx.Unassign(profile.Name, soundEvent, file.Name);
        ProfileSfx.SetOccasional(profile, soundEvent, file.Name, false);
        ProfileStorage.Save();
        return;
      }

      if (ProfileSfx.Assign(profile.Name, soundEvent, file))
      {
        Preview(file);
      }
      else
      {
        Sounds.ui_invalid.Play(160f, 1f);
      }
    }

    /// <summary>
    /// Fait entendre le fichier. Passe par le dossier du profil quand la copie existe
    /// deja, sinon decode directement depuis le vivier.
    /// </summary>
    private void Preview(SoundFile file)
    {
      try
      {
        using var stream = file.Open();
        var effect = Microsoft.Xna.Framework.Audio.SoundEffect.FromStream(stream);
        effect?.Play(Audio.MasterVolume, Audio.MasterPitch, 0f);
      }
      catch (Exception e)
      {
        Log.Error($"[Sfx] ecoute de {file.Name} impossible : {e.Message}");
        Sounds.ui_invalid.Play(160f, 1f);
      }
    }
  }

  /// <summary>
  /// Rappelle en tete d'ecran de quel profil et de quel evenement il s'agit : sans
  /// cela, une liste de noms de fichiers ne dit pas a quoi on est en train de les
  /// rattacher.
  /// </summary>
  public class UIPickerHeader : MenuItem
  {
    private readonly string line;
    private readonly Vector2 tweenFrom;
    private readonly Vector2 tweenTo;

    /// <summary>Place voulue a l'ecran, hors defilement de la liste.</summary>
    private Vector2 anchor;

    public UIPickerHeader(Vector2 position, string profileName, string soundEvent) : base(position)
    {
      line = $"{profileName} - {SoundEvents.Label(soundEvent)}";
      tweenTo = position;
      anchor = position;
      tweenFrom = position - Vector2.UnitY * 40f;
    }

    public override void Render()
    {
      base.Render();
      Draw.OutlineTextCentered(TFGame.Font, MenuText.Safe(line), Position, Calc.HexToColor("FFEC5E"), Color.Black);
    }

    public override void Update()
    {
      base.Update();

      // La liste peut defiler sous ce panneau : on le replace a chaque image plutot
      // que de le laisser suivre la camera du calque.
      Position = MenuCamera.Fixed(MainMenu, anchor);
    }
    public override void TweenIn()
    {
      anchor = tweenFrom;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 20, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(tweenFrom, tweenTo, t.Eased);
      Add(tween);
    }

    public override void TweenOut()
    {
      Vector2 start = anchor;
      Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 12, true);
      tween.OnUpdate = t => anchor = Vector2.Lerp(start, tweenFrom, t.Eased);
      Add(tween);
    }

    protected override void OnSelect() { }

    protected override void OnDeselect() { }

    protected override void OnConfirm() { }
  }
}
