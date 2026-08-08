using System;
using System.Collections.Generic;
using FortRise;
using Microsoft.Xna.Framework;
using TowerFall;

namespace TFModFortRiseProfiles
{
  /// <summary>
  /// Ecran de la liste des profils : ouvert par la lame PROFILES du menu principal.
  ///
  /// C'est un CustomMenuState et non une Scene a part : MainMenu sait deja faire
  /// entrer et sortir un ecran en fondu, lui donner un titre, gerer le bouton retour
  /// et le fil des transitions. Une scene independante obligerait a refaire tout
  /// cela, et a quitter puis reconstruire le menu principal a chaque aller-retour.
  ///
  /// FortRise garde une instance par type et la reutilise ; l'etat propre a un
  /// affichage donne est donc reconstruit dans Create() et non dans le constructeur.
  /// </summary>
  public class UIProfilesMenu : CustomMenuState
  {
    /// <summary>
    /// Profil que l'ecran d'edition doit ouvrir. Passe par un champ statique parce
    /// que MainMenu instancie les etats lui-meme : il n'y a pas de place, dans
    /// CallStateFunc, pour transmettre un argument.
    /// </summary>
    internal static ProfileData Editing;

    private const float FirstRowY = 54f;
    private const float RowStep = 16f;
    private const float RowX = 108f;
    private static readonly Vector2 PreviewPosition = new Vector2(52f, 104f);

    private readonly List<MenuItem> items = new List<MenuItem>();
    private UIProfilePreview preview;

    public UIProfilesMenu(MainMenu main) : base(main)
    {
    }

    public override void Create()
    {
      ScreenTitles.Apply(Main, ModRegisters.MenuState<UIProfilesMenu>());

      Main.BackState = MainMenu.MenuState.Main;
      Main.TweenBGCameraToY(2);

      preview = new UIProfilePreview(PreviewPosition);
      Main.Add(preview);
      items.Clear();
      items.Add(preview);

      Build(0);
    }

    public override void Destroy()
    {
      items.Clear();
      preview = null;
    }

    /// <summary>
    /// (Re)construit les lignes depuis le stockage.
    /// </summary>
    /// <param name="selectIndex">
    /// Rang a selectionner parmi les lignes construites, borne a la liste obtenue.
    /// Sert a rester au meme endroit apres une suppression.
    /// </param>
    private void Build(int selectIndex)
    {
      var rows = new List<UIMenuRow>();
      List<ProfileData> profiles = ProfileStorage.Profiles;

      UIMenuRow addRow = MakeRow(0, "+ NEW PROFILE");
      addRow.OnConfirmed = AskNewProfile;
      rows.Add(addRow);

      foreach (ProfileData profile in profiles)
      {
        ProfileData captured = profile;
        UIMenuRow row = MakeRow(rows.Count, DisplayName(captured));
        row.RightText = () => ArcherLabel(captured);
        row.OnConfirmed = () => Edit(captured);
        row.OnAlt = () => AskDelete(captured);
        row.AltGuide = "DELETE";
        row.OnSelected = () => preview?.Show(captured);
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
      items.AddRange(rows);

      float lastY = FirstRowY + (rows.Count - 1) * RowStep;
      Main.MaxUICameraY = Math.Max(0f, lastY - 190f);

      MenuItem toSelect = rows[Math.Clamp(selectIndex, 0, rows.Count - 1)];
      Main.ToStartSelected = toSelect;

      // Au premier affichage MainMenu selectionne ToStartSelected une fois la
      // transition finie ; apres une reconstruction en place il n'y a pas de
      // transition, il faut donc rendre le focus soi-meme.
      if (!Main.Transitioning)
      {
        toSelect.Selected = true;
      }
    }

    private UIMenuRow MakeRow(int index, string label)
    {
      float y = FirstRowY + index * RowStep;

      // Les lignes entrent alternativement par la gauche et par la droite, comme les
      // boutons de l'ecran d'options du jeu.
      var from = new Vector2(index % 2 == 0 ? -200f : 520f, y);

      var row = new UIMenuRow(new Vector2(RowX, y), from, label);

      // Par defaut une ligne vide le portrait : c'est le cas de "+ NEW PROFILE", qui
      // ne designe aucun profil.
      row.OnSelected = () => preview?.Show(null);
      return row;
    }

    private static string DisplayName(ProfileData profile)
    {
      return string.IsNullOrEmpty(profile.Name) ? "(UNNAMED)" : profile.Name;
    }

    private static string ArcherLabel(ProfileData profile)
    {
      string archer = ArcherCatalog.NameOf(ArcherCatalog.IndexOf(profile));
      if (archer.Length == 0)
      {
        return "";
      }

      // "ALT" en toutes lettres et non une etoile : la police du jeu ne connait pas
      // le caractere '*' et ne dessinait rien, les profils en tenue alternative
      // etaient donc indiscernables des autres.
      return profile.IsAlt ? archer + " ALT" : archer;
    }

    private void AskNewProfile()
    {
      Main.Add(new VirtualKeyboard(
          "NEW PROFILE",
          "",
          name => RejectName(name, null),
          name =>
          {
            ProfileData profile = ProfileStorage.Add(name);
            Edit(profile);
          }));
    }

    private void AskDelete(ProfileData profile)
    {
      Dialogs.Confirm(Main, "DELETE PROFILE", DisplayName(profile), () =>
      {
        int index = ProfileStorage.Profiles.IndexOf(profile);
        ProfileStorage.Remove(profile);
        Rebuild(index);
      });
    }

    private void Rebuild(int removedIndex)
    {
      foreach (MenuItem item in items)
      {
        if (item != preview)
        {
          Main.Remove(item);
        }
      }

      items.RemoveAll(item => item != preview);
      preview?.Show(null);

      // removedIndex compte dans la liste des profils, les lignes ont en plus
      // "+ NEW PROFILE" en tete. Apres suppression du dernier profil, ce decalage
      // ramene naturellement le focus sur la ligne qui a pris sa place, ou sur celle
      // du dessus s'il n'y en a plus.
      Build(removedIndex + 1);
    }

    private void Edit(ProfileData profile)
    {
      Editing = profile;
      Main.State = ModRegisters.MenuState<UIProfileEdit>();
    }

    /// <summary>
    /// Regles de nommage partagees par la creation et le renommage.
    /// </summary>
    /// <returns>null si le nom convient, sinon le message a afficher.</returns>
    internal static string RejectName(string name, ProfileData except)
    {
      if (string.IsNullOrEmpty(name))
      {
        return "EMPTY NAME";
      }

      if (ProfileStorage.NameTaken(name, except))
      {
        return "ALREADY IN THE LIST";
      }

      return null;
    }
  }
}
