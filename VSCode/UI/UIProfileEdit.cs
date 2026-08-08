using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Fiche d'un profil : nom, archer prefere, costume, suppression.
  ///
  /// Le profil edite arrive par <see cref="UIProfilesMenu.Editing"/>. Il est deja
  /// present dans le stockage quand cet ecran s'ouvre, y compris a la creation : les
  /// modifications portent donc directement sur l'objet, et l'ecriture sur disque a
  /// lieu en sortie d'ecran plutot qu'a chaque touche.
  /// </summary>
  public class UIProfileEdit : CustomMenuState
  {
    // La fiche a grossi au fil des rubriques : dix lignes tiennent tout juste dans les
    // 240 pixels de haut, en laissant la place au guide des boutons en bas d'ecran.
    private const float FirstRowY = 46f;
    private const float RowStep = 16f;
    private const float RowX = 120f;
    private static readonly Vector2 PreviewPosition = new Vector2(56f, 108f);

    private ProfileData profile;
    private UIProfilePreview preview;
    private bool saved;

    public UIProfileEdit(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      MainMenu.MenuState listState = ModRegisters.MenuState<UIProfilesMenu>();

      profile = UIProfilesMenu.Editing;
      if (profile == null)
      {
        // Peut arriver si l'ecran est atteint autrement que par la liste. Rien a
        // editer : on repart d'ou on aurait du venir.
        Main.State = listState;
        return;
      }

      saved = false;
      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfileEdit>());

      Main.BackState = listState;
      Main.TweenBGCameraToY(2);

      preview = new UIProfilePreview(PreviewPosition);
      preview.Show(profile);
      Main.Add(preview);

      var rows = new List<UIMenuRow>();

      UIMenuRow nameRow = MakeRow(rows.Count, "NAME");
      nameRow.RightText = () => DisplayName(profile);
      nameRow.OnConfirmed = AskRename;
      rows.Add(nameRow);

      UIMenuRow archerRow = MakeRow(rows.Count, "ARCHER");
      archerRow.RightText = () => ArcherCatalog.NameOf(ArcherCatalog.IndexOf(profile));
      archerRow.OnLeft = () => CycleArcher(-1);
      archerRow.OnRight = () => CycleArcher(1);
      rows.Add(archerRow);

      UIMenuRow costumeRow = MakeRow(rows.Count, "COSTUME");
      costumeRow.RightText = CostumeLabel;
      costumeRow.OnLeft = ToggleCostume;
      costumeRow.OnRight = ToggleCostume;
      costumeRow.OnConfirmed = ToggleCostume;
      rows.Add(costumeRow);

      UIMenuRow soundsRow = MakeRow(rows.Count, "SOUNDS");
      soundsRow.RightText = TotalSounds;
      soundsRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileSounds>();
      rows.Add(soundsRow);

      UIMenuRow imagesRow = MakeRow(rows.Count, "IMAGES");
      imagesRow.RightText = CountImages;
      imagesRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileImages>();
      rows.Add(imagesRow);

      UIMenuRow colorsRow = MakeRow(rows.Count, "COLORS");
      colorsRow.RightText = CountTrials;
      colorsRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileTrials>();
      rows.Add(colorsRow);

      UIMenuRow gamepadRow = MakeRow(rows.Count, "GAMEPAD");
      gamepadRow.RightText = () => profile.Gamepad == null ? "GLOBAL" : "CUSTOM";
      gamepadRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileGamepad>();
      rows.Add(gamepadRow);

      UIMenuRow keyboardRow = MakeRow(rows.Count, "KEYBOARD");
      keyboardRow.RightText = () => profile.Keyboard == null ? "GLOBAL" : "CUSTOM";
      keyboardRow.OnConfirmed = () => Main.State = ModRegisters.MenuState<UIProfileKeyboard>();
      rows.Add(keyboardRow);

      UIMenuRow saveRow = MakeRow(rows.Count, "SAVE");
      saveRow.RightText = () => saved ? "SAVED" : "";
      saveRow.OnConfirmed = SaveAndClose;
      rows.Add(saveRow);

      UIMenuRow deleteRow = MakeRow(rows.Count, "DELETE PROFILE");
      deleteRow.OnConfirmed = AskDelete;
      rows.Add(deleteRow);

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

      // Filet pour les rubriques a venir : si la liste depasse un jour la hauteur
      // disponible, elle defilera au lieu de cacher ses dernieres lignes.
      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);

      Main.ToStartSelected = rows[0];
    }

    public override void Destroy()
    {
      // Filet de securite : quelle que soit la facon de sortir de l'ecran, y compris
      // par le bouton retour sans passer par SAVE, l'etat en memoire part sur le
      // disque. Rien de ce qui a ete change n'est perdu.
      ProfileStorage.Save();

      // Ni profile ni preview ne sont remis a null ici. Destroy() est appele au
      // debut de la transition, mais les lignes de l'ecran sortant continuent d'etre
      // dessinees une douzaine d'images pendant qu'elles glissent hors champ, et
      // leurs libelles relisent le profil a chaque image.
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);

      return new UIMenuRow(new Vector2(RowX, y), from, label)
      {
        ContentWidth = 175f
      };
    }

    private void AskRename()
    {
      Main.Add(new VirtualKeyboard(
          "PROFILE NAME",
          profile.Name,
          name => UIProfilesMenu.RejectName(name, profile),
          name =>
          {
            // Le dossier de sons porte le nom du profil pour rester lisible : il doit
            // suivre le renommage, sinon les sons deja choisis seraient orphelins.
            string previous = profile.Name;
            profile.Name = name;

            // Sons et images vivent tous deux sous le dossier du profil : deplacer
            // celui-ci emmene les deux, mais le cache d'images garde des chemins de
            // l'ancien nom.
            ProfileSfx.MoveProfileFolder(previous, name);
            ProfileImages.OnProfileRenamed(profile);
          }));
    }

    /// <summary>
    /// Nombre total de sons attaches au profil, tous evenements confondus. Donne une
    /// idee depuis la fiche sans avoir a ouvrir l'ecran des sons.
    /// </summary>
    private string TotalSounds()
    {
      if (profile == null || string.IsNullOrEmpty(profile.Name))
      {
        return "";
      }

      int total = 0;

      foreach (string soundEvent in SoundEvents.Fixed)
      {
        total += ProfileSfx.CountAssigned(profile.Name, soundEvent);
      }

      foreach (ProfileData other in ProfileStorage.Profiles)
      {
        if (!ReferenceEquals(other, profile) && !string.IsNullOrEmpty(other.Name))
        {
          total += ProfileSfx.CountAssigned(profile.Name, SoundEvents.KilledBy(other.Name));
        }
      }

      return total == 0 ? "-" : total.ToString();
    }

    private void CycleArcher(int direction)
    {
      int count = ArcherCatalog.Count;
      if (count == 0)
      {
        return;
      }

      int index = ArcherCatalog.IndexOf(profile) + direction;
      ArcherCatalog.SetArcher(profile, index);

      // Les essais sont ranges par archer et par costume : changer d''archer ne perd
      // plus rien, ceux de l''ancien restent en place et sortent simplement du filtre.
      // Seule la palette relevee dans l''atlas doit etre relue.
      SpriteRecolor.Forget(profile);

      // Un archer sans tenue alternative afficherait "ALT" pour un portrait
      // identique : on retombe sur la tenue normale plutot que d'annoncer un choix
      // qui ne se voit pas.
      if (profile.IsAlt && !ArcherCatalog.HasAlt(ArcherCatalog.IndexOf(profile)))
      {
        profile.Costume = ProfileCostumes.Normal;
      }
    }

    /// <summary>Nombre d''essais pour l''archer et le costume courants.</summary>
    private string CountTrials()
    {
      if (profile == null)
      {
        return "";
      }

      int count = ProfileTrials.For(profile).Count;
      return count == 0 ? "-" : count.ToString();
    }

    /// <summary>Nombre d''emplacements d'image remplis, sur les six possibles.</summary>
    private string CountImages()
    {
      if (profile == null || string.IsNullOrEmpty(profile.Name))
      {
        return "";
      }

      int filled = 0;
      foreach (string slot in ProfileImages.Slots)
      {
        if (ProfileImages.HasImage(profile, slot))
        {
          filled++;
        }
      }

      return filled == 0 ? "-" : filled + "/" + ProfileImages.Slots.Length;
    }

    private static string DisplayName(ProfileData profile)
    {
      return profile == null || string.IsNullOrEmpty(profile.Name) ? "(UNNAMED)" : profile.Name;
    }

    /// <summary>
    /// Ecrit la fiche et revient a la liste.
    ///
    /// La sortie par le bouton retour enregistre aussi : ce bouton ne rend pas la
    /// sauvegarde possible, il la rend visible, et sert de "termine" explicite.
    /// </summary>
    private void SaveAndClose()
    {
      ProfileStorage.Save();
      saved = true;
      Main.State = ModRegisters.MenuState<UIProfilesMenu>();
    }

    private string CostumeLabel()
    {
      if (profile == null || !ArcherCatalog.HasAlt(ArcherCatalog.IndexOf(profile)))
      {
        return ProfileCostumes.Normal + " (ONLY)";
      }

      return profile.IsAlt ? ProfileCostumes.Alt : ProfileCostumes.Normal;
    }

    private void ToggleCostume()
    {
      if (!ArcherCatalog.HasAlt(ArcherCatalog.IndexOf(profile)))
      {
        return;
      }

      profile.Costume = profile.IsAlt ? ProfileCostumes.Normal : ProfileCostumes.Alt;
    }

    private void AskDelete()
    {
      ProfileData deleted = profile;
      Dialogs.Confirm(Main, "DELETE PROFILE", DisplayName(deleted),
          () =>
          {
            ProfileStorage.Remove(deleted);
            UIProfilesMenu.Editing = null;
            Main.State = ModRegisters.MenuState<UIProfilesMenu>();
          });
    }
  }
}
